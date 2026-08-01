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
        // W4b: the Texture command — a deterministic A8 coverage raster (checker + ramp)
        // tinted by the paint, one copy clipped by an rrect. Nearest sampling on BOTH backends.
        ["texture-coverage"] = b =>
        {
            const int tw = 16, th = 12;
            var alpha = new byte[tw * th];
            for (var y = 0; y < th; y++)
                for (var x = 0; x < tw; x++)
                    alpha[y * tw + x] = (x / 2 + y / 2) % 2 == 0
                        ? (byte)255
                        : (byte)(x * 255 / (tw - 1));
            var raster = new TextureData(tw, th, alpha);
            b.Texture(new Rect(12, 12, 64, 48), Color.FromRgb(31, 111, 235), raster);
            b.PushClip(new RRect(new Rect(96, 24, 48, 48), new CornerRadii(16)));
            b.Texture(new Rect(88, 16, 64, 64), Mint, raster);
            b.PopClip();
        },

        // GROUP-OPACITY LAYERS (spec S1) — the scene that DISCRIMINATES a real offscreen layer from
        // the per-command-alpha approximation. Two opaque circles OVERLAP inside a 50% layer: with a
        // real layer the union composites once and the overlap looks identical to the rest; with the
        // approximation each circle blends separately and the overlap goes visibly darker. The
        // nested layer on the right multiplies alpha through two levels.
        ["layer-group-opacity"] = b =>
        {
            // NOTE: Build() already clears to Background. A second Clear here would diverge by
            // design — the Reference applies mid-stream Clears, the GPU backends honor only the
            // leading one (the documented spike fence) — and the parity gate catches it.
            b.PushLayer(0.5f);
            b.FillRRect(new RRect(new Rect(12, 20, 48, 48), new CornerRadii(24)),
                Paint.Solid(Color.FromRgb(0x5B, 0x88, 0xFF)));
            b.FillRRect(new RRect(new Rect(40, 20, 48, 48), new CornerRadii(24)),
                Paint.Solid(Color.FromRgb(0x5B, 0x88, 0xFF)));
            b.PopLayer();

            b.PushLayer(0.8f);
            b.FillRect(new Rect(96, 16, 52, 40), Paint.Solid(Color.FromRgb(0x80, 0xB8, 0x5C)));
            b.PushLayer(0.5f);
            b.FillRect(new Rect(104, 30, 52, 40), Paint.Solid(Color.FromRgb(0x9B, 0x8C, 0xFF)));
            b.PopLayer();
            b.PopLayer();

            // A layer with a CLIP still composites through the clip.
            b.PushClip(new RRect(new Rect(20, 80, 120, 30), new CornerRadii(15)));
            b.PushLayer(0.6f);
            b.FillRect(new Rect(16, 76, 60, 38), Paint.Solid(Color.FromRgb(0xF5, 0x9E, 0x0B)));
            b.FillRect(new Rect(60, 76, 60, 38), Paint.Solid(Color.FromRgb(0xF5, 0x9E, 0x0B)));
            b.PopLayer();
            b.PopClip();
        },

        // RADIAL gradients (the hero "spotlight"): a circular glow, an ELLIPTICAL one (the shape the
        // design actually uses — 800×500 at 20% 0%), and one fading to TRANSPARENT over a solid so
        // the alpha ramp is exercised, not just the hue ramp. Radii reuse the linear slots, so this
        // scene is also what proves the reinterpretation is wired identically on all three backends.
        ["gradient-radial"] = b =>
        {
            b.FillRect(new Rect(0, 0, 160, 120), Paint.Solid(new Color(0x0A, 0x0D, 0x1A, 255)));

            b.FillRect(new Rect(8, 8, 64, 64),
                Paint.Radial(new Point(40, 40), 32, 32,
                    Color.FromRgb(0x5B, 0x88, 0xFF), new Color(0x5B, 0x88, 0xFF, 0)));

            b.FillRect(new Rect(80, 8, 72, 48),
                Paint.Radial(new Point(96, 8), 64, 40,
                    Color.FromRgb(0x7A, 0x64, 0xFF), Color.FromRgb(0x80, 0xB8, 0x5C)));

            b.FillRRect(new RRect(new Rect(16, 78, 128, 34), new CornerRadii(12)),
                Paint.Radial(new Point(80, 95), 60, 18,
                    new Color(0xFF, 0xFF, 0xFF, 200), new Color(0xFF, 0xFF, 0xFF, 0)));
        },

        // GRADIENT TEXT: A8 coverage filled with a gradient TINT instead of a flat color. The axis
        // is LOCAL space and evaluates through the normative Paint.ColorAt on both rasterizers, so
        // this scene is what keeps the shader's lerp honest against the CPU reference. The second
        // copy runs the axis vertically under a clip, mixing the two free-slot features.
        ["texture-gradient-tint"] = b =>
        {
            const int tw = 24, th = 8;
            var alpha = new byte[tw * th];
            for (var y = 0; y < th; y++)
                for (var x = 0; x < tw; x++)
                    alpha[y * tw + x] = (byte)(y < 2 || y >= th - 2 ? 255 : (x * 255 / (tw - 1)));
            var raster = new TextureData(tw, th, alpha);

            b.Texture(new Rect(12, 16, 120, 32),
                Paint.Linear(new Point(12, 16), new Point(132, 16),
                    Color.FromRgb(0x8E, 0xB0, 0xFF), Color.FromRgb(0x7A, 0x64, 0xFF)),
                raster);

            b.PushClip(new RRect(new Rect(16, 60, 110, 40), new CornerRadii(12)));
            b.Texture(new Rect(12, 56, 120, 48),
                Paint.Linear(new Point(12, 56), new Point(12, 104),
                    Color.FromRgb(0x80, 0xB8, 0x5C), new Color(0x28, 0x49, 0xF0, 160)),
                raster);
            b.PopClip();
        },

        // W4 images: an Rgba8 texture (2×2 color quad) in the three fits' geometry — texel color
        // wins over the tint, alpha blends, the covered copy clips to an rrect.
        ["texture-rgba"] = b =>
        {
            var rgba = TextureData.Rgba(2, 2, new byte[]
            {
                255, 0, 0, 255,   0, 255, 0, 255,
                0, 0, 255, 255,   255, 255, 255, 128,
            });
            b.Texture(new Rect(10, 10, 60, 40), new Color(0, 0, 0, 255), rgba);
            b.PushClip(new RRect(new Rect(90, 14, 48, 48), new CornerRadii(14)));
            b.Texture(new Rect(84, 8, 60, 60), new Color(0, 0, 0, 255), rgba);
            b.PopClip();
        },

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
