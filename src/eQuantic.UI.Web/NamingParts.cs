using eQuantic.UI.Primitives;

namespace eQuantic.UI.Web;

/// <summary>
/// Which nameable each <see cref="LabelPart"/> and <see cref="DescriptionPart"/> reaches, settled as the lowering
/// unwinds (TS twin: <c>naming-parts.ts</c>, Photon's twin: <c>NamedByParts</c>, #1916).
///
/// <para>
/// A part reaches the first ancestor that IS a nameable, or that HOLDS a nameable control no nearer part already
/// reached. The walk keeps two lists in reading order, the parts that reached nothing yet and the nameable controls it
/// met, and each node takes a <see cref="Mark"/> of both before its subtree is lowered: what lies past the mark when it
/// returns is its subtree's. A nameable container takes every part past its mark; a node holding exactly one candidate
/// control past its mark hands it every part past the mark; a layer that is no dialog drops what reached nothing inside
/// it; anything else leaves both lists to the node above. The same shape, bottom up, is what Photon's semantics walk
/// computes over its laid-out tree, so the two targets name the same nameable by the same parts.
/// </para>
///
/// <para>
/// A node holding TWO candidate controls refuses, rather than naming the first: a developer composing a form of their own
/// is told to give each field its own group, instead of having the wrong field named in silence.
/// </para>
///
/// <para>
/// A part takes its id only when it reaches a nameable, scoped by its path as <see cref="MergedReading"/>'s parts are, so
/// a part that names nothing writes nothing. The nameable lists the ids in <c>aria-labelledby</c> or
/// <c>aria-describedby</c>, after any it already lists, and the browser computes the name from what the parts say, so it
/// follows their text.
/// </para>
/// </summary>
internal sealed class NamingParts
{
    private readonly List<Pending> _pending = [];
    private readonly List<Candidate> _controls = [];

    /// <summary>Where a node's subtree starts in the two lists.</summary>
    internal readonly record struct Mark(int Parts, int Controls);

    /// <summary>The lists' ends, before a node's subtree is lowered.</summary>
    internal Mark Begin() => new(_pending.Count, _controls.Count);

    /// <summary>A part lowered to <paramref name="host"/>: it waits for the nameable it reaches, and a control inside it is
    /// no candidate for anything.</summary>
    internal void Part(Mark at, bool describes, HtmlElement host, string id)
    {
        _controls.RemoveRange(at.Controls, _controls.Count - at.Controls);
        _pending.Add(new Pending(describes, host, id));
    }

    /// <summary>A container that names itself: every part past the mark reaches <paramref name="target"/>, and what it
    /// holds is no candidate outside it.</summary>
    internal void Container(Mark at, HtmlElement target)
    {
        Reach(at, target);
        _controls.RemoveRange(at.Controls, _controls.Count - at.Controls);
    }

    /// <summary>A nameable control: a part inside it reaches it, and it is then a candidate for the parts outside, claimed
    /// already when one inside reached it or when it carries a label of its own (<paramref name="named"/>): a field
    /// that names itself is no field a dialog's title could be meant for.</summary>
    internal void Control(Mark at, HtmlElement target, bool named)
    {
        var claimed = named || _pending.Count > at.Parts;
        Reach(at, target);
        _controls.RemoveRange(at.Controls, _controls.Count - at.Controls);
        _controls.Add(new Candidate(target) { Claimed = claimed });
    }

    /// <summary>A layer that is no dialog: a part inside it names nothing, and what it holds is no candidate outside it.</summary>
    internal void Boundary(Mark at)
    {
        _pending.RemoveRange(at.Parts, _pending.Count - at.Parts);
        _controls.RemoveRange(at.Controls, _controls.Count - at.Controls);
    }

    /// <summary>
    /// Any other node: the parts past the mark reach the one candidate control past it, if there is one, which is then
    /// claimed. Two candidates are refused, naming <paramref name="kind"/>, the node that holds them.
    /// </summary>
    internal void Other(Mark at, string kind)
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
        if (count > 1) throw new InvalidOperationException(Refusal(_pending[at.Parts].Describes, kind, count));
        Reach(at, only!.Target);
        only.Claimed = true;
    }

    /// <summary>The words every producer refuses two candidates with (TS twin: <c>refusal</c>; Photon: <c>NamedByParts</c>).</summary>
    internal static string Refusal(bool describes, string kind, int count) =>
        $"A {(describes ? "DescriptionPart" : "LabelPart")} reaches a {kind} holding {count} controls it could "
        + $"{(describes ? "describe" : "name")}, and it {(describes ? "describes" : "names")} none of them: give each "
        + "field its own Column or Group, with its label and its control inside (#1916).";

    /// <summary>Every part past the mark reaches <paramref name="target"/>, in reading order, and leaves the list.</summary>
    private void Reach(Mark at, HtmlElement target)
    {
        for (var i = at.Parts; i < _pending.Count; i++)
        {
            var part = _pending[i];
            part.Host.Id = part.Id;
            List(target, part.Describes ? "aria-describedby" : "aria-labelledby", part.Id);
        }
        _pending.RemoveRange(at.Parts, _pending.Count - at.Parts);
    }

    /// <summary>
    /// <paramref name="id"/> listed in <paramref name="attribute"/> of <paramref name="target"/>, after the ids it already
    /// lists: in its raw attributes when a lowering wrote the attribute there (a field's description twin), and in its
    /// typed member otherwise, so the element never carries the attribute twice.
    /// </summary>
    private static void List(HtmlElement target, string attribute, string id)
    {
        if (target is RealizedElement { RawAttributes: { } raw } && raw.TryGetValue(attribute, out var written))
        {
            raw[attribute] = Joined(written, id);
            return;
        }
        if (attribute == "aria-describedby") target.AriaDescribedBy = Joined(target.AriaDescribedBy, id);
        else target.AriaLabelledBy = Joined(target.AriaLabelledBy, id);
    }

    private static string Joined(string? listed, string id) => listed is { Length: > 0 } ? $"{listed} {id}" : id;

    /// <summary>A part that reached nothing yet.</summary>
    private sealed record Pending(bool Describes, HtmlElement Host, string Id);

    /// <summary>A nameable control the walk met, and whether a nearer part already reached it.</summary>
    private sealed class Candidate(HtmlElement target)
    {
        public HtmlElement Target { get; } = target;

        public bool Claimed { get; set; }
    }
}
