using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace eQuantic.UI.Server;

/// <summary>
/// Extension methods for registering dynamic JavaScript endpoints.
/// </summary>
public static class ScriptEndpointExtensions
{
    /// <summary>
    /// Maps a dynamic JavaScript endpoint that generates content on each request.
    /// </summary>
    /// <param name="endpoints">The endpoint route builder.</param>
    /// <param name="path">The URL path (e.g., "/_equantic/theme.js").</param>
    /// <param name="scriptGenerator">Function that generates the JavaScript content.</param>
    /// <returns>The endpoint route builder for chaining.</returns>
    public static IEndpointRouteBuilder MapScriptJs(
        this IEndpointRouteBuilder endpoints,
        string path,
        Func<string> scriptGenerator)
    {
        endpoints.MapGet(path, async context =>
        {
            context.Response.ContentType = "application/javascript; charset=utf-8";
            context.Response.Headers.CacheControl = "public, max-age=3600";
            await context.Response.WriteAsync(scriptGenerator());
        });
        
        return endpoints;
    }
    
    /// <summary>
    /// Maps a dynamic JavaScript endpoint with access to HttpContext.
    /// Useful for generating request-specific scripts.
    /// </summary>
    /// <param name="endpoints">The endpoint route builder.</param>
    /// <param name="path">The URL path.</param>
    /// <param name="scriptGenerator">Function that generates the JavaScript content with access to the HttpContext.</param>
    /// <returns>The endpoint route builder for chaining.</returns>
    public static IEndpointRouteBuilder MapScriptJs(
        this IEndpointRouteBuilder endpoints,
        string path,
        Func<HttpContext, string> scriptGenerator)
    {
        endpoints.MapGet(path, async context =>
        {
            context.Response.ContentType = "application/javascript; charset=utf-8";
            await context.Response.WriteAsync(scriptGenerator(context));
        });
        
        return endpoints;
    }
}
