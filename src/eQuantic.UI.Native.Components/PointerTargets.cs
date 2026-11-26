using eQuantic.UI.Native.Framework;
using eQuantic.UI.Primitives;

namespace eQuantic.UI.Native.Components;

/// <summary>
/// The frame's <see cref="PointerTarget"/>s, collected by the paint walk from the first <see cref="Listener"/> or
/// <see cref="RawGestureDetector"/> on (#1562, #1563). Before one, nothing a frame draws can be its child or stand
/// above it, so a frame with neither collects nothing and allocates nothing for it.
/// <para>
/// A node is a solid when it painted something of its OWN: a command written while it was the innermost node
/// open, before its first child, between two of them or after its last. A command counts as the builder counts
/// it, and the layer a box opens for its opacity is no paint, so the walk settles past it
/// (<see cref="Settle"/>). Registering input (a press, a drag, a scroll, a field) makes a node a solid too
/// (<see cref="Answers"/>): a transparent button is hit where it answers.
/// </para>
/// </summary>
internal sealed class PointerTargets
{
    private readonly List<PointerTarget> _entries = [];

    /// <summary>The nodes open in the walk, innermost last: each one's entry and the command count its own paint
    /// is measured from.</summary>
    private readonly List<(int Index, int Mark)> _open = [];

    public IReadOnlyList<PointerTarget> Entries => _entries;

    /// <summary>A node's visit begins, at <paramref name="commands"/> written. What the node around it wrote since
    /// its own mark was its own paint.</summary>
    public void Enter(Rect bounds, Rect visible, VisualNode source, Element? element, int commands)
    {
        if (_open.Count > 0 && commands > _open[^1].Mark) MarkSolid(_open[^1].Index);
        _open.Add((_entries.Count, commands));
        _entries.Add(new PointerTarget(bounds, visible, End: -1, Solid: false, source as Listener,
            source as RawGestureDetector, element));
    }

    /// <summary>The innermost node's visit ends, at <paramref name="commands"/> written: what it wrote since its
    /// last child was its own, its subtree ends here, and the node around it measures its own paint from now.</summary>
    public void Leave(int commands)
    {
        var (index, mark) = _open[^1];
        _open.RemoveAt(_open.Count - 1);
        if (commands > mark) MarkSolid(index);
        _entries[index] = _entries[index] with { End = _entries.Count };
        Settle(commands);
    }

    /// <summary>The commands written so far are no paint of the innermost node open: a layer it or a child
    /// opened or closed.</summary>
    public void Settle(int commands)
    {
        if (_open.Count > 0) _open[^1] = (_open[^1].Index, commands);
    }

    /// <summary>The innermost node open registered input: it is hit where it answers.</summary>
    public void Answers()
    {
        if (_open.Count > 0) MarkSolid(_open[^1].Index);
    }

    private void MarkSolid(int index)
    {
        // A listener or a detector draws nothing of its own and is never a solid: what it holds is.
        if (_entries[index] is { Solid: false, Behavior: null } entry) _entries[index] = entry with { Solid = true };
    }
}
