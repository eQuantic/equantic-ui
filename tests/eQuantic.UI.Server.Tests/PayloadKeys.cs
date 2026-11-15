using System.Text.Json;
using FluentAssertions;

namespace eQuantic.UI.Server.Tests;

/// <summary>
/// The key a hydration payload names one component's data by. A page's own component is
/// <c>Type#0</c>; every component it composes is named by where it stands (#1477),
/// <c>r{tree}/{type}@{slots}</c>, which a test reads by the component's type rather than spelling the
/// slots of a page whose shape it does not mean to pin.
/// </summary>
internal static class PayloadKeys
{
    /// <summary>The one key naming a composed component of type <typeparamref name="T"/>.</summary>
    public static string Of<T>(JsonElement payload) => Of(payload, typeof(T));

    /// <summary>The one key naming a composed component of <paramref name="type"/>.</summary>
    public static string Of(JsonElement payload, Type type)
    {
        var keys = All(payload, type);
        keys.Should().ContainSingle($"one {type.Name} stands in the page; the payload named: {payload}");
        return keys[0];
    }

    /// <summary>Every key naming a composed component of <paramref name="type"/>, in the payload's order.</summary>
    public static IReadOnlyList<string> All(JsonElement payload, Type type)
    {
        var identity = Web.ComponentIdentity.Of(type);
        return payload.EnumerateObject()
            .Select(property => property.Name)
            .Where(name => name.Contains($"/{identity}@", StringComparison.Ordinal))
            .ToList();
    }

    /// <summary>The data the payload carries for the one composed component of <paramref name="type"/>, when it
    /// carries any.</summary>
    public static bool TryGet(JsonElement payload, Type type, out JsonElement value)
    {
        var keys = All(payload, type);
        if (keys.Count == 0)
        {
            value = default;
            return false;
        }

        keys.Should().ContainSingle($"one {type.Name} stands in the page; the payload named: {payload}");
        value = payload.GetProperty(keys[0]);
        return true;
    }

    /// <summary>The data the payload carries for the one composed component of <paramref name="type"/>.</summary>
    public static JsonElement Get(JsonElement payload, Type type) => payload.GetProperty(Of(payload, type));

    /// <summary>Whether the payload names any composed component of <paramref name="type"/>.</summary>
    public static bool Names(JsonElement payload, Type type) => All(payload, type).Count > 0;
}
