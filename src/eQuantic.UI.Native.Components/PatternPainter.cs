using eQuantic.UI.Native.Engine;
using eQuantic.UI.Primitives;

namespace eQuantic.UI.Native.Components;

/// <summary>
/// Photon's realizer of <see cref="Pattern"/> (#1937, #1939): every kind of the closed set placed in its
/// <see cref="Pattern.Area"/> (or the window), clipped to its <see cref="Pattern.ClipTo"/> with that
/// area's rounded corners, as CSS places and clips a background layer.
/// <list type="bullet">
/// <item>A grid is its hairlines, ordinary fills from the placing area's origin across the clip area.</item>
/// <item>A tile, vector or raster, is composited ONCE at its device size (<see cref="TileCompositor"/>,
/// kept by <see cref="PatternTileCache"/>) and drawn as ONE <see cref="DrawCommandKind.TiledTexture"/>,
/// however many tiles show. A host with no rasterizer draws no vector tile, and one with no image
/// loader no raster tile.</item>
/// </list>
/// It keeps nothing: one instance serves every frame, as <see cref="EmitVisitor"/> does.
/// </summary>
internal sealed class PatternPainter : IPatternVisitor<PatternLayer, Nothing>
{
    public static readonly PatternPainter Shared = new();

    /// <summary>The largest side of a tile's texture, in device pixels: a 4096-pixel square is the whole
    /// budget of <see cref="PatternTileCache"/>. A larger tile is drawn from a texture of this size,
    /// magnified by the filter.</summary>
    internal const int MaxTileSide = 4096;

    /// <summary>The most rules a grid draws on one axis, as many as a tile at its cap has pixels on a
    /// side. Past it, the rules are closer than any screen tells apart.</summary>
    internal const int MaxRulesPerAxis = MaxTileSide;

    private PatternPainter() { }

    public Nothing Visit(GridPattern pattern, PatternLayer layer)
    {
        var s = layer.State;
        var color = pattern.Color.Resolve(s.Mode, s.Contrast);
        if (color.A == 0) return Nothing.Value;
        var clip = layer.Placement[pattern.ClipTo];
        var bounds = clip.Rect;
        if (bounds.IsEmpty) return Nothing.Value;
        var placing = layer.Placement.PlacingArea(pattern, Window(pattern, s));

        var cell = pattern.Cell;
        // A rule as wide as its cell already fills it, as the web's hard-stop gradient does.
        var line = MathF.Min(pattern.LineWidth, cell);
        // Rules are sized to the clip area's rectangle, so only its rounded corners need a clip.
        var rounded = !clip.Radii.IsZero;
        if (rounded) s.Builder.PushClip(clip);
        Rules(s, color, cell, line, placing, bounds, vertical: true);
        Rules(s, color, cell, line, placing, bounds, vertical: false);
        if (rounded) s.Builder.PopClip();
        return Nothing.Value;
    }

    /// <summary>
    /// The rules of one axis across <paramref name="bounds"/>, the vertical ones or the horizontal: from
    /// the origin of <paramref name="placing"/>, the placing area, and on every side of it, by index rather than by a running sum, so the
    /// hundredth rule is where the first one says. The range of indices is counted before the first
    /// rule, so the loop ends whatever the cell. An axis with more rules than
    /// <see cref="MaxRulesPerAxis"/>, or whose rules fill their cells, is ONE fill of the colour at the
    /// share of each cell its rule covers, which is what so many rules average to on any screen.
    /// </summary>
    private static void Rules(in EmitState s, Color color, float cell, float line, Rect placing, Rect bounds,
        bool vertical)
    {
        var (at0, start, end) = vertical ? (placing.X, bounds.Left, bounds.Right) : (placing.Y, bounds.Top, bounds.Bottom);
        var first = Math.Floor(((double)start - line - at0) / cell);
        var count = Math.Ceiling(((double)end - at0) / cell) - first;
        if (!(count > 0)) return;
        if (line >= cell || count > MaxRulesPerAxis)
        {
            s.Builder.FillRect(bounds, Paint.Solid(color.WithOpacity(line / cell)));
            return;
        }
        var paint = Paint.Solid(color);
        for (var i = 0; i < (int)count; i++)
        {
            var at = at0 + (float)(first + i) * cell;
            if (at >= end) break;
            var from = MathF.Max(at, start);
            var to = MathF.Min(at + line, end);
            if (!(to > from)) continue;
            s.Builder.FillRect(vertical
                ? Rect.FromLTRB(from, bounds.Top, to, bounds.Bottom)
                : Rect.FromLTRB(bounds.Left, from, bounds.Right, to), paint);
        }
    }

    public Nothing Visit(DrawingPattern pattern, PatternLayer layer)
    {
        var s = layer.State;
        if (s.Motion.IconRasterizer is not { } rasterizer || pattern.Artwork.IsEmpty || pattern.Opacity <= 0)
            return Nothing.Value;
        var clip = layer.Placement[pattern.ClipTo];
        var placing = layer.Placement.PlacingArea(pattern, Window(pattern, s));
        if (!TryTile(pattern, placing, pattern.Artwork.Aspect, s.Builder.CurrentTransform,
                out var tiling, out var width, out var height))
            return Nothing.Value;

        var print = ArtworkFingerprint.Of(pattern.Artwork);
        // The tint reaches the pixels only through currentColor, so artwork in its own colours is one tile
        // in every mode.
        var tint = print.Inherits ? (pattern.Tint ?? s.Theme.TextPrimary).Resolve(s.Mode, s.Contrast) : default;
        var key = new PatternTileKey(print.Hash, null, width, height, tint);
        var cache = s.Motion.PatternCache ?? PatternTileCache.Shared;
        if (!cache.TryGet(key, out var tile))
        {
            tile = TileCompositor.Shared.Drawing(rasterizer, pattern.Artwork, width, height, tint);
            cache.Add(key, tile);
        }
        Draw(s, clip, tile, tiling, pattern.Opacity);
        return Nothing.Value;
    }

    public Nothing Visit(ImagePattern pattern, PatternLayer layer)
    {
        var s = layer.State;
        if (s.Motion.ImageLoader is null) return Nothing.Value;
        // The dark artwork in dark mode, as the web's toggles choose it.
        var source = s.Mode == ThemeMode.Dark && pattern.DarkSource is { } dark ? dark : pattern.Source;
        if (s.Motion.Decoded(source) is not { Width: > 0, Height: > 0 } decoded) return Nothing.Value;
        var clip = layer.Placement[pattern.ClipTo];
        var placing = layer.Placement.PlacingArea(pattern, Window(pattern, s));
        if (!TryTile(pattern, placing, decoded.Width / (float)decoded.Height, s.Builder.CurrentTransform,
                out var tiling, out var width, out var height))
            return Nothing.Value;

        var key = new PatternTileKey(0, source, width, height, default);
        var cache = s.Motion.PatternCache ?? PatternTileCache.Shared;
        if (!cache.TryGet(key, out var tile))
        {
            tile = TileCompositor.Shared.Raster(decoded, width, height);
            cache.Add(key, tile);
        }
        Draw(s, clip, tile, tiling, 1);
        return Nothing.Value;
    }

    /// <summary>One tile layer: the tiles over the clip area's rectangle, inside its rounded corners.</summary>
    private static void Draw(in EmitState s, RRect clip, TextureData? tile, in Tiling tiling, float opacity)
    {
        if (tile is null || clip.Rect.IsEmpty) return;
        var rounded = !clip.Radii.IsZero;
        if (rounded) s.Builder.PushClip(clip);
        s.Builder.TiledTexture(clip.Rect, tile, tiling, opacity);
        if (rounded) s.Builder.PopClip();
    }

    /// <summary>
    /// The window in the box's own space, for a pattern attached to it, or null: one attached to its box,
    /// or one under a transform, which CSS paints as attached to its box.
    /// </summary>
    private static Rect? Window(Pattern pattern, in EmitState s)
    {
        if (pattern.Attachment != TileAttachment.Window || s.UnderTransform) return null;
        if ((s.Builder.CurrentTransform.Invert() is not { } fromDevice)) return null;
        var window = new Rect(0, 0, s.Motion.ViewportW, s.Motion.ViewportH);
        return (s.Motion.Root * fromDevice).TransformBounds(window);
    }

    /// <summary>
    /// Where the tiles of <paramref name="pattern"/> stand in <paramref name="area"/>, and the texture's
    /// size in device pixels under <paramref name="transform"/>: the tile's size
    /// (<see cref="ArtworkPattern.SizeIn"/>), made whole across the area by <see cref="TileRepeat.Fitted"/>
    /// (an auto side scaled with it, CSS Backgrounds 3 §3.9), the anchored origin moved by the offset, and on
    /// each axis the repetition: every period, once, or spaced so the first and the last whole tiles touch
    /// the area's edges (§3.4). False when the tile has no size.
    /// </summary>
    private static bool TryTile(ArtworkPattern pattern, Rect area, float aspect, in Matrix2D transform,
        out Tiling tiling, out int width, out int height)
    {
        tiling = default;
        width = height = 0;
        var size = pattern.SizeIn(area.Size, aspect);
        var tileWidth = size.Width;
        var tileHeight = size.Height;
        var fitX = pattern.HorizontalRepeat == TileRepeat.Fitted;
        var fitY = pattern.VerticalRepeat == TileRepeat.Fitted;
        if (fitX && tileWidth > 0)
        {
            var fitted = area.Width / MathF.Max(1, MathF.Floor(area.Width / tileWidth + 0.5f));
            if (!fitY && pattern.Height.IsAuto && pattern.Fit is null) tileHeight *= fitted / tileWidth;
            tileWidth = fitted;
        }
        if (fitY && tileHeight > 0)
        {
            var fitted = area.Height / MathF.Max(1, MathF.Floor(area.Height / tileHeight + 0.5f));
            if (!fitX && pattern.Width.IsAuto && pattern.Fit is null) tileWidth *= fitted / tileHeight;
            tileHeight = fitted;
        }
        if (!(tileWidth > 0) || !(tileHeight > 0) || !float.IsFinite(tileWidth) || !float.IsFinite(tileHeight))
            return false;

        var (originX, periodX, repeatsX) = Axis(pattern.HorizontalRepeat, area.X, area.Width, tileWidth,
            pattern.Anchor.X, pattern.Offset.X);
        var (originY, periodY, repeatsY) = Axis(pattern.VerticalRepeat, area.Y, area.Height, tileHeight,
            pattern.Anchor.Y, pattern.Offset.Y);
        tiling = new Tiling(new Point(originX, originY), new Size(periodX, periodY),
            new Size(tileWidth, tileHeight), repeatsX, repeatsY);

        // The texture holds the tile at its device size, rounded up: the period stays exact, and the tile is
        // resampled by less than a texel across it.
        var scaleX = MathF.Sqrt(transform.M11 * transform.M11 + transform.M12 * transform.M12);
        var scaleY = MathF.Sqrt(transform.M21 * transform.M21 + transform.M22 * transform.M22);
        width = DeviceSide(tileWidth * scaleX);
        height = DeviceSide(tileHeight * scaleY);
        return width > 0 && height > 0;
    }

    /// <summary>One axis of the repetition: where cell 0 starts, the period, and whether it repeats.</summary>
    private static (float Origin, float Period, bool Repeats) Axis(TileRepeat repeat, float start, float length,
        float tile, float anchor, float offset)
    {
        var anchored = start + anchor * (length - tile) + offset;
        if (repeat == TileRepeat.Spaced)
        {
            // As many whole tiles as fit, with a hair of room for the float that says exactly n.
            var fits = MathF.Floor(length / tile + 1e-4f);
            // Two or more: the first at the start, the last at the end, the position ignored (§3.4).
            return fits >= 2 ? (start, tile + (length - fits * tile) / (fits - 1), true) : (anchored, tile, false);
        }
        return (anchored, tile, repeat != TileRepeat.Once);
    }

    /// <summary>A tile's side in device pixels, rounded up past a rounding's hair and never past the cap.</summary>
    private static int DeviceSide(float device) =>
        float.IsFinite(device) && device > 0 ? Math.Clamp((int)MathF.Ceiling(device - 1e-3f), 1, MaxTileSide) : 0;
}
