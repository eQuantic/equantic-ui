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

    /// <summary>Whether one of them is a server value crossing as its projection.</summary>
    public bool HasProjection => Values.Any(value => value.Projection is not null);

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
            if (Reader(component, entry) is var (read, declared) && read is not null)
                values.Add(new HydratedValue(NameOf(entry), read, entry.Projection, declared!));
            else
                unresolved.Add($"{entry.DeclaringType}.{entry.Member} ({entry.Kind})");
        }

        return new HydrationContract(values, unresolved);
    }

    /// <summary>
    /// The name a value crosses under: the twin's name for the member, or for a property that keeps its
    /// store through <c>field</c>, the slot the twin keeps that store in.
    /// </summary>
    private static string NameOf(HydratedMemberAttribute entry) => entry.Kind == HydratedMemberKind.BackingField
        ? TwinName.BackingSlot(entry.Member)
        : TwinName.Of(entry.Member);

    /// <summary>How to read an entry off the live component, and the type the member is declared as.</summary>
    private static (Func<object, object?>? Read, Type? Declared) Reader(Type component, HydratedMemberAttribute entry)
    {
        if (Declaring(component, entry.DeclaringType) is not { } declaring) return (null, null);
        var field = entry.Kind switch
        {
            HydratedMemberKind.Property => null,
            // THE C# COMPILER'S NAMES for the storage it synthesizes: a captured primary-constructor
            // parameter's, and the store of a property whose accessors use `field`. They are the names the
            // server keeps, each pinned by a test over what the compiler built, so a compiler that names
            // them differently fails there rather than here.
            HydratedMemberKind.CapturedParameter => declaring.GetField($"<{entry.Member}>P", Declared),
            HydratedMemberKind.BackingField => declaring.GetField($"<{entry.Member}>k__BackingField", Declared),
            _ => declaring.GetField(entry.Member, Declared),
        };
        if (field is not null) return (field.GetValue, field.FieldType);
        return entry.Kind == HydratedMemberKind.Property
            && declaring.GetProperty(entry.Member, Declared) is { CanRead: true } property
            ? (property.GetValue, property.PropertyType)
            : (null, null);
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
