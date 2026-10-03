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

    private static readonly ConcurrentDictionary<(Type Type, string Member), Func<object, object?>?> Readers = new();

    public static object? Of(object? value, string projection)
    {
        if (value is null) return null;
        var projected = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (var read in projection.Split(',', StringSplitOptions.RemoveEmptyEntries))
            Write(projected, value, read);
        return projected;
    }

    private static void Write(Dictionary<string, object?> into, object value, string read)
    {
        var presence = read.EndsWith('?');
        var segments = (presence ? read[..^1] : read).Split('.');
        var target = into;
        var current = value;
        for (var i = 0; i < segments.Length; i++)
        {
            var name = TwinName.Of(segments[i]);
            var next = Reader(current.GetType(), segments[i]) is { } reader
                ? reader(current)
                : throw new InvalidOperationException(
                    $"{current.GetType().FullName} has no member '{segments[i]}', which the hydration manifest "
                    + "reads. Rebuild the assembly so its manifest is written again.");

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
        }
    }

    /// <summary>The member a read names, as C# declares it on the value's type or one it derives from.</summary>
    private static Func<object, object?>? Reader(Type type, string member) =>
        Readers.GetOrAdd((type, member), static key =>
        {
            for (var declaring = key.Type; declaring is not null; declaring = declaring.BaseType)
            {
                if (declaring.GetProperty(key.Member, Declared) is { CanRead: true } property
                    && property.GetIndexParameters().Length == 0)
                    return property.GetValue;
                if (declaring.GetField(key.Member, Declared) is { } field)
                    return field.GetValue;
            }
            return null;
        });
}
