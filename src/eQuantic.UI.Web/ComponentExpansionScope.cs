using System.Reflection;
using eQuantic.UI.Primitives;

namespace eQuantic.UI.Web;

/// <summary>
/// NAMES EVERY COMPONENT THE PAGE EXPANDS, so server data loaded for one of them can be handed back
/// to the same one on the client.
///
/// <para>
/// Only the root of a route used to prefetch, and only the root's fields travelled — a component
/// the page merely composed had <c>PrefetchAsync</c> called never. Fixing that needs an identity
/// that survives the SSR/hydration boundary, because the client does not receive components: it
/// re-runs <c>build()</c> and constructs fresh ones. The payload has to say WHICH component each
/// field map belongs to.
/// </para>
///
/// <para>
/// THE NAME IS <c>Type#ordinal</c> in depth-first expansion order, and the alternative is worth
/// stating because it was the first design. <c>lowering.ts</c> already carries a structural
/// <c>path</c> (<c>r/0/0</c>) for Photon's retained store, and the matching key would have been
/// that path — except the C# web realizer has no path at all, and threading one through every visit
/// is a large mechanical change across a file family whose output is pinned byte for byte. The
/// ordinal is the same guarantee for less: both sides expand the same tree in the same order, so
/// both count the same.
/// </para>
///
/// <para>
/// THE TYPE NAME IS NOT DECORATION — it is what makes a drift safe. If the two sides ever expand
/// different trees, an ordinal alone would hand a component the state of whatever happened to sit
/// at that number; carrying the type means the client can refuse a key whose type disagrees and
/// leave the component with its own defaults, which is a missing value rather than a wrong one.
/// </para>
/// </summary>
public sealed class ComponentExpansionScope
{
    private static readonly System.Threading.AsyncLocal<ComponentExpansionScope?> Current = new();

    private readonly Dictionary<string, int> _ordinals = new(StringComparer.Ordinal);
    private readonly Dictionary<string, UiComponent> _expanded = new(StringComparer.Ordinal);

    /// <summary>
    /// The render-scoped ambient scope, armed by the SSR pipeline around an expansion and null
    /// everywhere else — the same shape <see cref="StyleSink.Ambient"/> uses, and for the same
    /// reason: a component deep in the tree contributes to one per-page collection without every
    /// visit signature growing a parameter.
    /// </summary>
    public static ComponentExpansionScope? Ambient
    {
        get => Current.Value;
        set => Current.Value = value;
    }

    /// <summary>State to APPLY as each component is reached, keyed as above. The server's discovery
    /// loop fills this from the previous round, because a rebuild constructs new instances and the
    /// values a prefetch loaded live on the old ones.</summary>
    public IReadOnlyDictionary<string, IReadOnlyDictionary<string, object?>> Restore { get; init; }
        = new Dictionary<string, IReadOnlyDictionary<string, object?>>(StringComparer.Ordinal);

    /// <summary>Every component reached this round, in expansion order.</summary>
    public IReadOnlyDictionary<string, UiComponent> Expanded => _expanded;

    /// <summary>
    /// Names a component the realizer is about to expand and restores anything already loaded for
    /// it — BEFORE its <c>Build</c> runs, which is the whole point of hooking the expansion rather
    /// than the render.
    /// </summary>
    /// <summary>
    /// A component's fields AS THEY ARE — raw CLR values under raw field names, nulls included.
    ///
    /// <para>
    /// Deliberately NOT the payload's snapshot, and the difference cost a round. The payload is a
    /// WIRE form: it renames an auto-property's backing field to the property, writes an enum as the
    /// camelCase string the client expects, and drops nulls so one unwritable value cannot empty the
    /// page. Every one of those is wrong for restoring a value onto a fresh C# instance — the
    /// property name matches no field, the string is not assignable to the enum, and a prefetch that
    /// CLEARED a non-null default would silently keep the default. Round-to-round restoration reads
    /// this; only the response is normalized.
    /// </para>
    /// </summary>
    public static IReadOnlyDictionary<string, object?> Capture(UiComponent component)
    {
        var captured = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (var field in FieldsOf(component.GetType()))
        {
            captured[field.Name] = field.GetValue(component);
        }
        return captured;
    }

    public string Enter(UiComponent component)
    {
        var typeName = component.GetType().Name;
        var ordinal = _ordinals.TryGetValue(typeName, out var seen) ? seen : 0;
        _ordinals[typeName] = ordinal + 1;

        var key = $"{typeName}#{ordinal}";
        _expanded[key] = component;

        if (Restore.TryGetValue(key, out var fields)) Apply(component, fields);
        return key;
    }

    /// <summary>
    /// Writes a field map back onto a component. By NAME against the declared fields of the type
    /// and its bases, so a value loaded in one round survives into the next one's fresh instance.
    /// A name the type does not declare is skipped rather than thrown on: the round that wrote it
    /// may have expanded a different tree, and a missing value is recoverable where a throw is not.
    /// </summary>
    private static void Apply(UiComponent component, IReadOnlyDictionary<string, object?> fields)
    {
        foreach (var field in FieldsOf(component.GetType()))
        {
            if (!fields.TryGetValue(field.Name, out var value)) continue;
            // A null is written like any other value, because CLEARING a non-null default is
            // something a prefetch legitimately does and skipping it would silently keep the
            // default. The type check still stands for anything that is not null.
            //
            // A Nullable<T> needs nothing special here, which is worth stating because it looks as
            // though it should: GetValue really does box a non-null `long?` as a `System.Int64`,
            // but `IsInstanceOfType` special-cases Nullable and answers True for that box.
            // Measured — `typeof(long?).IsInstanceOfType(42L)` is True — so unwrapping the
            // underlying type here would be a line that reads like a fix for a defect there is no
            // evidence of.
            if (value is not null && !field.FieldType.IsInstanceOfType(value)) continue;
            field.SetValue(component, value);
        }
    }

    /// <summary>
    /// Every instance field a type holds, ITS BASES INCLUDED. <c>GetFields</c> alone does not return
    /// a base type's PRIVATE fields, so a page that kept its loaded value in a private field on a
    /// base class would have had it silently dropped — measured, and the second half of the defect
    /// this change fixes. The derived declaration wins when a name is shadowed, which is the order
    /// C# itself resolves.
    /// </summary>
    public static IEnumerable<FieldInfo> FieldsOf(Type type)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        for (var current = type; current is not null && current != typeof(object); current = current.BaseType)
        {
            foreach (var field in current.GetFields(
                BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.DeclaredOnly))
            {
                if (seen.Add(field.Name)) yield return field;
            }
        }
    }
}
