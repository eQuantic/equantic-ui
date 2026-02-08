using System.Reflection;
using eQuantic.UI.Core;

namespace eQuantic.UI.Lucide;

public class LucideIconProvider : IIconProvider
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
        var pascalName = string.Join("", name.Split('-').Select(p => char.ToUpper(p[0]) + p.Substring(1)));
        return typeof(LucideIcons).GetMethod(pascalName, new[] { typeof(int), typeof(double), typeof(string), typeof(string) });
    }
}
