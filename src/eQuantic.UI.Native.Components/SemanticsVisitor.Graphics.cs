using eQuantic.UI.Native.Framework;
using eQuantic.UI.Primitives;

namespace eQuantic.UI.Native.Components;

/// <summary>
/// Graphics — eight words that draw. Six of them carry a <c>Label</c>, and the rule is one rule for
/// all six: a labelled graphic announces what it IS, an unlabelled one is decoration and stays
/// silent. That is HTML's own contract for <c>alt</c>, and the day it was written down here only one
/// of the six honoured it.
///
/// <para>
/// This family is why the visitor exists. <c>Image</c>, <c>Canvas</c>, <c>Vector</c>, <c>Drawing</c>
/// and <c>CameraPreview</c> each grew a <c>Label</c> the web read and Photon did not, and each was
/// found separately — by a consumer counting accessibility elements, then by hand, then by asking the
/// question of the ASSEMBLY instead of a reader's memory. A seventh graphic with a label cannot
/// repeat it: there is a method here it has to fill in.
/// </para>
/// </summary>
internal sealed partial class SemanticsVisitor
{
    /// <inheritdoc cref="Graphic"/>
    public bool Visit(Icon node, LayoutNode laidOut) => Graphic(node.Label, laidOut);

    /// <summary>Alt text is what an image says, and an empty alt is HTML's own way of saying
    /// decorative. <inheritdoc cref="Graphic" path="/summary"/></summary>
    public bool Visit(Primitives.Image node, LayoutNode laidOut) => Graphic(node.Label, laidOut);

    /// <summary><c>Canvas.Label</c> says "what assistive technology is told this canvas IS, because a
    /// drawing says nothing on its own" — a labelled chart or diagram.</summary>
    public bool Visit(Canvas node, LayoutNode laidOut) => Graphic(node.Label, laidOut);

    /// <inheritdoc cref="Graphic"/>
    public bool Visit(Vector node, LayoutNode laidOut) => Graphic(node.Label, laidOut);

    /// <summary>
    /// <inheritdoc cref="Graphic" path="/summary"/> Its INPUT shapes are controls of their own, announced
    /// after the drawing as a reader walks it, each a button named by its label with its toggle state as
    /// the selected one, at the id the input route gives it, so a reader's activation runs the shape the
    /// keyboard stops on (#674). A drawing with no label still announces them: the picture is
    /// decoration, the concelhos are not.
    /// </summary>
    public bool Visit(Drawing node, LayoutNode laidOut)
    {
        var announced = Graphic(node.Label, laidOut);
        var shapes = node.Artwork.Shapes;
        for (var i = 0; i < shapes.Count; i++)
        {
            var shape = shapes[i];
            if (!shape.IsInput) continue;
            if (laidOut.Element is not NodeElement at)
                throw new InvalidOperationException("A drawing with input shapes was announced with no position to name them by.");
            _nodes.Add(new SemanticNode(SemanticRole.Button, at.PartId(i),
                ShapeHit.BoundsOf(node.Artwork, shape, laidOut.Bounds), shape.Label ?? "", null, false,
                Selected: shape.Selected));
        }
        return announced;
    }

    /// <inheritdoc cref="Graphic"/>
    public bool Visit(CameraPreview node, LayoutNode laidOut) => Graphic(node.Label, laidOut);

    /// <inheritdoc cref="Decorative"/>
    public bool Visit(Spinner node, LayoutNode laidOut) => Decorative;

    /// <inheritdoc cref="EscapeHatch"/>
    public bool Visit(WebFrame node, LayoutNode laidOut) => EscapeHatch;

    /// <summary>The one rule, in one place: what a graphic says is its label, and a graphic with
    /// nothing to say says nothing.</summary>
    private bool Graphic(string? label, LayoutNode laidOut) =>
        label is { Length: > 0 }
            ? Announce(new(SemanticRole.Image, IdOf(laidOut), laidOut.Bounds, label, null, false))
            : Decorative;
}
