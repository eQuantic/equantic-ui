using eQuantic.UI.Core.Theme.Types;

namespace eQuantic.UI.Core.Theme;

public interface ITextTheme
{
    string Base { get; }
    string GetVariant(Variant variant);
    string GetHeading(int level);
}
