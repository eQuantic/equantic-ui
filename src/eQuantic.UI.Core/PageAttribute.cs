using System;

namespace eQuantic.UI.Core;

/// <summary>
/// Marks a component as a routable page.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public class PageAttribute : Attribute
{
    /// <summary>
    /// The route pattern for this page (e.g., "/counter", "/user/{id:int}").
    /// </summary>
    public string Route { get; }

    /// <summary>
    /// Optional page title for metadata.
    /// </summary>
    public string? Title { get; set; }

    /// <summary>
    /// Optional meta description for SEO.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Disables Server-Side Rendering for this page.
    /// Set to true for pages that require client-side data or authentication.
    /// </summary>
    /// <remarks>
    /// When SSR is disabled, the page will render a loading placeholder
    /// and hydrate entirely on the client side.
    /// </remarks>
    public bool DisableSsr { get; set; }

    /// <summary>
    /// Cache duration in seconds for SSR output.
    /// Set to 0 to disable caching (default).
    /// </summary>
    public int SsrCacheDuration { get; set; }

    public PageAttribute(string route)
    {
        Route = route ?? throw new ArgumentNullException(nameof(route));
    }
}
