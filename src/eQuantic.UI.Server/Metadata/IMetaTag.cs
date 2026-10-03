namespace eQuantic.UI.Server.Metadata;

public interface IMetaTag
{
    string Render();
    string Key { get; }
}

public abstract class MetaTag : IMetaTag
{
    /// <summary>
    /// The attribute every tag a document's metadata writes carries, so a client navigation can tell
    /// them from the head's own tags (the viewport, the theme colour, the app's scripts): it removes
    /// the marked set the previous page left and writes the next page's in its place. Patched by
    /// name alone, a tag the next page does not have stayed, a description or a canonical included.
    /// </summary>
    internal const string Managed = "data-eq-meta";

    public abstract string Key { get; }
    public abstract string Render();
}
