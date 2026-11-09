using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;
using eQuantic.UI.Server.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace eQuantic.UI.Server;

/// <summary>
/// ASP.NET Core middleware for handling Server Actions.
/// Provides secure RPC-style invocation of server-side methods from client components.
/// </summary>
/// <remarks>
/// This middleware:
/// <list type="bullet">
///   <item>Validates request payload size and format</item>
///   <item>Enforces authorization via <see cref="IServerActionAuthorizationService"/></item>
///   <item>Invokes the Server Action method with deserialized arguments</item>
///   <item>Returns JSON responses with success/failure status</item>
/// </list>
/// </remarks>
public class ServerActionsMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IServerActionRegistry _registry;
    private readonly IServiceProvider _serviceProvider;
    private readonly IServerActionAuthorizationService _authorizationService;
    private readonly ILogger<ServerActionsMiddleware> _logger;
    private readonly HashSet<Assembly> _allowedAssemblies;
    private readonly AllowedRequestOrigins _origins;
    private readonly IOptionsMonitor<ServerActionsOptions> _actions;

    private const string ActionsPath = "/api/_equantic/actions";

    /// <summary>
    /// How an action's arguments are read: as the wire writes them, an <see cref="Primitives.UploadedFile"/>
    /// refused anywhere but as an action's own parameter (<see cref="UploadedFileArgumentConverter"/>).
    /// </summary>
    private static readonly JsonSerializerOptions ArgumentOptions = new(Json.EqJson.Options)
    {
        Converters = { new UploadedFileArgumentConverter() },
    };

    public ServerActionsMiddleware(
        RequestDelegate next,
        IServerActionRegistry registry,
        IServiceProvider serviceProvider,
        IServerActionAuthorizationService authorizationService,
        UIOptions options,
        IOptionsMonitor<ServerActionsOptions> actions,
        ILogger<ServerActionsMiddleware> logger)
    {
        _next = next;
        _registry = registry;
        _serviceProvider = serviceProvider;
        _authorizationService = authorizationService;
        _logger = logger;

        // Complex Server Action parameter types are only deserialized when declared in the
        // application's own (scanned) assemblies or an explicitly opted-in assembly.
        _allowedAssemblies = new HashSet<Assembly>(options.AssembliesToScan);
        _allowedAssemblies.UnionWith(options.AllowedDeserializationAssemblies);
        // A reloaded appsettings.json changes the allowed origins without a restart.
        _origins = new AllowedRequestOrigins(actions);
        _actions = actions;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (context.Request.Path == ActionsPath && context.Request.Method == "POST")
        {
            await HandleServerAction(context);
            return;
        }

        await _next(context);
    }

    private async Task HandleServerAction(HttpContext context)
    {
        // A request another site's page sends is refused before anything of it is read (#678).
        if (!_origins.Allows(context.Request))
        {
            _logger.LogWarning(
                "Server Action request refused - it came from another site: Origin {Origin}, Sec-Fetch-Site {FetchSite}",
                AllowedRequestOrigins.ForLog(context.Request.Headers.Origin.ToString()),
                AllowedRequestOrigins.ForLog(context.Request.Headers["Sec-Fetch-Site"].ToString()));
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            await WriteErrorResponse(context, "A request from another site cannot call a Server Action.");
            return;
        }

        // A JSON body, which a page's fetch sends: another content type is one a plain form can post
        // without a preflight, so it is refused before anything of it is read, beside the origin check (#1291).
        if (!context.Request.HasJsonContentType())
        {
            _logger.LogWarning(
                "Server Action request refused - its content type is {ContentType}, not JSON",
                AllowedRequestOrigins.ForLog(context.Request.ContentType ?? "none"));
            context.Response.StatusCode = StatusCodes.Status415UnsupportedMediaType;
            await WriteErrorResponse(context, "A Server Action takes a JSON body.");
            return;
        }

        var maxRequestBodySize = _actions.CurrentValue.MaxRequestBodySize;

        // Validate Content-Length to prevent oversized payloads
        if (context.Request.ContentLength > maxRequestBodySize)
        {
            _logger.LogWarning(
                "Server Action request rejected - payload too large: {Size} bytes (max: {MaxSize})",
                context.Request.ContentLength,
                maxRequestBodySize);

            context.Response.StatusCode = StatusCodes.Status413PayloadTooLarge;
            await WriteErrorResponse(context, "Request payload too large.");
            return;
        }

        // Content-Length is absent for chunked / Transfer-Encoding requests, so the check
        // above does not catch them. Kestrel stops reading at the cap where its feature is still
        // writable, and the body is read through the cap in every case: a middleware that started
        // reading it first leaves the feature read-only, and the cap was skipped (#1291).
        var maxBodySizeFeature = context.Features.Get<IHttpMaxRequestBodySizeFeature>();
        if (maxBodySizeFeature is { IsReadOnly: false })
        {
            maxBodySizeFeature.MaxRequestBodySize = maxRequestBodySize;
        }

        ServerActionRequest? request;
        try
        {
            // Enable buffering for potential re-reads and limit body size
            context.Request.EnableBuffering();

            request = await JsonSerializer.DeserializeAsync<ServerActionRequest>(
                new BoundedReadStream(context.Request.Body, maxRequestBodySize),
                new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    MaxDepth = 32 // Prevent deeply nested payloads
                }
            );
        }
        catch (BadHttpRequestException ex)
        {
            _logger.LogWarning(ex, "Server Action request body exceeded the {MaxSize}-byte limit", maxRequestBodySize);
            context.Response.StatusCode = StatusCodes.Status413PayloadTooLarge;
            await WriteErrorResponse(context, "Request payload too large.");
            return;
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Invalid JSON in Server Action request");
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            await WriteErrorResponse(context, "Invalid request format.");
            return;
        }

        if (request == null || string.IsNullOrEmpty(request.ActionId))
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            await WriteErrorResponse(context, "Missing or invalid ActionId.");
            return;
        }

        // Sanitize action ID to prevent injection
        var actionId = SanitizeActionId(request.ActionId);
        // The caller's own text, so it reaches a plain-text log without a line break of its own (#1291).
        var loggedId = AllowedRequestOrigins.ForLog(actionId);

        var descriptor = _registry.GetAction(actionId);
        if (descriptor == null)
        {
            _logger.LogWarning("Server Action not found: {ActionId}", loggedId);
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            // The id is not echoed: the response says nothing the request did not (#1291).
            await WriteErrorResponse(context, "Action not found.");
            return;
        }

        // Authorization check
        var authResult = await _authorizationService.AuthorizeAsync(context, descriptor);
        if (!authResult.Succeeded)
        {
            if (authResult.IsUnauthenticated)
            {
                _logger.LogWarning(
                    "Unauthenticated access attempt to Server Action {ActionId}",
                    loggedId);
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                await WriteErrorResponse(context, "Authentication required.");
            }
            else
            {
                _logger.LogWarning(
                    "Forbidden access attempt to Server Action {ActionId}: {Reason}",
                    loggedId,
                    authResult.FailureReason);
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                // The reason names the roles and policies the action requires: it stays in the log (#1291).
                await WriteErrorResponse(context, "Access denied.");
            }
            return;
        }

        // Where a component finds a capability DURING the action — the same request container the
        // renderer arms, so GetService<T>() answers here exactly as it answers inside Build. It did
        // not, and the gap forced a page to choose: take services through the constructor and reach
        // them in actions, or take none and keep its prefetched state. Neither half was optional,
        // and no page could have both.
        var outerScope = Primitives.CapabilityScope.Current;
        Primitives.CapabilityScope.Current = context.RequestServices.GetService;

        try
        {
            // Built from THIS REQUEST's services. A middleware is a singleton, so the provider it
            // was constructed with is the ROOT one — and a server action is exactly where the
            // scoped things live: the DbContext the action queries, the unit of work it commits,
            // the tenant it belongs to. .NET refuses to resolve those from the root by design.
            // Deserialize and validate arguments. A malformed one is the caller's, answered 400 with a warning:
            // it answered 500 and logged an error, so any caller could fill the log (#1291).
            object?[] args;
            try
            {
                args = await DeserializeArgumentsAsync(context, request.Arguments, descriptor.ParameterTypes);
            }
            catch (JsonException ex)
            {
                _logger.LogWarning("Server Action {ActionId} refused its arguments: {Reason}", loggedId, ex.Message);
                context.Response.StatusCode = StatusCodes.Status400BadRequest;
                await WriteErrorResponse(context, "The action's arguments are not what it takes.");
                return;
            }

            var component = ActivatorUtilities.CreateInstance(context.RequestServices, descriptor.ComponentType);

            // Invoke the method
            object? result;
            if (descriptor.IsAsync)
            {
                var task = (Task)descriptor.Method.Invoke(component, args)!;
                await task;

                // Get result from Task<T> if applicable
                var resultProperty = task.GetType().GetProperty("Result");
                result = resultProperty?.GetValue(task);
            }
            else
            {
                result = descriptor.Method.Invoke(component, args);
            }

            _logger.LogDebug(
                "Server Action {ActionId} executed successfully",
                loggedId);

            // EqJson serializes Int64/UInt64 as strings (the eQuantic long wire protocol) so values
            // beyond 2^53 survive into the client BigInt-backed `long` runtime. The result is written as
            // the type the action declares: written as its runtime type, an entity returned where a
            // summary was declared sent every field it has (#1291).
            await WriteResultAsync(context, result, DeclaredResultType(descriptor.Method));
        }
        catch (UploadNotBoundException ex)
        {
            // The page named an upload it may not receive, or one not finished: the action never ran, and
            // the reason is one the page shows.
            _logger.LogWarning("Server Action {ActionId} refused its file: {Reason}", loggedId, ex.Message);
            context.Response.StatusCode = ex.StatusCode;
            await WriteErrorResponse(context, ex.Message);
        }
        catch (Exception ex)
        {
            // Log the full exception but return sanitized message to client
            _logger.LogError(ex,
                "Server Action {ActionId} failed with exception",
                loggedId);

            context.Response.StatusCode = StatusCodes.Status500InternalServerError;

            // In production, don't expose internal error details
            var errorMessage = IsDevelopment(context)
                ? ex.InnerException?.Message ?? ex.Message
                : "An error occurred while processing the request.";

            await WriteErrorResponse(context, errorMessage);
        }
        finally
        {
            // RESTORED, not cleared: whatever armed the scope before this action is entitled to
            // still be in force after it.
            Primitives.CapabilityScope.Current = outerScope;
        }
    }

    private static string SanitizeActionId(string actionId)
    {
        // Remove any potentially dangerous characters
        // ActionId format should be: "ComponentName/MethodName"
        return actionId
            .Replace("..", "")
            .Replace("\\", "")
            .Trim();
    }

    /// <summary>The type an action declares it answers: the <c>T</c> of a <c>Task&lt;T&gt;</c>, its return type
    /// otherwise, and none for <c>void</c> or a bare <c>Task</c>.</summary>
    private static Type? DeclaredResultType(MethodInfo method)
    {
        var returned = method.ReturnType;
        if (returned == typeof(void) || returned == typeof(Task)) return null;
        return returned.IsGenericType && returned.GetGenericTypeDefinition() == typeof(Task<>)
            ? returned.GetGenericArguments()[0]
            : returned;
    }

    /// <summary>The answer <c>{ success, result }</c>, the result serialized once, as <paramref name="declared"/>.</summary>
    private static async Task WriteResultAsync(HttpContext context, object? result, Type? declared)
    {
        var buffer = new System.Buffers.ArrayBufferWriter<byte>();
        await using (var writer = new Utf8JsonWriter(buffer, new JsonWriterOptions { Encoder = Json.EqJson.Options.Encoder }))
        {
            writer.WriteStartObject();
            writer.WriteBoolean("success", true);
            writer.WritePropertyName("result");
            if (result is null || declared is null) writer.WriteNullValue();
            else JsonSerializer.Serialize(writer, result, declared, Json.EqJson.Options);
            writer.WriteEndObject();
        }
        context.Response.ContentType = "application/json; charset=utf-8";
        await context.Response.Body.WriteAsync(buffer.WrittenMemory, context.RequestAborted);
    }

    private static async Task WriteErrorResponse(HttpContext context, string error)
    {
        await context.Response.WriteAsJsonAsync(new { success = false, error }, Json.EqJson.Options);
    }

    private static bool IsDevelopment(HttpContext context)
    {
        var env = context.RequestServices.GetService<Microsoft.AspNetCore.Hosting.IWebHostEnvironment>();
        return env?.EnvironmentName == "Development";
    }

    private async Task<object?[]> DeserializeArgumentsAsync(HttpContext context, JsonElement[]? arguments, Type[] parameterTypes)
    {
        if (arguments == null || arguments.Length == 0)
            return Array.Empty<object?>();

        if (arguments.Length > parameterTypes.Length)
        {
            _logger.LogWarning(
                "Server Action received more arguments ({Received}) than expected ({Expected})",
                arguments.Length,
                parameterTypes.Length);
        }

        var result = new object?[parameterTypes.Length];

        for (int i = 0; i < Math.Min(arguments.Length, parameterTypes.Length); i++)
        {
            var paramType = parameterTypes[i];

            // A file the person uploaded: the argument names it, and the server binds its own record of
            // it, for its owner alone.
            if (paramType == typeof(Primitives.UploadedFile))
            {
                result[i] = await UploadBinding.BindAsync(context, arguments[i]);
                continue;
            }

            // Validate that we're not deserializing to dangerous types
            if (!IsAllowedType(paramType))
            {
                _logger.LogWarning(
                    "Attempted to deserialize to disallowed type: {Type}",
                    paramType.FullName);
                throw new InvalidOperationException($"Type '{paramType.Name}' is not allowed for deserialization.");
            }

            // EqJson.Options accepts Int64/UInt64 as either string (the eQuantic long wire
            // protocol) or number, so large values from the client BigInt runtime survive.
            result[i] = arguments[i].Deserialize(paramType, ArgumentOptions);
        }

        return result;
    }

    /// <summary>
    /// Validates that a type is safe for deserialization.
    /// </summary>
    private bool IsAllowedType(Type type)
    {
        // Allow primitives and common types
        if (type.IsPrimitive || type == typeof(string) || type == typeof(decimal) ||
            type == typeof(DateTime) || type == typeof(DateTimeOffset) ||
            type == typeof(Guid) || type == typeof(TimeSpan))
        {
            return true;
        }

        // Allow nullable versions of allowed types
        var underlyingType = Nullable.GetUnderlyingType(type);
        if (underlyingType != null)
        {
            return IsAllowedType(underlyingType);
        }

        // Allow arrays and lists of allowed types
        if (type.IsArray)
        {
            return IsAllowedType(type.GetElementType()!);
        }

        if (type.IsGenericType)
        {
            var genericDef = type.GetGenericTypeDefinition();

            // Allow common generic collections
            if (genericDef == typeof(List<>) ||
                genericDef == typeof(IList<>) ||
                genericDef == typeof(IEnumerable<>) ||
                genericDef == typeof(ICollection<>) ||
                genericDef == typeof(Dictionary<,>) ||
                genericDef == typeof(IDictionary<,>))
            {
                return type.GetGenericArguments().All(IsAllowedType);
            }
        }

        // Allow enums
        if (type.IsEnum)
        {
            return true;
        }

        // Complex types (DTOs/models) are allowed only when declared in the application's own
        // scanned assemblies, or an assembly explicitly opted in via
        // UIOptions.AllowedDeserializationAssemblies. This is an allow-list (secure by default):
        // framework, system and third-party types are rejected unless opted in.
        if (type.IsClass || type.IsValueType)
        {
            return _allowedAssemblies.Contains(type.Assembly);
        }

        return false;
    }
}

/// <summary>
/// Request payload for server actions.
/// </summary>
public class ServerActionRequest
{
    public string ActionId { get; set; } = string.Empty;
    public JsonElement[]? Arguments { get; set; }
}
