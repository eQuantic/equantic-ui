using System.Collections.Concurrent;
using System.Collections.Frozen;
using System.Collections.Immutable;
using System.Collections.ObjectModel;
using System.Reflection;

namespace eQuantic.UI.Server.Rendering;

/// <summary>
/// What makes a set or a dictionary written whole answer differently in the browser: a comparer its
/// copy there would not keep. The wire carries elements and never a comparer, and the browser rebuilds
/// a set or a dictionary with its element type's default equality, and a sorted one in its element
/// type's default order. So a <c>HashSet&lt;string&gt;</c> made with
/// <c>StringComparer.OrdinalIgnoreCase</c> answered <c>Contains("ADMIN")</c> true on the server and
/// false in the browser, in silence. Such a value does not cross.
/// <para>
/// The comparer is read from the collections whose comparer can be read, each by the member that
/// holds it, and a wrapper is read through what it wraps. A set or a dictionary of any other class,
/// in a member the browser rebuilds as a set or a dictionary, has an equality nothing here can see, so
/// it does not cross either. Anything else, a sequence or a plain object included, carries no comparer.
/// </para>
/// </summary>
internal static class ForeignComparer
{
    /// <summary>The collections whose comparer can be read, by the member holding it, and whether it
    /// orders (an <c>IComparer</c>) or only finds (an <c>IEqualityComparer</c>).</summary>
    private static readonly Dictionary<Type, (string Member, bool Orders)> Readable = new()
    {
        [typeof(HashSet<>)] = ("Comparer", false),
        [typeof(Dictionary<,>)] = ("Comparer", false),
        [typeof(ConcurrentDictionary<,>)] = ("Comparer", false),
        [typeof(FrozenSet<>)] = ("Comparer", false),
        [typeof(FrozenDictionary<,>)] = ("Comparer", false),
        [typeof(ImmutableHashSet<>)] = ("KeyComparer", false),
        [typeof(ImmutableDictionary<,>)] = ("KeyComparer", false),
        [typeof(SortedSet<>)] = ("Comparer", true),
        [typeof(SortedDictionary<,>)] = ("Comparer", true),
        [typeof(SortedList<,>)] = ("Comparer", true),
        [typeof(ImmutableSortedSet<>)] = ("KeyComparer", true),
        [typeof(ImmutableSortedDictionary<,>)] = ("KeyComparer", true),
    };

    /// <summary>The wrappers that answer with the collection they wrap, by the protected member
    /// holding it.</summary>
    private static readonly Dictionary<Type, string> Wrappers = new()
    {
        [typeof(ReadOnlyDictionary<,>)] = "Dictionary",
        [typeof(ReadOnlySet<>)] = "Set",
    };

    /// <summary>The members the browser rebuilds as a set or a dictionary, by their definition.</summary>
    private static readonly HashSet<Type> RebuiltAsSetOrDictionary =
    [
        typeof(HashSet<>), typeof(ISet<>), typeof(IReadOnlySet<>), typeof(SortedSet<>),
        typeof(Dictionary<,>), typeof(IDictionary<,>), typeof(IReadOnlyDictionary<,>),
        typeof(SortedDictionary<,>), typeof(SortedList<,>),
    ];

    /// <summary>
    /// Why <paramref name="value"/>, held by a member declared as <paramref name="declared"/>, would
    /// answer differently in the browser, as a phrase naming it with the comparer it holds or saying that
    /// its equality cannot be read. Null when it would answer the same, which is every value that is no
    /// set or dictionary.
    /// </summary>
    public static string? Of(object value, Type declared)
    {
        for (var type = value.GetType(); type is not null; type = type.BaseType)
        {
            if (!type.IsGenericType) continue;
            var definition = type.GetGenericTypeDefinition();
            if (Wrappers.TryGetValue(definition, out var wrapped))
                return type.GetProperty(wrapped, BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(value) is { } inner
                    ? Of(inner, declared)
                    : null;
            if (Readable.TryGetValue(definition, out var readable))
                return Foreign(type.GetProperty(readable.Member, BindingFlags.Instance | BindingFlags.Public)!, value, readable.Orders)
                    is { } comparer
                    ? $"a {Named(value.GetType())} with a {Named(comparer.GetType())}"
                    : null;
        }
        var shape = Nullable.GetUnderlyingType(declared) ?? declared;
        return shape.IsGenericType && RebuiltAsSetOrDictionary.Contains(shape.GetGenericTypeDefinition())
            ? $"a {Named(value.GetType())}, whose equality cannot be read"
            : null;
    }

    /// <summary>A type's name as C# writes it bare, without the arity a generic one carries.</summary>
    private static string Named(Type type) =>
        type.Name.IndexOf('`') is var tick and >= 0 ? type.Name[..tick] : type.Name;

    /// <summary>
    /// The comparer a readable collection holds, when the browser's copy would not keep it.
    /// A set or a dictionary that only finds keeps its element type's default equality, which for a
    /// string is the ordinal one. A sorted one keeps its element type's default order and no other:
    /// for a string that order is the current culture's, so an ordinal one is foreign there.
    /// </summary>
    private static object? Foreign(PropertyInfo member, object value, bool orders)
    {
        if (member.GetValue(value) is not { } comparer) return null;
        var element = member.PropertyType.GetGenericArguments()[0];
        var standard = (orders ? typeof(Comparer<>) : typeof(EqualityComparer<>)).MakeGenericType(element)
            .GetProperty("Default", BindingFlags.Public | BindingFlags.Static)!.GetValue(null);
        return ReferenceEquals(comparer, standard)
            || !orders && element == typeof(string) && ReferenceEquals(comparer, StringComparer.Ordinal)
            ? null
            : comparer;
    }
}
