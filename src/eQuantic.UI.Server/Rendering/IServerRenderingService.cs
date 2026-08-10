using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

using eQuantic.UI.Core.Assets;
using eQuantic.UI.Core.Metadata;

namespace eQuantic.UI.Server.Rendering;

/// <summary>
/// Service responsible for server-side rendering of eQuantic.UI components.
/// </summary>
/// <remarks>
/// SSR enables:
/// <list type="bullet">
///   <item>SEO optimization - search engines can index the rendered HTML</item>
///   <item>Faster First Contentful Paint (FCP) - users see content immediately</item>
///   <item>Better performance on slow networks - no JS required for initial view</item>
///   <item>Social media preview cards - Open Graph tags rendered server-side</item>
/// </list>
///
/// The rendering flow is:
/// C# Component → Render() → HtmlNode (Virtual DOM) → ToHtml() → HTML String
///
/// This bypasses the TypeScript compilation entirely for the initial render.
/// </remarks>
public interface IServerRenderingService
{
    /// <summary>
    /// Renders a page component to HTML string for SSR.
    /// </summary>
    /// <param name="pageTypeName">The name of the page component type.</param>
    /// <param name="context">The HTTP context for the current request.</param>
    /// <returns>The rendered HTML string, or null if SSR is not available for this page.</returns>
    Task<ServerRenderResult> RenderPageAsync(string pageTypeName, HttpContext context);

    /// <summary>
    /// Renders a component instance to HTML string.
    /// </summary>
    /// <param name="component">The component instance to render.</param>
    /// <returns>The rendered HTML string.</returns>
    string RenderComponent(Core.IComponent component);

    /// <summary>
    /// Checks if SSR is enabled for a specific page.
    /// </summary>
    /// <param name="pageTypeName">The name of the page component type.</param>
    /// <returns>True if SSR is enabled for this page.</returns>
    bool IsSsrEnabled(string pageTypeName);
}

/// <summary>
/// Result of server-side rendering operation.
/// </summary>
public sealed class ServerRenderResult
{
    private ServerRenderResult(bool success, string? html, MetadataCollection? metadata, string? error, string? serializedState, AssetCollection? assets = null)
    {
        Success = success;
        Html = html;
        Metadata = metadata;
        Error = error;
        SerializedState = serializedState;
        Assets = assets;
    }

    /// <summary>
    /// Whether the rendering was successful.
    /// </summary>
    public bool Success { get; }

    /// <summary>
    /// The rendered HTML string (if successful).
    /// </summary>
    public string? Html { get; }

    /// <summary>
    /// The extracted metadata from the component (if implemented).
    /// </summary>
    public MetadataCollection? Metadata { get; }

    /// <summary>
    /// Error message (if unsuccessful).
    /// </summary>
    public string? Error { get; }

    /// <summary>
    /// Serialized state for client-side hydration (JSON).
    /// </summary>
    public string? SerializedState { get; }

    /// <summary>
    /// Asset dependencies collected from components during rendering.
    /// </summary>
    public AssetCollection? Assets { get; }

    /// <summary>
    /// What the page asked the server to ANSWER with (<see cref="Primitives.IHandleStatus"/>), or
    /// 200 when it asked for nothing. A route that matched while its content did not exist renders
    /// fine and must not answer OK — see the interface for why that is a machine-visible failure.
    /// </summary>
    public int StatusCode { get; private init; } = 200;

    /// <summary>
    /// Creates a successful render result.
    /// </summary>
    public static ServerRenderResult Ok(string html, MetadataCollection? metadata = null,
        string? serializedState = null, AssetCollection? assets = null, int statusCode = 200) =>
        new(true, html, metadata, null, serializedState, assets) { StatusCode = statusCode };

    /// <summary>
    /// Creates a failed render result.
    /// </summary>
    public static ServerRenderResult Fail(string error) => new(false, null, null, error, null);

    /// <summary>
    /// Creates a result indicating SSR is not available for this page.
    /// </summary>
    public static ServerRenderResult NotAvailable() => new(false, null, null, "SSR not available", null);
}
