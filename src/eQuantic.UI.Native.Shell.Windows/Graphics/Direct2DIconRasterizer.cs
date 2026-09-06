using eQuantic.UI.Native.Framework;
using eQuantic.UI.Primitives;

namespace eQuantic.UI.Native.Shell.Windows.Graphics;

/// <summary>
/// The W4 icon rasterizer on Windows — Direct2D only (the zero third-party rule, the twin of the
/// Mac's CoreGraphics one): the shared <see cref="SvgPath"/> parser lowers the glyph's path data to
/// moves/lines/cubics, a path geometry fills (nonzero) or strokes (round caps/joins, the icon-pack
/// convention) into an A8 coverage bitmap at device scale. The transform scales viewBox units to
/// pixels and Direct2D scales stroke widths with it, so strokes stay in glyph units — exactly the
/// CTM arrangement on the Mac. v1 fences: single fill rule (nonzero — IconGlyph carries none),
/// per-glyph raster (no atlas packing yet).
/// </summary>
public sealed unsafe class Direct2DIconRasterizer : IIconRasterizer, IDisposable
{
    private readonly void* _d2d;
    private readonly void* _wic;
    private readonly void* _roundStroke;
    private bool _disposed;

    public Direct2DIconRasterizer()
    {
        Com.EnsureInitialized();
        _d2d = D2D.CreateFactory();
        _wic = Wic.CreateFactory();

        var round = new D2D.StrokeStyleProperties
        {
            StartCap = D2D.CapStyleRound,
            EndCap = D2D.CapStyleRound,
            DashCap = D2D.CapStyleRound,
            LineJoin = D2D.LineJoinRound,
            MiterLimit = 10,
            DashStyle = D2D.DashStyleSolid,
        };
        void* stroke;
        Com.Check(D2D.CreateStrokeStyle(_d2d, &round, null, 0, &stroke), "stroke style");
        _roundStroke = stroke;
    }

    public TextRaster? Rasterize(IconGlyph glyph, float widthDp, float heightDp, float scale)
    {
        var segments = SvgPath.Parse(glyph.Path);
        if (segments.Count == 0) return null;

        // viewBox "minX minY w h" → normalize to origin, scale units → pixels.
        var parts = glyph.ViewBox.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 4
            || !float.TryParse(parts[0], System.Globalization.CultureInfo.InvariantCulture, out var minX)
            || !float.TryParse(parts[1], System.Globalization.CultureInfo.InvariantCulture, out var minY)
            || !float.TryParse(parts[2], System.Globalization.CultureInfo.InvariantCulture, out var unitW)
            || !float.TryParse(parts[3], System.Globalization.CultureInfo.InvariantCulture, out var unitH)
            || unitW <= 0 || unitH <= 0) return null;

        var pxW = Math.Max(1, (int)MathF.Ceiling(widthDp * scale));
        var pxH = Math.Max(1, (int)MathF.Ceiling(heightDp * scale));

        void* geometry = null;
        try
        {
            geometry = Geometry(segments);
            if (geometry is null) return null;
            var shape = geometry;
            var stroke = glyph.Style == IconGlyphStyle.Stroke;
            var strokeWidth = glyph.StrokeWidth;
            var strokeStyle = _roundStroke;
            return CoverageCanvas.Draw(_d2d, _wic, pxW, pxH, 0, (target, brush) =>
            {
                var t = (void*)target;
                // viewBox units → pixels, each axis on its own scale, origin moved to zero first.
                var transform = D2D.Matrix3x2.TranslateThenScale(-minX, -minY, pxW / unitW, pxH / unitH);
                D2D.SetTransform(t, &transform);
                if (stroke) D2D.DrawGeometry(t, shape, (void*)brush, strokeWidth, strokeStyle);
                else D2D.FillGeometry(t, shape, (void*)brush, null);
            });
        }
        finally
        {
            Com.Release(ref geometry);
        }
    }

    /// <summary>The path as a Direct2D geometry — figures opened on every Move, closed on Close,
    /// left open (a stroke's open contour) otherwise.</summary>
    private void* Geometry(IReadOnlyList<PathSegment> segments)
    {
        void* geometry;
        Com.Check(D2D.CreatePathGeometry(_d2d, &geometry), "path geometry");
        void* sink = null;
        try
        {
            Com.Check(D2D.Open(geometry, &sink), "geometry sink");
            D2D.SetFillMode(sink, D2D.FillModeWinding);
            var open = false;
            foreach (var segment in segments)
            {
                switch (segment.Verb)
                {
                    case PathVerb.Move:
                        if (open) D2D.EndFigure(sink, D2D.FigureEndOpen);
                        D2D.BeginFigure(sink, new D2D.Point2F(segment.End.X, segment.End.Y), D2D.FigureBeginFilled);
                        open = true;
                        break;
                    case PathVerb.Line:
                        if (!open) { D2D.BeginFigure(sink, new D2D.Point2F(segment.End.X, segment.End.Y), D2D.FigureBeginFilled); open = true; break; }
                        D2D.AddLine(sink, new D2D.Point2F(segment.End.X, segment.End.Y));
                        break;
                    case PathVerb.Cubic:
                        if (!open) { D2D.BeginFigure(sink, new D2D.Point2F(segment.End.X, segment.End.Y), D2D.FigureBeginFilled); open = true; break; }
                        var bezier = new D2D.BezierSegment
                        {
                            Point1 = new D2D.Point2F(segment.C1.X, segment.C1.Y),
                            Point2 = new D2D.Point2F(segment.C2.X, segment.C2.Y),
                            Point3 = new D2D.Point2F(segment.End.X, segment.End.Y),
                        };
                        D2D.AddBezier(sink, &bezier);
                        break;
                    case PathVerb.Close:
                        if (open) D2D.EndFigure(sink, D2D.FigureEndClosed);
                        open = false;
                        break;
                }
            }
            if (open) D2D.EndFigure(sink, D2D.FigureEndOpen);
            Com.Check(D2D.Close(sink), "geometry close");
            return geometry;
        }
        catch
        {
            Com.Release(geometry);
            throw;
        }
        finally
        {
            Com.Release(ref sink);
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Com.Release(_roundStroke);
        Com.Release(_wic);
        Com.Release(_d2d);
    }
}
