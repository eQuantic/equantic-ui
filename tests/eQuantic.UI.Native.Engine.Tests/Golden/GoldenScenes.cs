using eQuantic.UI.Primitives;

namespace eQuantic.UI.Native.Engine.Tests.Golden;

/// <summary>
/// The shared scene catalog behind the cross-backend contract (plan M0: "the same N golden images
/// pass on Metal, Vulkan, Reference within tolerance"). ONE definition per scene, consumed twice:
/// <see cref="GoldenSceneTests"/> matches the Reference render against the repo goldens (normative),
/// and <see cref="MetalParityTests"/> renders the same display lists on the GPU and fuzzy-compares
/// against the Reference. Scenes are deliberately small (160×120) and quantized-color so the golden
/// images stay tiny and reviewable in the repo.
/// </summary>
public static class GoldenScenes
{
    public const int Width = 160;
    public const int Height = 120;

    private static readonly Color Background = Color.FromRgb(24, 26, 32);   // dark slate
    private static readonly Color Accent = Color.FromRgb(64, 156, 255);     // primary blue
    private static readonly Color Warm = Color.FromRgb(255, 122, 61);       // orange
    private static readonly Color Mint = Color.FromRgb(52, 199, 130);       // green

    private static readonly Dictionary<string, Action<DisplayListBuilder>> Scenes = new()
    {
        ["clear"] = _ => { },

        ["solid-rect"] = b =>
            b.FillRect(new Rect(30, 25, 100, 70), Paint.Solid(Accent)),

        // Blending correctness: two 50%-alpha rects; the overlap must composite src-over in linear space.
        ["translucent-overlap"] = b =>
        {
            b.FillRect(new Rect(20, 20, 80, 60), Paint.Solid(Warm.WithOpacity(0.5f)));
            b.FillRect(new Rect(60, 40, 80, 60), Paint.Solid(Accent.WithOpacity(0.5f)));
        },

        ["rrect-uniform"] = b =>
            b.FillRRect(new RRect(new Rect(30, 20, 100, 80), new CornerRadii(16)), Paint.Solid(Mint)),

        ["rrect-per-corner"] = b =>
            b.FillRRect(
                new RRect(new Rect(30, 20, 100, 80), new CornerRadii(TopLeft: 32, TopRight: 0, BottomRight: 16, BottomLeft: 8)),
                Paint.Solid(Accent)),

        // Radius 60 on a 120x50 box: vertical sides force the CSS proportional clamp → pill-ish shape.
        ["rrect-radius-overflow"] = b =>
            b.FillRRect(new RRect(new Rect(20, 35, 120, 50), new CornerRadii(60)), Paint.Solid(Warm)),

        ["circle"] = b =>
            b.FillRRect(new RRect(new Rect(55, 35, 50, 50), new CornerRadii(25)), Paint.Solid(Accent)),

        ["border"] = b =>
            b.StrokeRRect(new RRect(new Rect(30, 25, 100, 70), new CornerRadii(12)), 4, Paint.Solid(Mint)),

        ["border-over-fill"] = b =>
        {
            var shape = new RRect(new Rect(30, 25, 100, 70), new CornerRadii(12));
            b.FillRRect(shape, Paint.Solid(Accent.WithOpacity(0.35f)));
            b.StrokeRRect(shape, 3, Paint.Solid(Accent));
        },

        ["gradient-horizontal"] = b =>
            b.FillRect(new Rect(20, 30, 120, 60),
                Paint.Linear(new Point(20, 0), new Point(140, 0), Warm, Accent)),

        ["gradient-diagonal-rrect"] = b =>
            b.FillRRect(new RRect(new Rect(25, 20, 110, 80), new CornerRadii(20)),
                Paint.Linear(new Point(25, 20), new Point(135, 100), Mint, Accent)),

        // Rotate 30° around the rect center: exercises inverse-transform sampling + AA on slanted edges.
        ["transform-rotate"] = b =>
        {
            var center = new Point(80, 60);
            b.PushTransform(
                Matrix2D.Translation(-center.X, -center.Y)
                * Matrix2D.Rotation(30 * MathF.PI / 180)
                * Matrix2D.Translation(center.X, center.Y));
            b.FillRect(new Rect(40, 40, 80, 40), Paint.Solid(Warm));
            b.Pop();
        },

        // Uniform 2x scale about the origin: a 40x25/r6 shape lands as 80x50/r12 — AA width must stay ~1px.
        ["transform-scale"] = b =>
        {
            b.PushTransform(Matrix2D.Scale(2, 2));
            b.FillRRect(new RRect(new Rect(20, 17.5f, 40, 25), new CornerRadii(6)), Paint.Solid(Mint));
            b.Pop();
        },

        // Clip correctness: overflowing content (a rotated rect + gradient + circle) confined to a
        // rounded viewport — the ScrollView contract. Clip edges must anti-alias like shape edges.
        ["clip-rrect"] = b =>
        {
            var viewport = new RRect(new Rect(30, 20, 100, 80), new CornerRadii(16));
            b.PushClip(viewport);
            b.FillRect(new Rect(10, 30, 140, 25), Paint.Linear(new Point(10, 0), new Point(150, 0), Warm, Accent));
            b.FillRRect(new RRect(new Rect(90, 50, 70, 70), new CornerRadii(35)), Paint.Solid(Mint));
            var center = new Point(50, 75);
            b.PushTransform(
                Matrix2D.Translation(-center.X, -center.Y)
                * Matrix2D.Rotation(20 * MathF.PI / 180)
                * Matrix2D.Translation(center.X, center.Y));
            b.FillRect(new Rect(20, 65, 60, 20), Paint.Solid(Accent.WithOpacity(0.7f)));
            b.Pop();
            b.PopClip();
            // Outside the clip, unaffected: proves the pop restored the unclipped state.
            b.FillRRect(new RRect(new Rect(140, 95, 14, 14), new CornerRadii(7)), Paint.Solid(Warm));
        },

        // The §05 analytic shadow: an elevated card (E2-ish blur) over the background, plus a
        // small-radius chip shadow — falloff must be smooth and hug the per-corner radii.
        ["shadow-rrect"] = b =>
        {
            var card = new RRect(new Rect(30, 20, 100, 70), new CornerRadii(14));
            b.ShadowRRect(card, offsetY: 4, blur: 12, spread: 0, Color.FromRgb(0, 0, 0).WithOpacity(0.45f));
            b.FillRRect(card, Paint.Solid(Color.FromRgb(38, 41, 51)));
            var chip = new RRect(new Rect(100, 85, 44, 22), new CornerRadii(11));
            b.ShadowRRect(chip, offsetY: 2, blur: 6, spread: 1, Color.FromRgb(0, 0, 0).WithOpacity(0.5f));
            b.FillRRect(chip, Paint.Solid(Mint));
        },

        // A miniature "UI card": the primitives composing the way the component layer drives them.
        ["card-composition"] = b =>
        {
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
        },
    };

    public static TheoryData<string> Names
    {
        get
        {
            var data = new TheoryData<string>();
            foreach (var name in Scenes.Keys) data.Add(name);
            return data;
        }
    }

    public static DisplayList Build(string name)
    {
        if (!Scenes.TryGetValue(name, out var compose))
            throw new ArgumentException($"Unknown golden scene '{name}'.", nameof(name));
        var builder = new DisplayListBuilder();
        builder.Clear(Background);
        compose(builder);
        return builder.Build();
    }
}
