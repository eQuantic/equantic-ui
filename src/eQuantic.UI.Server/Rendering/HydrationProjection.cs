using System.Collections.Concurrent;
using System.Reflection;

namespace eQuantic.UI.Server.Rendering;

/// <summary>
/// A server value as the browser reads it: <c>null</c> when it is null, and otherwise a plain object
/// holding only the reads its projection lists (<see cref="Primitives.HydratedMemberAttribute.Projection"/>),
/// each under the twin's name for it.
/// <para>
/// The projection is the build's answer to "what of this does the browser read", so nothing else of the
/// value is touched: a member it does not list is never read here, let alone written into the page.
/// A read crosses the value it reaches, which the build has checked is plain data; a read ending in
/// <c>?</c> crosses only whether its value is null, as null or an empty object. A null met on the way
/// crosses as null where it was met, which is what the browser's own read would meet.
/// </para>
/// </summary>
internal static class HydrationProjection
{
    private const BindingFlags Declared =
        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

    private static readonly ConcurrentDictionary<(Type Type, string Member), (Func<object, object?> Read, Type Type)?> Members = new();

    /// <param name="value">The value the manifest's entry holds.</param>
    /// <param name="projection">The reads the browser makes of it.</param>
    /// <param name="declared">The type the entry's member is declared as, which the reads are bound against.</param>
    public static object? Of(object? value, string projection, Type declared)
    {
        if (value is null) return null;
        var projected = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (var read in projection.Split(',', StringSplitOptions.RemoveEmptyEntries))
            Write(projected, value, declared, read);
        return projected;
    }

    private static void Write(Dictionary<string, object?> into, object value, Type declared, string read)
    {
        var presence = read.EndsWith('?');
        var segments = (presence ? read[..^1] : read).Split('.');
        var target = into;
        var current = value;
        var type = declared;
        for (var i = 0; i < segments.Length; i++)
        {
            var name = TwinName.Of(segments[i]);
            var member = Member(type, segments[i]) ?? throw new InvalidOperationException(
                $"{type.FullName} has no member '{segments[i]}', which the hydration manifest reads. Rebuild "
                + "the assembly so its manifest is written again.");
            var next = member.Read(current);

            if (i == segments.Length - 1)
            {
                target[name] = !presence ? next
                    : next is null ? null
                    : target.TryGetValue(name, out var already) && already is Dictionary<string, object?> ? already
                    : new Dictionary<string, object?>(StringComparer.Ordinal);
                return;
            }
            if (next is null)
            {
                target[name] = null;
                return;
            }
            if (!target.TryGetValue(name, out var nested) || nested is not Dictionary<string, object?> child)
                target[name] = child = new Dictionary<string, object?>(StringComparer.Ordinal);
            target = child;
            current = next;
            type = member.Type;
        }
    }

    /// <summary>
    /// The member a read names, bound as C# binds it: on the type the value is declared as, then the types
    /// it derives from (or, for an interface, the interfaces it extends). The value's run-time type is not
    /// asked, so a member a derived type hides with <c>new</c> is not the one read, while a virtual one
    /// still answers with its override, and an interface's member with its implementation.
    /// </summary>
    private static (Func<object, object?> Read, Type Type)? Member(Type declared, string member) =>
        Members.GetOrAdd((declared, member), static key =>
        {
            foreach (var type in Bound(key.Type))
            {
                if (type.GetProperty(key.Member, Declared) is { CanRead: true } property
                    && property.GetIndexParameters().Length == 0)
                    return (property.GetValue, property.PropertyType);
                if (type.GetField(key.Member, Declared) is { } field)
                    return (field.GetValue, field.FieldType);
            }
            return null;
        });

    private static IEnumerable<Type> Bound(Type declared)
    {
        if (declared.IsInterface) return declared.GetInterfaces().Prepend(declared);
        var chain = new List<Type>();
        for (var type = declared; type is not null; type = type.BaseType) chain.Add(type);
        return chain;
    }
}
