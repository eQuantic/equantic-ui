using eQuantic.UI.Primitives;

namespace eQuantic.UI.Native.Components;

/// <summary>
/// Which listeners hear a pointer (#1562): Flutter's <c>HitTestBehavior</c> rule, over what lies under the point.
/// <para>
/// Flutter tests a listener's child first, then itself when it is opaque, adds itself when either hit or it is
/// translucent, and returns whether it was hit; a stack stops at its first child that returns true. Over the
/// entries under the point, in paint order, that rule is:
/// </para>
/// <list type="bullet">
/// <item>a listener's child is hit when a solid inside it is under the point, or an <c>Opaque</c> listener is;</item>
/// <item>an entry returns true when it is a solid, or a listener that is <c>Opaque</c> or has its child hit;</item>
/// <item>a listener is reached when nothing painted above it, outside its own subtree, returns true;</item>
/// <item>a listener hears when it is reached, and its child is hit or it is not <c>DeferToChild</c>;</item>
/// <item>the listeners that hear are told innermost and topmost first: the latest painted first.</item>
/// </list>
/// <para>
/// The web's runtime writes the same rule (<c>listenerHits</c>, <c>dom/listener-hits.ts</c>), and both run the
/// cases of <c>listener-hits.fixture.json</c>, so the two writings cannot drift.
/// </para>
/// </summary>
internal static class ListenerHits
{
    /// <summary>One entry under the point.</summary>
    /// <param name="Start">Its place in paint order.</param>
    /// <param name="End">The place past its subtree: the entries between are inside it.</param>
    /// <param name="Solid">Whether it draws or answers.</param>
    /// <param name="Behavior">Its behaviour when it is a listener, or null.</param>
    internal readonly record struct Under(int Start, int End, bool Solid, HitTestBehavior? Behavior);

    /// <summary>The listeners of <paramref name="under"/> (in paint order) that hear, as indices into it, the
    /// latest painted first.</summary>
    internal static List<int> Hear(IReadOnlyList<Under> under)
    {
        var heard = new List<int>();
        for (var i = under.Count - 1; i >= 0; i--)
        {
            if (under[i].Behavior is not { } behavior) continue;
            var childHit = ChildHit(under, i);
            if (!Reached(under, i)) continue;
            if (childHit || behavior != HitTestBehavior.DeferToChild) heard.Add(i);
        }
        return heard;
    }

    private static bool Inside(Under outer, Under entry) => entry.Start > outer.Start && entry.Start < outer.End;

    private static bool ChildHit(IReadOnlyList<Under> under, int listener)
    {
        for (var j = 0; j < under.Count; j++)
        {
            if (!Inside(under[listener], under[j])) continue;
            if (under[j].Solid || under[j].Behavior == HitTestBehavior.Opaque) return true;
        }
        return false;
    }

    private static bool Returns(IReadOnlyList<Under> under, int entry) =>
        under[entry].Solid
        || under[entry].Behavior == HitTestBehavior.Opaque
        || (under[entry].Behavior is not null && ChildHit(under, entry));

    private static bool Reached(IReadOnlyList<Under> under, int listener)
    {
        for (var j = 0; j < under.Count; j++)
        {
            if (under[j].Start < under[listener].End) continue;
            if (Returns(under, j)) return false;
        }
        return true;
    }
}
