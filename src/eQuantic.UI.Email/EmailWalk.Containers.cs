using eQuantic.UI.Primitives;

namespace eQuantic.UI.Email;

/// <summary>
/// Containers and layout — fourteen words, of which the medium has three. Column, Row and Box are
/// what nested tables can express; the other eleven need a layout engine an email client does not
/// have (there is no flexbox, no grid and no <c>gap</c> in Word's engine) or a viewport a printed
/// page does not have.
/// </summary>
internal abstract partial class EmailWalk
{
    /// <summary>A Column is a table with one row per child; its gap is a spacer row.</summary>
    public abstract Nothing Visit(Column node, Nothing state);

    /// <summary>A Row is exactly one table row; its gap is a spacer cell.</summary>
    public abstract Nothing Visit(Row node, Nothing state);

    /// <summary>A Box is a cell that paints a background and padding.</summary>
    public abstract Nothing Visit(Box node, Nothing state);

    public Nothing Visit(AdaptiveNode node, Nothing state) => Refuse(node);

    public Nothing Visit(Anchored node, Nothing state) => Refuse(node);

    public Nothing Visit(Flexible node, Nothing state) => Refuse(node);

    public Nothing Visit(Grid node, Nothing state) => Refuse(node);

    public Nothing Visit(Overlay node, Nothing state) => Refuse(node);

    public Nothing Visit(Pinned node, Nothing state) => Refuse(node);

    public Nothing Visit(Positioned node, Nothing state) => Refuse(node);

    public Nothing Visit(SafeArea node, Nothing state) => Refuse(node);

    public Nothing Visit(ScrollView node, Nothing state) => Refuse(node);

    public Nothing Visit(Spacer node, Nothing state) => Refuse(node);

    public Nothing Visit(Stack node, Nothing state) => Refuse(node);
}
