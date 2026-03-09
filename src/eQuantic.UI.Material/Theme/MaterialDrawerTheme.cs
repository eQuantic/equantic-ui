using eQuantic.UI.Core.Theme;

namespace eQuantic.UI.Material.Theme;

public class MaterialDrawerTheme : IDrawerTheme
{
    public string Backdrop => "fixed inset-0 z-50 bg-gray-900/50 backdrop-blur-sm transition-opacity duration-300";
    public string Panel => "fixed z-50 bg-white shadow-2xl transition-transform duration-300 flex flex-col gap-6 p-6 overflow-y-auto dark:bg-gray-900";
}
