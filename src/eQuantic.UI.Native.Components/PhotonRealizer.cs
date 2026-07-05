using eQuantic.UI.Native.Engine;
using eQuantic.UI.Native.Framework;
using eQuantic.UI.Primitives;

namespace eQuantic.UI.Native.Components;

/// <summary>A pressable region registered by the realizer — hit rect expanded to the §08 contract.</summary>
public readonly record struct HitRegion(Rect Bounds, Pressable Node);

/// <summary>The realized frame: the laid-out tree (absolute bounds) and the interactive hit regions.</summary>
public sealed class RealizeResult
{
    public RealizeResult(LayoutNode root, IReadOnlyList<HitRegion> hitRegions)
    {
        Root = root;
        HitRegions = hitRegions;
    }

    public LayoutNode Root { get; }
    public IReadOnlyList<HitRegion> HitRegions { get; }
}

/// <summary>
/// The NATIVE REALIZER for the shared abstract vocabulary (docs/SHARED-COMPONENTS-PLAN.md): lays a
/// <see cref="VisualNode"/> tree out with the C# flex engine, resolves tokens for the active theme
/// mode, and lowers the result to Photon draw commands. The web realizer lowers the SAME tree to
/// HtmlElement/DOM + CSS.
/// </summary>
public static class PhotonRealizer
{
    public static RealizeResult Realize(
        VisualNode root,
        float viewportWidth,
        float viewportHeight,
        IAppTheme theme,
        ThemeMode mode,
        DisplayListBuilder builder,
        ITextMeasurer? measurer = null,
        float typeScale = 1f)
    {
        var context = new LayoutContext(theme, measurer ?? ApproximateTextMeasurer.Instance, typeScale);
        var layout = LayoutEngine.Layout(root, viewportWidth, viewportHeight, context);

        var hits = new List<HitRegion>();
        Emit(layout, theme, mode, builder, hits);
        return new RealizeResult(layout, hits);
    }

    private static void Emit(LayoutNode node, IAppTheme theme, ThemeMode mode, DisplayListBuilder builder, List<HitRegion> hits)
    {
        switch (node.Source)
        {
            case Box box:
                EmitChrome(node.Bounds, box.Style.Background, box.Style.CornerRadius,
                    box.Style.BorderColor, box.Style.BorderWidth, theme, mode, builder);
                break;

            case FlexNode flex when flex.Background is not null:
                EmitChrome(node.Bounds, flex.Background, flex.CornerRadius, default, 0, theme, mode, builder);
                break;

            case Text text:
                EmitTextPlaceholder(node, text, theme, mode, builder);
                break;

            // Spec A11 fence: a SurfaceSubtle box under the radius stands in for the bitmap until the
            // engine gains texture upload (M4) - the documented placeholder pattern.
            case Image image:
                builder.FillRRect(
                    new RRect(node.Bounds, image.CornerRadius),
                    Paint.Solid(theme.SurfaceSubtle.Resolve(mode)));
                break;

            // Spec A10, W4 fence: a tinted disc at 30% alpha stands in for the glyph until the atlas
            // lands — the same documented placeholder pattern as text bars.
            case Icon icon:
            {
                var tint = (icon.Color ?? theme.TextPrimary).Resolve(mode).WithOpacity(0.30f);
                builder.FillRRect(
                    new RRect(node.Bounds, new CornerRadii(node.Bounds.Width / 2)),
                    Paint.Solid(tint));
                break;
            }

            case Pressable pressable:
                hits.Add(new HitRegion(ExpandHitRect(node.Bounds), pressable));
                break;
        }

        // A ScrollView clips its subtree to the viewport (spec A6) — the engine clip primitive.
        if (node.Source is ScrollView)
        {
            builder.PushClip(new RRect(node.Bounds));
            foreach (var child in node.Children)
                Emit(child, theme, mode, builder, hits);
            builder.PopClip();
            return;
        }

        foreach (var child in node.Children)
            Emit(child, theme, mode, builder, hits);
    }

    private static void EmitChrome(Rect bounds, ColorToken? background, CornerRadii radius,
        ColorToken borderColor, float borderWidth, IAppTheme theme, ThemeMode mode, DisplayListBuilder builder)
    {
        if (bounds.IsEmpty) return;

        if (background is { } bg)
        {
            var color = bg.Resolve(mode);
            if (color.A > 0)
                builder.FillRRect(new RRect(bounds, radius), Paint.Solid(color));
        }

        if (borderWidth > 0)
        {
            var color = borderColor.Resolve(mode);
            if (color.A > 0)
            {
                // Borders draw INSIDE the bounds (spec fence). The engine stroke is centered, so a
                // centered stroke on the half-width-deflated shape covers exactly [0, w] inward.
                builder.StrokeRRect(
                    new RRect(bounds.Inflate(-borderWidth / 2), radius.Deflate(borderWidth / 2)),
                    borderWidth,
                    Paint.Solid(color));
            }
        }
    }

    /// <summary>
    /// W4-pending placeholder: until the HarfBuzz/FreeType text stack lands, a text run renders as one
    /// soft bar per measured line (55% of the line box, text color at 30% alpha) — deterministic,
    /// verifies layout geometry in goldens, and is unmistakably a placeholder. Regenerating goldens
    /// when real glyphs arrive is by design.
    /// </summary>
    private static void EmitTextPlaceholder(LayoutNode node, Text text, IAppTheme theme, ThemeMode mode, DisplayListBuilder builder)
    {
        if (node.Text is not { } measurement) return;
        var color = (text.Color ?? theme.TextPrimary).Resolve(mode).WithOpacity(0.30f);
        var barHeight = measurement.LineHeight * 0.55f;

        for (var i = 0; i < measurement.Lines.Count; i++)
        {
            var line = measurement.Lines[i];
            if (line.Width <= 0) continue;
            var y = node.Bounds.Y + i * measurement.LineHeight + (measurement.LineHeight - barHeight) / 2;
            builder.FillRRect(
                new RRect(new Rect(node.Bounds.X, y, MathF.Min(line.Width, node.Bounds.Width), barHeight),
                    new CornerRadii(barHeight / 3)),
                Paint.Solid(color));
        }
    }

    /// <summary>Hit contract (spec §08): every interactive node exposes ≥ 48dp per side — visual bounds
    /// may be smaller; the hit rect expands symmetrically.</summary>
    private static Rect ExpandHitRect(Rect bounds)
    {
        var growX = MathF.Max(0, Touch.MinTarget - bounds.Width) / 2;
        var growY = MathF.Max(0, Touch.MinTarget - bounds.Height) / 2;
        return new Rect(bounds.X - growX, bounds.Y - growY, bounds.Width + growX * 2, bounds.Height + growY * 2);
    }
}
