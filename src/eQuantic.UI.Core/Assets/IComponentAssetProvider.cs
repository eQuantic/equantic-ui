namespace eQuantic.UI.Core.Assets;

/// <summary>
/// External asset provider for components that don't implement IRequireAssets directly.
/// Register in DI to associate assets with third-party or framework components.
/// </summary>
/// <typeparam name="TComponent">The component type this provider supplies assets for.</typeparam>
/// <example>
/// <code>
/// // Provider declaration
/// public class ChartJsAssetProvider : IComponentAssetProvider&lt;ChartCanvas&gt;
/// {
///     public void ConfigureAssets(AssetBuilder assets)
///     {
///         assets.AddScript("https://cdn.jsdelivr.net/npm/chart.js@4.4.1/dist/chart.umd.min.js");
///     }
/// }
///
/// // Registration in DI
/// services.AddSingleton&lt;IComponentAssetProvider&lt;ChartCanvas&gt;, ChartJsAssetProvider&gt;();
/// </code>
/// </example>
public interface IComponentAssetProvider<TComponent> where TComponent : IComponent
{
    /// <summary>
    /// Declares the assets required by the target component type.
    /// </summary>
    void ConfigureAssets(AssetBuilder assets);
}
