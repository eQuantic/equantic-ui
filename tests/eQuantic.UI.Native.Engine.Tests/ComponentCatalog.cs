using System.Reflection;
using eQuantic.UI.Components;
using eQuantic.UI.Primitives;
using eQuantic.UI.Web.Tests;

namespace eQuantic.UI.Native.Engine.Tests;

/// <summary>
/// Every shipped component, DERIVED from the build output rather than listed, and how a plausible instance
/// of each is built. The semantics parity fixture (#339) and the variant matrix (#1247) read the same set,
/// so a component that joins the SDK joins both with no edit to either.
/// </summary>
internal static class ComponentCatalog
{
    /// <summary>Every public, concrete component of every assembly beside the test that references the
    /// vocabulary, in the order of its full name.</summary>
    public static IReadOnlyList<Type> Components { get; } = Derive();

    private static readonly NavItem[] Destinations =
        [new(Icons.Person, "Profile"), new(Icons.Search, "Search"), new(Icons.Heart, "Saved")];

    /// <summary>
    /// The components plausible arguments cannot read, and how each is built instead: a navigation refuses
    /// an empty list of destinations, a dialog an empty row of actions, a list beside a detail a list that
    /// builds nothing; and a calendar left without a date shows the month the clock says, so what it reads
    /// would name another month every month.
    /// </summary>
    public static IReadOnlyDictionary<Type, Func<VisualNode>> Overrides { get; } = new Dictionary<Type, Func<VisualNode>>
    {
        [typeof(BottomNavigation)] = () => new BottomNavigation(Destinations, 1, _ => { }),
        [typeof(NavigationRail)] = () => new NavigationRail(Destinations, 0, _ => { }),
        [typeof(ListDetail)] = () => new ListDetail(() => new Text("Inbox"), () => new Text("Message")),
        [typeof(Dialog)] = () => Page(new Dialog("Sign out?", "You can sign back in anytime.",
            [new DialogAction("Stay", null, Variant.Ghost), new DialogAction("Sign out")])),
        [typeof(eQuantic.UI.Components.Calendar)] = () => new eQuantic.UI.Components.Calendar(new DateOnly(2026, 7, 17), _ => { }),
    };

    /// <summary>A layer over the page it opens on, as a dialog is shown.</summary>
    public static VisualNode Page(VisualNode layer)
    {
        var page = new Column(gap: Space.S2) { Width = SizeValue.Fill };
        page.Add(new Text("Cards", TypeRole.Title));
        page.Add(layer);
        return page;
    }

    /// <summary>
    /// The constructor a component is authored through, its widest (the one its factory mirrors), and
    /// plausible arguments for it; or why there are none.
    /// </summary>
    public static bool TryWidest(Type type, out ConstructorInfo? constructor, out object?[] arguments, out string why)
    {
        constructor = type.GetConstructors().MaxBy(candidate => candidate.GetParameters().Length);
        arguments = [];
        if (constructor is null)
        {
            why = "no public constructor to build it with";
            return false;
        }
        return FactoryArguments.For(constructor.GetParameters(), out arguments, out why);
    }

    private static IReadOnlyList<Type> Derive()
    {
        var vocabulary = typeof(VisualNode).Assembly.GetName().Name;
        var types = new List<Type>();
        foreach (var path in Directory.EnumerateFiles(AppContext.BaseDirectory, "eQuantic.*.dll"))
        {
            if (Path.GetFileNameWithoutExtension(path).EndsWith(".Tests", StringComparison.Ordinal)) continue;
            Assembly assembly;
            try { assembly = Assembly.LoadFrom(path); }
            catch (BadImageFormatException) { continue; }
            catch (FileLoadException) { continue; }
            if (assembly.GetReferencedAssemblies().All(reference => reference.Name != vocabulary)) continue;
            Type[] exported;
            try { exported = assembly.GetExportedTypes(); }
            catch (Exception) { continue; }
            types.AddRange(exported.Where(type => typeof(UiComponent).IsAssignableFrom(type)
                && type is { IsAbstract: false, ContainsGenericParameters: false }));
        }
        return types.Distinct().OrderBy(type => type.FullName, StringComparer.Ordinal).ToList();
    }
}
