using System.Collections.Generic;
using System.Linq;

namespace eQuantic.UI.Core.Assets;

/// <summary>
/// Collects and deduplicates asset dependencies from components.
/// </summary>
public class AssetCollection
{
    private readonly Dictionary<string, IAsset> _assets = new();

    /// <summary>
    /// Adds an asset if not already present (deduplication by Key).
    /// </summary>
    public void Add(IAsset asset)
    {
        _assets.TryAdd(asset.Key, asset);
    }

    /// <summary>
    /// All collected assets.
    /// </summary>
    public IEnumerable<IAsset> Assets => _assets.Values;

    /// <summary>
    /// Whether the collection has any assets.
    /// </summary>
    public bool HasAssets => _assets.Count > 0;

    /// <summary>
    /// Renders all assets as HTML tags, ordered: stylesheets first, then scripts.
    /// </summary>
    public IEnumerable<string> RenderTags()
    {
        // CSS before JS for proper render-blocking order
        foreach (var asset in _assets.Values.OfType<StylesheetAsset>())
            yield return asset.Render();
        foreach (var asset in _assets.Values.OfType<InlineStyleAsset>())
            yield return asset.Render();
        foreach (var asset in _assets.Values.OfType<ScriptAsset>())
            yield return asset.Render();
        foreach (var asset in _assets.Values.OfType<InlineScriptAsset>())
            yield return asset.Render();
    }
}
