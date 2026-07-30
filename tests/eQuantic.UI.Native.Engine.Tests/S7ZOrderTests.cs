using eQuantic.UI.Native.Components;
using eQuantic.UI.Native.Engine;
using eQuantic.UI.Primitives;
using FluentAssertions;

namespace eQuantic.UI.Native.Engine.Tests;

/// <summary>Spec S7 on Photon: ZIndex reorders the Stack's PAINT (and hit) order — higher paints
/// later (on top); equal values keep declaration order.</summary>
public class S7ZOrderTests
{
    [Fact]
    public void ZIndex_ReordersPaint_HigherOnTop()
    {
        var stack = new Stack();
        stack.Add(new Positioned(Marker(0x30), top: 0) { ZIndex = 2 });   // declared first, paints LAST
        stack.Add(new Positioned(Marker(0x10), top: 0));                  // z 0 → paints first
        stack.Add(new Positioned(Marker(0x20), top: 0) { ZIndex = 1 });

        var host = new PhotonHost(stack, PhotonTheme.Instance, ThemeMode.Light, 100, 100);
        var builder = new DisplayListBuilder();
        host.RenderFrame(builder);

        var reds = new List<byte>();
        foreach (var command in builder.Build().Commands)
            if (command.Kind == DrawCommandKind.FillRRect && command.Paint.Color.R is 0x10 or 0x20 or 0x30)
                reds.Add(command.Paint.Color.R);

        reds.Should().Equal(new byte[] { 0x10, 0x20, 0x30 }, "paint order follows ZIndex ascending (topmost last)");
    }

    private static Box Marker(byte r) => new(new BoxStyle
    {
        Width = 40, Height = 40, Background = new ColorToken(Color.FromRgb(r, 0, 0)),
    });
}
