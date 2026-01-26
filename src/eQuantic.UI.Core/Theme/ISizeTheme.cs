using eQuantic.UI.Core.Theme.Types;

namespace eQuantic.UI.Core.Theme;

public interface ISizeTheme
{
    string GetFontSize(Size size);
    string GetPadding(Size size);
    string GetRadius(Size size);
}
