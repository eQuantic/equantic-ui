using System.Reflection;

namespace eQuantic.UI.Server.Rendering;

/// <summary>
/// The comparer a collection written whole holds, when the browser's copy would not keep it. The wire
/// carries elements and never a comparer, and the browser rebuilds a set or a dictionary with its
/// default equality and a sorted collection with its default order: a string by its code units, a
/// number by its value. So a <c>HashSet&lt;string&gt;</c> made with <c>StringComparer.OrdinalIgnoreCase</c>
/// answered <c>Contains("ADMIN")</c> true on the server and false in the browser, in silence. Such a
/// value does not cross: the payload leaves it out, and the log names it.
/// </summary>
internal static class ForeignComparer
{
    /// <summary>The comparer <paramref name="value"/> holds, when it is neither its element type's
    /// default nor, for a string, the ordinal one; null otherwise, and for a value with no comparer.</summary>
    public static object? Of(object value)
    {
        if (value.GetType().GetProperty("Comparer", BindingFlags.Public | BindingFlags.Instance) is not { } property
            || property.PropertyType is not { IsGenericType: true } shape
            || property.GetValue(value) is not { } comparer)
            return null;
        var element = shape.GetGenericArguments()[0];
        var definition = shape.GetGenericTypeDefinition();
        var holder = definition == typeof(IEqualityComparer<>) ? typeof(EqualityComparer<>)
            : definition == typeof(IComparer<>) ? typeof(Comparer<>)
            : null;
        if (holder is null) return null;
        var standard = holder.MakeGenericType(element).GetProperty("Default", BindingFlags.Public | BindingFlags.Static)!.GetValue(null);
        return ReferenceEquals(comparer, standard)
            || element == typeof(string) && ReferenceEquals(comparer, StringComparer.Ordinal)
            ? null
            : comparer;
    }
}
