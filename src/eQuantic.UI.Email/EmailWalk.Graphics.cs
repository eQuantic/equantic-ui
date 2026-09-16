using eQuantic.UI.Primitives;

namespace eQuantic.UI.Email;

/// <summary>
/// Graphics — eight words, of which the medium has one. An <see cref="Image"/> is an
/// <c>&lt;img&gt;</c> at an absolute address, which is the only artwork an inbox can fetch;
/// everything else here draws at RUNTIME — a vector rasterized by the engine, a canvas painted by a
/// callback, a camera — and there is no runtime in a message.
/// </summary>
internal abstract partial class EmailWalk
{
    /// <summary>An image by absolute http(s) address, sized by attributes.</summary>
    public abstract Nothing Visit(Image node, Nothing state);

    public Nothing Visit(CameraPreview node, Nothing state) => Refuse(node);

    public Nothing Visit(Canvas node, Nothing state) => Refuse(node);

    public Nothing Visit(Drawing node, Nothing state) => Refuse(node);

    public Nothing Visit(Icon node, Nothing state) => Refuse(node);

    public Nothing Visit(Spinner node, Nothing state) => Refuse(node);

    public Nothing Visit(Vector node, Nothing state) => Refuse(node);

    public Nothing Visit(WebFrame node, Nothing state) => Refuse(node);
}
