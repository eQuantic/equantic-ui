using eQuantic.UI.Core.Theme.Types;

namespace eQuantic.UI.Core.Theme;

public interface ISizeTheme
{
    string GetFontSize(SizeVariant size);
    string GetPadding(SizeVariant size);
    string GetRadius(SizeVariant size);
}
