using eQuantic.UI.Core.Theme.Types;

namespace eQuantic.UI.Core.Theme;

public interface IBadgeTheme
{
    string Base { get; }
    string GetVariant(Variant variant);
}
