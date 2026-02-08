namespace eQuantic.UI.Core.Assets;

/// <summary>
/// Fluent builder for declaring asset dependencies within a component.
/// </summary>
public class AssetBuilder
{
    private readonly AssetCollection _collection;

    public AssetBuilder(AssetCollection collection) => _collection = collection;

    /// <summary>
    /// Adds an external script (CDN or local path).
    /// </summary>
    public AssetBuilder AddScript(string src, bool defer = true)
    {
        _collection.Add(new ScriptAsset(src, defer));
        return this;
    }

    /// <summary>
    /// Adds an inline script block.
    /// </summary>
    public AssetBuilder AddInlineScript(string content)
    {
        _collection.Add(new InlineScriptAsset(content));
        return this;
    }

    /// <summary>
    /// Adds an external stylesheet (CDN or local path).
    /// </summary>
    public AssetBuilder AddStylesheet(string href)
    {
        _collection.Add(new StylesheetAsset(href));
        return this;
    }

    /// <summary>
    /// Adds an inline style block.
    /// </summary>
    public AssetBuilder AddInlineStyle(string content)
    {
        _collection.Add(new InlineStyleAsset(content));
        return this;
    }
}
