using eQuantic.UI.Native.Components;
using eQuantic.UI.Native.Engine;
using eQuantic.UI.Native.Framework;
using eQuantic.UI.Primitives;
using FluentAssertions;
using Xunit;

namespace eQuantic.UI.Native.Engine.Tests;

/// <summary>
/// Artwork with a RUN of colours, on Photon. The engine already fills glyph coverage with a
/// gradient (that is how gradient text draws), so a gradient-filled shape is the same alpha mask it
/// always was, tinted with a paint instead of a colour — no new command, no new shader.
/// </summary>
public class VectorGradientPhotonTests
{
    private static VectorDrawing Sky() => SvgDocument.Parse(
        "<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 100 100\">"
        + "<linearGradient id=\"sky\" x1=\"0\" y1=\"0\" x2=\"0\" y2=\"1\">"
        + "<stop offset=\"0\" stop-color=\"#4facfe\"/><stop offset=\"1\" stop-color=\"#00f2fe\"/>"
        + "</linearGradient>"
        + "<rect x=\"20\" y=\"0\" width=\"60\" height=\"100\" fill=\"url(#sky)\"/>"
        + "</svg>");

    private static DrawCommand Textured(VisualNode root, float width, float height)
    {
        var textured = new List<DrawCommand>();
        foreach (var command in Frame(root, width, height).Commands)
            if (command.Kind == DrawCommandKind.Texture) textured.Add(command);
        textured.Should().ContainSingle("the artwork is one shape, so it is one mask");
        return textured[0];
    }

    private static DisplayList Frame(VisualNode root, float width, float height)
    {
        var host = new PhotonHost(root, PhotonTheme.Instance, ThemeMode.Light, width, height)
        {
            IconRasterizer = new SquareRasterizer(),
        };
        var builder = new DisplayListBuilder();
        host.RenderFrame(builder);
        return builder.Build();
    }

    /// <summary>
    /// The run reaches the display list AS a run, with its ends and an axis that follows the box the
    /// artwork was given — not a flat colour picked from one end, which is what "supporting"
    /// gradients usually means.
    /// </summary>
    [Fact]
    public void TheRunReachesTheDisplayListWithItsAxis()
    {
        var textured = Textured(new Drawing(Sky(), 200, 100), 400, 200);
        textured.Paint.Kind.Should().Be(PaintKind.LinearGradient);
        textured.Paint.Color.Should().Be(Color.FromRgb(0x4f, 0xac, 0xfe));
        textured.Paint.EndColor.Should().Be(Color.FromRgb(0x00, 0xf2, 0xfe));

        // The default units are fractions of the SHAPE's box: the rect covers 20..80 of a 100-wide
        // viewBox drawn 200 wide, so the run starts 40dp in and spans the full height.
        textured.Paint.GradientStart.X.Should().BeApproximately(40, 0.01f);
        textured.Paint.GradientStart.Y.Should().BeApproximately(0, 0.01f);
        textured.Paint.GradientEnd.Y.Should().BeApproximately(100, 0.01f);
    }

    /// <summary>A flat fill is still a flat fill: the gradient path must not reach for a run that
    /// is not there.</summary>
    [Fact]
    public void AFlatFillStaysFlat()
    {
        var flat = SvgDocument.Parse(
            "<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 10 10\">"
            + "<rect width=\"10\" height=\"10\" fill=\"#123456\"/></svg>");

        var textured = Textured(new Drawing(flat, 100, 100), 200, 200);
        textured.Paint.Kind.Should().Be(PaintKind.Solid);
        textured.Paint.Color.Should().Be(Color.FromRgb(0x12, 0x34, 0x56));
    }

    /// <summary>A rasterizer that answers every glyph with an opaque square: this suite is about
    /// the PAINT that fills the coverage, not about the coverage.</summary>
    private sealed class SquareRasterizer : IIconRasterizer
    {
        public TextRaster? Rasterize(IconGlyph glyph, float widthDp, float heightDp, float scale)
        {
            var width = Math.Max((int)MathF.Round(widthDp * scale), 1);
            var height = Math.Max((int)MathF.Round(heightDp * scale), 1);
            var coverage = new byte[width * height];
            Array.Fill(coverage, (byte)255);
            return new TextRaster(width, height, coverage);
        }
    }
}
