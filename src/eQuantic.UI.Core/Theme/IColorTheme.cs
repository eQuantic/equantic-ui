using eQuantic.UI.Core.Theme.Types;

namespace eQuantic.UI.Core.Theme;

public interface IColorTheme
{
    ThemeColor Primary { get; }
    ThemeColor Secondary { get; }
    ThemeColor Destructive { get; }
    ThemeColor Muted { get; }
    ThemeColor Accent { get; }
    ThemeColor Border { get; }
    ThemeColor Input { get; }
    ThemeColor Ring { get; }
    ThemeColor Background { get; }
    ThemeColor Foreground { get; }
}
