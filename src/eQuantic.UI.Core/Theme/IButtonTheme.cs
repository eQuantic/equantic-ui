using eQuantic.UI.Core.Theme.Types;

namespace eQuantic.UI.Core.Theme;

public interface IButtonTheme
{
    string Base { get; }
    string GetVariant(Variant variant);
    string GetSize(Size size);
}
