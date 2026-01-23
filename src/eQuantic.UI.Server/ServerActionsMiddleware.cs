using System;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace eQuantic.UI.Server;

/// <summary>
/// ASP.NET Core middleware for handling Server Actions.
/// </summary>
public class ServerActionsMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IServerActionRegistry _registry;
    private readonly IServiceProvider _serviceProvider;
    
    private const string ActionsPath = "/api/_equantic/actions";
    
    public ServerActionsMiddleware(
        RequestDelegate next, 
        IServerActionRegistry registry,
        IServiceProvider serviceProvider)
    {
        _next = next;
        _registry = registry;
        _serviceProvider = serviceProvider;
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
        try
        {
            var request = await JsonSerializer.DeserializeAsync<ServerActionRequest>(
                context.Request.Body,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
            );
            
            if (request == null || string.IsNullOrEmpty(request.ActionId))
            {
                context.Response.StatusCode = 400;
                await context.Response.WriteAsJsonAsync(new { success = false, error = "Invalid request" });
                return;
            }
            
            var descriptor = _registry.GetAction(request.ActionId);
            if (descriptor == null)
            {
                context.Response.StatusCode = 404;
                await context.Response.WriteAsJsonAsync(new { success = false, error = $"Action not found: {request.ActionId}" });
                return;
            }
            
            // Create component instance
            var component = ActivatorUtilities.CreateInstance(_serviceProvider, descriptor.ComponentType);
            
            // Deserialize arguments
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
            
            await context.Response.WriteAsJsonAsync(new { success = true, result });
        }
        catch (Exception ex)
        {
            context.Response.StatusCode = 500;
            await context.Response.WriteAsJsonAsync(new { success = false, error = ex.Message });
        }
    }
    
    private object?[] DeserializeArguments(JsonElement[]? arguments, Type[] parameterTypes)
    {
        if (arguments == null || arguments.Length == 0)
            return Array.Empty<object?>();
        
        var result = new object?[parameterTypes.Length];
        
        for (int i = 0; i < Math.Min(arguments.Length, parameterTypes.Length); i++)
        {
            result[i] = arguments[i].Deserialize(parameterTypes[i]);
        }
        
        return result;
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
