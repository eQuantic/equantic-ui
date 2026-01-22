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
    
    public PageAttribute(string route)
    {
        Route = route ?? throw new ArgumentNullException(nameof(route));
    }
}
