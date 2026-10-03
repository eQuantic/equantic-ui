using System.Collections.Concurrent;
using System.Reflection;
using eQuantic.UI.Primitives;

namespace eQuantic.UI.Server.Rendering;

/// <summary>
/// What one component type carries from its server render to the browser, read from the hydration
/// manifest the source generator wrote into the component's own assembly.
/// <para>
/// It replaces a guess. The server walked a component's fields and named each one after the field the
/// C# compiler synthesized, while eqc named the twin's members by its own rule, so an auto-property
/// crossed as <c>Downloads</c> into a twin that declares <c>downloads</c>, and so did a public field
/// spelled in Pascal case. The manifest says which members cross, and every value goes out under the
/// twin's name for it (<see cref="TwinName"/>, the rule eqc and the generator share).
/// </para>
/// <para>
/// Each entry is resolved once per type to a reader. A property is read through its getter, so no
/// synthesized name is guessed; a captured primary-constructor parameter is read from the field the
/// compiler synthesizes for it, the one convention this side keeps.
/// </para>
/// </summary>
internal sealed class HydrationContract
{
    private const BindingFlags Declared =
        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

    private static readonly ConcurrentDictionary<Assembly, ILookup<string, HydratedMemberAttribute>> Manifests = new();
    private static readonly ConcurrentDictionary<Type, HydrationContract?> Contracts = new();

    private HydrationContract(IReadOnlyList<HydratedValue> values, IReadOnlyList<string> unresolved)
    {
        Values = values;
        Unresolved = unresolved;
    }

    /// <summary>The values that cross, each under the twin's name for it.</summary>
    public IReadOnlyList<HydratedValue> Values { get; }

    /// <summary>
    /// Entries the type no longer declares as the manifest says, which only a manifest out of step with
    /// the assembly that carries it can produce. They are left out and reported.
    /// </summary>
    public IReadOnlyList<string> Unresolved { get; }

    /// <summary>
    /// The contract of <paramref name="component"/>, or <c>null</c> when its assembly describes nothing
    /// for it, in which case it carries no state.
    /// </summary>
    public static HydrationContract? For(Type component) => Contracts.GetOrAdd(component, Resolve);

    private static HydrationContract? Resolve(Type component)
    {
        // The manifest names a type by its definition's metadata name, which describes every closing of
        // a generic one.
        var key = Definition(component).FullName;
        var manifest = Manifests.GetOrAdd(component.Assembly, assembly =>
            assembly.GetCustomAttributes<HydratedMemberAttribute>().ToLookup(entry => entry.Component, StringComparer.Ordinal));
        if (key is null || !manifest.Contains(key)) return null;

        var values = new List<HydratedValue>();
        var unresolved = new List<string>();
        foreach (var entry in manifest[key])
        {
            if (Reader(component, entry) is { } read)
                values.Add(new HydratedValue(TwinName.Of(entry.Member), read, entry.Projection));
            else
                unresolved.Add($"{entry.DeclaringType}.{entry.Member} ({entry.Kind})");
        }

        return new HydrationContract(values, unresolved);
    }

    private static Func<object, object?>? Reader(Type component, HydratedMemberAttribute entry)
    {
        if (Declaring(component, entry.DeclaringType) is not { } declaring) return null;
        return entry.Kind switch
        {
            HydratedMemberKind.Property =>
                declaring.GetProperty(entry.Member, Declared) is { CanRead: true } property ? property.GetValue : null,
            // THE C# COMPILER'S NAME for a captured primary-constructor parameter's storage. It is the
            // one name the server keeps, pinned by a test over a real primary constructor so a compiler
            // that names it differently fails there rather than here.
            HydratedMemberKind.CapturedParameter =>
                declaring.GetField($"<{entry.Member}>P", Declared) is { } captured ? captured.GetValue : null,
            _ => declaring.GetField(entry.Member, Declared) is { } field ? field.GetValue : null,
        };
    }

    /// <summary>The type on the component's chain the manifest names, closed as the component closes it.</summary>
    private static Type? Declaring(Type component, string declared)
    {
        for (var type = component; type is not null; type = type.BaseType)
            if (string.Equals(Definition(type).FullName, declared, StringComparison.Ordinal))
                return type;
        return null;
    }

    private static Type Definition(Type type) => type.IsGenericType ? type.GetGenericTypeDefinition() : type;
}
