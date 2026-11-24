using eQuantic.UI.Native.Framework;
using eQuantic.UI.Primitives;

namespace eQuantic.UI.Native.Components;

/// <summary>The two words that change how many stops a subtree makes, rather than what one of them is.</summary>
internal sealed partial class SemanticsVisitor
{
    /// <summary>
    /// A subtree a reader passes over: nothing in it is a stop, whatever it holds, as the web's
    /// <c>aria-hidden</c> host is read (<c>LowerExcludeSemantics</c>). Not excluding, it is a wrapper and
    /// what it holds speaks for itself.
    /// </summary>
    public bool Visit(ExcludeSemantics node, LayoutNode laidOut) => node.Excluding ? Consumed : Wraps;

    /// <summary>
    /// ONE STOP for the subtree: its stops are collected as they would be heard, and joined. When a control
    /// is among them the stop is the FIRST control, with its role, value, states and identity, so the
    /// bridge's activate presses it, named by every stop's name in reading order, its own in its place. A
    /// control after it stays a stop of its own, after the merged one, and its name is its own. With no
    /// control the stop is a group named the same way, at the merge's own identity. Either way the stop has
    /// the merge's bounds: the box a reader outlines is the whole row it hears.
    /// <para>The web writes the same reading (<c>LowerMergeSemantics</c>): the parts are hidden and named
    /// into the control's <c>aria-labelledby</c>, or into the group's.</para>
    /// </summary>
    public bool Visit(MergeSemantics node, LayoutNode laidOut)
    {
        var heard = new List<SemanticNode>();
        SemanticsTree.CollectChildren(laidOut, heard);
        if (heard.Count == 0) return Consumed;

        var control = heard.FindIndex(stop => IsControl(stop.Role));
        var names = new List<string>();
        for (var i = 0; i < heard.Count; i++)
        {
            if (i != control && IsControl(heard[i].Role)) continue;
            if (heard[i].Label is { Length: > 0 } name) names.Add(name);
        }
        var label = string.Join(" ", names);

        _nodes.Add(control >= 0
            ? heard[control] with { Label = label, Bounds = laidOut.Bounds }
            : new SemanticNode(SemanticRole.Group, IdOf(laidOut), laidOut.Bounds, label, null, false));
        for (var i = 0; i < heard.Count; i++)
            if (i != control && IsControl(heard[i].Role)) _nodes.Add(heard[i]);
        return Consumed;
    }

    /// <summary>
    /// The roles a person operates, the ones a merge's stop becomes. The web reads the same set from the
    /// markup it wrote (<c>MergedReading.IsControl</c>): an element whose role, written or its own, is one of
    /// these.
    /// </summary>
    internal static bool IsControl(SemanticRole role) => role is SemanticRole.Button or SemanticRole.Link
        or SemanticRole.TextField or SemanticRole.CodeField or SemanticRole.Slider or SemanticRole.Checkbox
        or SemanticRole.Switch or SemanticRole.GridCell or SemanticRole.Radio or SemanticRole.Tab
        or SemanticRole.MenuItem or SemanticRole.Option or SemanticRole.Destination or SemanticRole.ComboBox
        or SemanticRole.TreeItem or SemanticRole.EditableComboBox;
}
