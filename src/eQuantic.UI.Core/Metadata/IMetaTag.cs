namespace eQuantic.UI.Core.Metadata;

public interface IMetaTag
{
    string Render();
    string Key { get; }
}

public abstract class MetaTag : IMetaTag
{
    public abstract string Key { get; }
    public abstract string Render();
}
