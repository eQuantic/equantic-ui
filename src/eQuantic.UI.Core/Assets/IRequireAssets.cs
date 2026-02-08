namespace eQuantic.UI.Core.Assets;

/// <summary>
/// Interface for components that require external assets (scripts, stylesheets).
/// When a component implements this interface, the SSR pipeline automatically
/// collects and injects the declared assets into the page's &lt;head&gt;.
/// </summary>
/// <example>
/// <code>
/// public class CodeBlock : StatelessComponent, IRequireAssets
/// {
///     public void ConfigureAssets(AssetBuilder assets)
///     {
///         assets.AddStylesheet("https://cdn.example.com/highlight.css");
///         assets.AddScript("https://cdn.example.com/highlight.js");
///     }
/// }
/// </code>
/// </example>
public interface IRequireAssets
{
    /// <summary>
    /// Declares the assets required by this component.
    /// Called once per component type per page render (deduplicated).
    /// </summary>
    void ConfigureAssets(AssetBuilder assets);
}
