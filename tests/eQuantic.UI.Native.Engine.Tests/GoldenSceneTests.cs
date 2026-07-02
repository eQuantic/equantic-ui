using eQuantic.UI.Native.Engine;
using eQuantic.UI.Native.Engine.Reference;
using eQuantic.UI.Native.Engine.Tests.Golden;

namespace eQuantic.UI.Native.Engine.Tests;

/// <summary>
/// The first golden-image cases (plan M0: "the same N golden images pass on Metal, Vulkan, Reference
/// within tolerance"). Today they run on the Reference backend — the normative output; the GPU
/// backends will run this exact suite against the same goldens. Scenes are deliberately small
/// (160×120) and quantized-color so the images stay tiny and reviewable in the repo.
/// </summary>
public class GoldenSceneTests
{
    private const int W = 160;
    private const int H = 120;

    private static readonly Color Background = Color.FromRgb(24, 26, 32);   // dark slate
    private static readonly Color Accent = Color.FromRgb(64, 156, 255);     // primary blue
    private static readonly Color Warm = Color.FromRgb(255, 122, 61);       // orange
    private static readonly Color Mint = Color.FromRgb(52, 199, 130);       // green

    private static void Run(string name, Action<DisplayListBuilder> scene)
    {
        using var backend = new ReferenceBackend();
        using var surface = backend.CreateSurface(W, H);
        var builder = new DisplayListBuilder();
        builder.Clear(Background);
        scene(builder);
        backend.Render(builder.Build(), surface);
        GoldenImage.Match(surface, name);
    }

    [Fact]
    public void Clear() => Run("clear", _ => { });

    [Fact]
    public void SolidRect() => Run("solid-rect", b =>
        b.FillRect(new Rect(30, 25, 100, 70), Paint.Solid(Accent)));

    [Fact]
    public void TranslucentOverlap() => Run("translucent-overlap", b =>
    {
        // Blending correctness: two 50%-alpha rects; the overlap must composite src-over in linear space.
        b.FillRect(new Rect(20, 20, 80, 60), Paint.Solid(Warm.WithOpacity(0.5f)));
        b.FillRect(new Rect(60, 40, 80, 60), Paint.Solid(Accent.WithOpacity(0.5f)));
    });

    [Fact]
    public void RRectUniform() => Run("rrect-uniform", b =>
        b.FillRRect(new RRect(new Rect(30, 20, 100, 80), new CornerRadii(16)), Paint.Solid(Mint)));

    [Fact]
    public void RRectPerCorner() => Run("rrect-per-corner", b =>
        b.FillRRect(
            new RRect(new Rect(30, 20, 100, 80), new CornerRadii(TopLeft: 32, TopRight: 0, BottomRight: 16, BottomLeft: 8)),
            Paint.Solid(Accent)));

    [Fact]
    public void RRectRadiusOverflowClamps() => Run("rrect-radius-overflow", b =>
        // Radius 60 on a 120x50 box: vertical sides force the CSS proportional clamp → pill-ish shape.
        b.FillRRect(new RRect(new Rect(20, 35, 120, 50), new CornerRadii(60)), Paint.Solid(Warm)));

    [Fact]
    public void Circle() => Run("circle", b =>
        b.FillRRect(new RRect(new Rect(55, 35, 50, 50), new CornerRadii(25)), Paint.Solid(Accent)));

    [Fact]
    public void Border() => Run("border", b =>
        b.StrokeRRect(new RRect(new Rect(30, 25, 100, 70), new CornerRadii(12)), 4, Paint.Solid(Mint)));

    [Fact]
    public void BorderOverFill() => Run("border-over-fill", b =>
    {
        var shape = new RRect(new Rect(30, 25, 100, 70), new CornerRadii(12));
        b.FillRRect(shape, Paint.Solid(Accent.WithOpacity(0.35f)));
        b.StrokeRRect(shape, 3, Paint.Solid(Accent));
    });

    [Fact]
    public void GradientHorizontal() => Run("gradient-horizontal", b =>
        b.FillRect(new Rect(20, 30, 120, 60),
            Paint.Linear(new Point(20, 0), new Point(140, 0), Warm, Accent)));

    [Fact]
    public void GradientDiagonalOnRRect() => Run("gradient-diagonal-rrect", b =>
        b.FillRRect(new RRect(new Rect(25, 20, 110, 80), new CornerRadii(20)),
            Paint.Linear(new Point(25, 20), new Point(135, 100), Mint, Accent)));

    [Fact]
    public void TransformRotatedRect() => Run("transform-rotate", b =>
    {
        // Rotate 30° around the rect center: exercises inverse-transform sampling + AA on slanted edges.
        var center = new Point(80, 60);
        b.PushTransform(
            Matrix2D.Translation(-center.X, -center.Y)
            * Matrix2D.Rotation(30 * MathF.PI / 180)
            * Matrix2D.Translation(center.X, center.Y));
        b.FillRect(new Rect(40, 40, 80, 40), Paint.Solid(Warm));
        b.Pop();
    });

    [Fact]
    public void TransformScaledRRect() => Run("transform-scale", b =>
    {
        // Uniform 2x scale about the origin: a 40x25/r6 shape lands as 80x50/r12 — AA width must stay ~1px.
        b.PushTransform(Matrix2D.Scale(2, 2));
        b.FillRRect(new RRect(new Rect(20, 17.5f, 40, 25), new CornerRadii(6)), Paint.Solid(Mint));
        b.Pop();
    });

    [Fact]
    public void CardComposition() => Run("card-composition", b =>
    {
        // A miniature "UI card": the primitives composing the way the widget layer will drive them.
        var card = new RRect(new Rect(20, 15, 120, 90), new CornerRadii(10));
        b.FillRRect(card, Paint.Solid(Color.FromRgb(38, 41, 51)));
        b.StrokeRRect(card, 2, Paint.Solid(Color.FromRgb(58, 63, 77)));
        // header strip
        b.FillRRect(new RRect(new Rect(30, 25, 70, 10), new CornerRadii(5)), Paint.Solid(Color.FromRgb(90, 96, 115)));
        // avatar circle
        b.FillRRect(new RRect(new Rect(30, 45, 24, 24), new CornerRadii(12)), Paint.Solid(Accent));
        // two content lines
        b.FillRRect(new RRect(new Rect(62, 49, 66, 7), new CornerRadii(3.5f)), Paint.Solid(Color.FromRgb(70, 75, 92)));
        b.FillRRect(new RRect(new Rect(62, 60, 48, 7), new CornerRadii(3.5f)), Paint.Solid(Color.FromRgb(70, 75, 92)));
        // action pill with gradient
        b.FillRRect(new RRect(new Rect(30, 80, 56, 16), new CornerRadii(8)),
            Paint.Linear(new Point(30, 0), new Point(86, 0), Accent, Mint));
    });
}
