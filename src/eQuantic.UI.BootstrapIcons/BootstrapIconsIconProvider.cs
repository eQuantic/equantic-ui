using System.Linq;
using System.Reflection;
using eQuantic.UI.Core;

namespace eQuantic.UI.BootstrapIcons;

public class BootstrapIconsIconProvider : IIconProvider
{
    public bool CanResolve(string name)
    {
        return GetMethod(name) != null;
    }

    public IComponent? CreateIcon(string name, int size = 24, double strokeWidth = 2, string color = "currentColor", string? className = null)
    {
        var method = GetMethod(name);
        if (method == null) return null;

        return method.Invoke(null, new object?[] { size, strokeWidth, color, className }) as IComponent;
    }

    private MethodInfo? GetMethod(string name)
    {
        var pascalName = string.Join("", name.Split(new[] { '-', '_' }, System.StringSplitOptions.RemoveEmptyEntries)
            .Select(p => char.ToUpper(p[0]) + p.Substring(1)));
        
        if (char.IsDigit(pascalName[0])) pascalName = "_" + pascalName;
        
        return typeof(BootstrapIcons).GetMethod(pascalName, new[] { typeof(int), typeof(double), typeof(string), typeof(string) });
    }
}
