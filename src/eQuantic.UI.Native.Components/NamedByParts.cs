using eQuantic.UI.Native.Framework;
using eQuantic.UI.Primitives;

namespace eQuantic.UI.Native.Components;

/// <summary>
/// Which nameable each <see cref="LabelPart"/> and <see cref="DescriptionPart"/> of a frame reaches, and the words each
/// nameable is named and described by (#1916; the web's twins: <c>NamingParts</c> and <c>naming-parts.ts</c>, whose
/// summary says why the rule is what it is).
///
/// <para>
/// The same rule, over the laid-out tree instead of the lowering: a part reaches the first ancestor that IS a nameable,
/// or that HOLDS a nameable control no nearer part already reached. The pass keeps the parts that reached nothing yet
/// and the nameable controls it met, in reading order, and each node takes a mark of both before its children: what lies
/// past the mark after them is its subtree's. A node holding two candidate controls refuses, in the web's words.
/// </para>
///
/// <para>
/// Photon lays each layer out under a root of its own, so the frame's pass reads the page, then each layer the page did
/// not read in place: a modal, open overlay's root is the dialog it names (<see cref="SemanticsVisitor.AnnounceOverlay"/>),
/// and an anchored panel is read where its anchor stands, its <see cref="Positioned"/> part the dialog when the panel is
/// one, as the semantics walk reads both. Any other layer is a boundary: a part inside it names nothing.
/// </para>
/// </summary>
internal sealed class NamedByParts
{
    private readonly Dictionary<LayoutNode, Names> _named = new(ReferenceEqualityComparer.Instance);
    private readonly List<Pending> _pending = [];
    private readonly List<Candidate> _controls = [];
    private readonly IReadOnlyDictionary<Element, LayoutNode> _panels;

    private NamedByParts(IReadOnlyDictionary<Element, LayoutNode> panels) => _panels = panels;

    /// <summary>The parts of <paramref name="frame"/>, settled: the page, then each layer it did not read in place.</summary>
    internal static NamedByParts Of(RealizeResult frame, IReadOnlyDictionary<Element, LayoutNode> panels)
    {
        var parts = new NamedByParts(panels);
        var readInPlace = new HashSet<LayoutNode>(panels.Values, ReferenceEqualityComparer.Instance);
        parts.Walk(frame.Root);
        for (var i = 0; i < frame.OverlayRoots.Count; i++)
        {
            var root = frame.OverlayRoots[i];
            if (readInPlace.Contains(root)) continue;
            var mark = parts.Begin();
            parts.Walk(root);
            if (i < frame.OverlayLayers.Count && frame.OverlayLayers[i] is { Modal: true, Open: true })
                parts.Container(mark, root);
            else
                parts.Boundary(mark);
        }
        return parts;
    }

    /// <summary>What the parts that reach <paramref name="nameable"/> say, joined in reading order, or null when no part
    /// reaches it or they say nothing: the nameable is then named by its own string, as the browser falls through an
    /// empty <c>aria-labelledby</c> to <c>aria-label</c>.</summary>
    internal string? LabelOf(LayoutNode nameable) =>
        _named.TryGetValue(nameable, out var names) ? Joined(names.Labels) : null;

    /// <summary>What the parts that describe <paramref name="nameable"/> say, joined in reading order, or null.</summary>
    internal string? DescriptionOf(LayoutNode nameable) =>
        _named.TryGetValue(nameable, out var names) ? Joined(names.Descriptions) : null;

    private static string? Joined(List<string> words) =>
        words.Count == 0 ? null : string.Join(" ", words) is { Length: > 0 } joined ? joined : null;

    private void Walk(LayoutNode node)
    {
        var mark = Begin();
        if (node.Source is Anchored anchored && node.Element is { } at && _panels.TryGetValue(at, out var layer))
        {
            foreach (var anchor in node) Walk(anchor);
            // The panel's layer is a scope of its own, as on the web: a dialog panel takes the parts inside it, and any
            // other panel drops them.
            foreach (var part in layer)
            {
                var inPanel = Begin();
                Walk(part);
                if (anchored.PanelRole == AnchorPanelRole.Dialog && part.Source is Positioned) Container(inPanel, part);
                else Boundary(inPanel);
            }
        }
        else
        {
            foreach (var child in node) Walk(child);
        }
        Settle(node, mark);
    }

    /// <summary>What <paramref name="node"/> is to the parts laid out under it, in the web's terms (<c>Settle</c>).</summary>
    private void Settle(LayoutNode node, Mark mark)
    {
        switch (node.Source)
        {
            case LabelPart:
                Part(mark, describes: false, SemanticsVisitor.TextWithin(node));
                break;
            case DescriptionPart:
                Part(mark, describes: true, SemanticsVisitor.TextWithin(node));
                break;
            case Group or LiveRegion:
                Container(mark, node);
                break;
            case Overlay:
                // The page keeps an overlay's place, and its layer is read under a root of its own (Of).
                Boundary(mark);
                break;
            case TextEntry entry:
                Control(mark, node, named: entry.Label is { Length: > 0 });
                break;
            case Pressable { Role: PressableRole.Checkbox or PressableRole.Radio or PressableRole.Switch } pressable:
                Control(mark, node, named: pressable.Label is { Length: > 0 });
                break;
            default:
                Other(mark, node.Source.NodeKind);
                break;
        }
    }

    private readonly record struct Mark(int Parts, int Controls);

    private Mark Begin() => new(_pending.Count, _controls.Count);

    private void Part(Mark at, bool describes, string words)
    {
        _controls.RemoveRange(at.Controls, _controls.Count - at.Controls);
        _pending.Add(new Pending(describes, words));
    }

    private void Container(Mark at, LayoutNode target)
    {
        Reach(at, target);
        _controls.RemoveRange(at.Controls, _controls.Count - at.Controls);
    }

    private void Control(Mark at, LayoutNode target, bool named)
    {
        var claimed = named || _pending.Count > at.Parts;
        Reach(at, target);
        _controls.RemoveRange(at.Controls, _controls.Count - at.Controls);
        _controls.Add(new Candidate(target) { Claimed = claimed });
    }

    private void Boundary(Mark at)
    {
        _pending.RemoveRange(at.Parts, _pending.Count - at.Parts);
        _controls.RemoveRange(at.Controls, _controls.Count - at.Controls);
    }

    private void Other(Mark at, string kind)
    {
        if (_pending.Count == at.Parts) return;
        Candidate? only = null;
        var count = 0;
        for (var i = at.Controls; i < _controls.Count; i++)
        {
            if (_controls[i].Claimed) continue;
            only ??= _controls[i];
            count++;
        }
        if (count == 0) return;
        if (count > 1)
            throw new InvalidOperationException(Refusal(_pending[at.Parts].Describes, kind, count));
        Reach(at, only!.Target);
        only.Claimed = true;
    }

    /// <summary>The web's words for two candidates (<c>NamingParts.Refusal</c>, <c>refusal</c>), so a developer meets
    /// one sentence on every target.</summary>
    internal static string Refusal(bool describes, string kind, int count) =>
        $"A {(describes ? "DescriptionPart" : "LabelPart")} reaches a {kind} holding {count} controls it could "
        + $"{(describes ? "describe" : "name")}, and it {(describes ? "describes" : "names")} none of them: give each "
        + "field its own Column or Group, with its label and its control inside (#1916).";

    private void Reach(Mark at, LayoutNode target)
    {
        if (_pending.Count == at.Parts) return;
        if (!_named.TryGetValue(target, out var names)) _named[target] = names = new Names();
        for (var i = at.Parts; i < _pending.Count; i++)
        {
            var part = _pending[i];
            if (part.Words.Length == 0) continue;
            (part.Describes ? names.Descriptions : names.Labels).Add(part.Words);
        }
        _pending.RemoveRange(at.Parts, _pending.Count - at.Parts);
    }

    private sealed record Pending(bool Describes, string Words);

    private sealed class Candidate(LayoutNode target)
    {
        public LayoutNode Target { get; } = target;

        public bool Claimed { get; set; }
    }

    private sealed class Names
    {
        public List<string> Labels { get; } = [];

        public List<string> Descriptions { get; } = [];
    }
}
