using eQuantic.UI.Native.Framework;
using eQuantic.UI.Primitives;

namespace eQuantic.UI.Native.Testing;

/// <summary>
/// One node as a Photon frame laid it out: Flutter's render box as <c>tester.getRect</c> reads it.
/// <para>
/// A COPY of what the frame measured, not the engine's layout node. The engine writes its nodes again
/// on the next frame, and a host that recycles frames hands them to it, so a test that held one would
/// read whatever the next frame wrote. This one stays what its frame measured.
/// </para>
/// </summary>
public sealed class MeasuredNode
{
    internal MeasuredNode(VisualNode node, Rect bounds, TextMeasurement? text, IReadOnlyList<MeasuredNode> children)
    {
        Node = node;
        Bounds = bounds;
        Text = text;
        Children = children;
    }

    /// <summary>The node laid out: a component for the subtree it built, or the node it built.</summary>
    public VisualNode Node { get; }

    /// <summary>Where the node lies in the viewport, in dp, from its top-left corner.</summary>
    public Rect Bounds { get; }

    /// <summary>The lines the node's text measured, each with its width and whether it was cut short;
    /// null for a node that is not text.</summary>
    public TextMeasurement? Text { get; }

    /// <summary>The nodes this one placed, in paint order.</summary>
    public IReadOnlyList<MeasuredNode> Children { get; }

    /// <summary>The node's kind and box, as a failure prints it.</summary>
    public override string ToString() =>
        $"{Node.GetType().Name} at ({Bounds.X:0.##}, {Bounds.Y:0.##}), {Bounds.Width:0.##} by {Bounds.Height:0.##}"
        + (Text is null ? "" : $", {Text.Lines.Count} line{(Text.Lines.Count == 1 ? "" : "s")}");

    /// <summary>Copies <paramref name="node"/> and everything under it, filing each copy under the id of
    /// the position that built it, the id Photon's semantics tree names it by.</summary>
    /// <exception cref="InvalidOperationException">Two nodes of the frame stand at one position: the
    /// engine lays one node out a position, and a reader that picked between two would answer one of
    /// them in silence.</exception>
    internal static MeasuredNode Copy(LayoutNode node, Dictionary<int, MeasuredNode> byPosition)
    {
        var children = new MeasuredNode[node.Children.Count];
        for (var i = 0; i < children.Length; i++) children[i] = Copy(node.Children[i], byPosition);

        var measured = new MeasuredNode(node.Source, node.Bounds, node.Text, children);
        if (node.Element is { } position && !byPosition.TryAdd(position.Id, measured))
            throw new InvalidOperationException(
                $"Two nodes of the frame stand at one position: {byPosition[position.Id]} and {measured}.");
        return measured;
    }
}
