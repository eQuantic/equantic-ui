using System;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using eQuantic.UI.Server.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

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

    private const string ActionsPath = "/api/_equantic/actions";

    /// <summary>
    /// Maximum allowed request body size in bytes (1 MB).
    /// </summary>
    private const long MaxRequestBodySize = 1_048_576;

    public ServerActionsMiddleware(
        RequestDelegate next,
        IServerActionRegistry registry,
        IServiceProvider serviceProvider,
        IServerActionAuthorizationService authorizationService,
        ILogger<ServerActionsMiddleware> logger)
    {
        _next = next;
        _registry = registry;
        _serviceProvider = serviceProvider;
        _authorizationService = authorizationService;
        _logger = logger;
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
        // Validate Content-Length to prevent oversized payloads
        if (context.Request.ContentLength > MaxRequestBodySize)
        {
            _logger.LogWarning(
                "Server Action request rejected - payload too large: {Size} bytes (max: {MaxSize})",
                context.Request.ContentLength,
                MaxRequestBodySize);

            context.Response.StatusCode = StatusCodes.Status413PayloadTooLarge;
            await WriteErrorResponse(context, "Request payload too large.");
            return;
        }

        ServerActionRequest? request;
        try
        {
            // Enable buffering for potential re-reads and limit body size
            context.Request.EnableBuffering();

            request = await JsonSerializer.DeserializeAsync<ServerActionRequest>(
                context.Request.Body,
                new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    MaxDepth = 32 // Prevent deeply nested payloads
                }
            );
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

        var descriptor = _registry.GetAction(actionId);
        if (descriptor == null)
        {
            _logger.LogWarning("Server Action not found: {ActionId}", actionId);
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            await WriteErrorResponse(context, $"Action not found: {actionId}");
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
                    actionId);
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                await WriteErrorResponse(context, "Authentication required.");
            }
            else
            {
                _logger.LogWarning(
                    "Forbidden access attempt to Server Action {ActionId}: {Reason}",
                    actionId,
                    authResult.FailureReason);
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                await WriteErrorResponse(context, authResult.FailureReason ?? "Access denied.");
            }
            return;
        }

        try
        {
            // Create component instance with DI
            var component = ActivatorUtilities.CreateInstance(_serviceProvider, descriptor.ComponentType);

            // Deserialize and validate arguments
            var args = DeserializeArguments(request.Arguments, descriptor.ParameterTypes);

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
                actionId);

            await context.Response.WriteAsJsonAsync(new { success = true, result });
        }
        catch (Exception ex)
        {
            // Log the full exception but return sanitized message to client
            _logger.LogError(ex,
                "Server Action {ActionId} failed with exception",
                actionId);

            context.Response.StatusCode = StatusCodes.Status500InternalServerError;

            // In production, don't expose internal error details
            var errorMessage = IsDevelopment(context)
                ? ex.InnerException?.Message ?? ex.Message
                : "An error occurred while processing the request.";

            await WriteErrorResponse(context, errorMessage);
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

    private static async Task WriteErrorResponse(HttpContext context, string error)
    {
        await context.Response.WriteAsJsonAsync(new { success = false, error });
    }

    private static bool IsDevelopment(HttpContext context)
    {
        var env = context.RequestServices.GetService<Microsoft.AspNetCore.Hosting.IWebHostEnvironment>();
        return env?.EnvironmentName == "Development";
    }

    private object?[] DeserializeArguments(JsonElement[]? arguments, Type[] parameterTypes)
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

            // Validate that we're not deserializing to dangerous types
            if (!IsAllowedType(paramType))
            {
                _logger.LogWarning(
                    "Attempted to deserialize to disallowed type: {Type}",
                    paramType.FullName);
                throw new InvalidOperationException($"Type '{paramType.Name}' is not allowed for deserialization.");
            }

            result[i] = arguments[i].Deserialize(paramType);
        }

        return result;
    }

    /// <summary>
    /// Validates that a type is safe for deserialization.
    /// </summary>
    private static bool IsAllowedType(Type type)
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
            if (genericDef == typeof(System.Collections.Generic.List<>) ||
                genericDef == typeof(System.Collections.Generic.IList<>) ||
                genericDef == typeof(System.Collections.Generic.IEnumerable<>) ||
                genericDef == typeof(System.Collections.Generic.ICollection<>) ||
                genericDef == typeof(System.Collections.Generic.Dictionary<,>) ||
                genericDef == typeof(System.Collections.Generic.IDictionary<,>))
            {
                return type.GetGenericArguments().All(IsAllowedType);
            }
        }

        // Allow enums
        if (type.IsEnum)
        {
            return true;
        }

        // Allow classes/structs from the same assembly as the Server project
        // or from the application's assemblies (custom DTOs)
        // This is a permissive check - in stricter scenarios, maintain an explicit whitelist
        if (type.IsClass || type.IsValueType)
        {
            // Deny system types that could be dangerous
            var ns = type.Namespace ?? "";
            if (ns.StartsWith("System.Reflection") ||
                ns.StartsWith("System.Runtime") ||
                ns.StartsWith("System.Diagnostics") ||
                ns.StartsWith("System.IO") ||
                ns.StartsWith("System.Security") ||
                ns.StartsWith("System.Threading") ||
                ns.StartsWith("Microsoft."))
            {
                return false;
            }

            // Allow application types (DTOs, models, etc.)
            return true;
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
