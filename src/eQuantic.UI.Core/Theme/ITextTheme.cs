namespace eQuantic.UI.Core.Theme;

public interface ITextTheme
{
    string Base { get; }
    string GetVariant(string variant);
}
