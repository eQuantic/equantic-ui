using eQuantic.UI.Core.Theme.Types;

namespace eQuantic.UI.Core.Theme;

public interface IInputTheme
{
    string Base { get; }
    string GetSize(Size size);
}
