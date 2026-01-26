using eQuantic.UI.Core.Theme.Types;

namespace eQuantic.UI.Core.Theme;

public interface IAvatarTheme
{
    string Root { get; }
    string Image { get; }
    string Fallback { get; }
    string GetSize(Size size);
}
