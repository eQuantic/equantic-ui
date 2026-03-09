using eQuantic.UI.Core.Theme;

namespace eQuantic.UI.Tailwind.Theme;

public class DrawerTheme : IDrawerTheme
{
    public string Backdrop => "fixed inset-0 z-50 bg-black/40 backdrop-blur-sm animate-in fade-in duration-300";
    public string Panel => "fixed z-50 bg-background shadow-2xl animate-in duration-300 border-border flex flex-col gap-6 p-6 overflow-y-auto";
}
