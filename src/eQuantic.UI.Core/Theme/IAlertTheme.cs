using eQuantic.UI.Core.Theme.Types;

namespace eQuantic.UI.Core.Theme;

public interface IAlertTheme
{
    string Base { get; }
    string Icon { get; }
    string Title { get; }
    string Description { get; }
    string GetVariant(Variant variant);
}
