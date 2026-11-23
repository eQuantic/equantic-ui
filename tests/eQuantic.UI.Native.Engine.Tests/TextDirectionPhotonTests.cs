using System.Globalization;
using eQuantic.UI.Native.Components;
using eQuantic.UI.Native.Engine;
using eQuantic.UI.Primitives;
using FluentAssertions;
using Xunit;

namespace eQuantic.UI.Native.Engine.Tests;

/// <summary>
/// Photon reads the direction a component's text runs in as the web does (#1568): the nearest
/// <see cref="Directionality"/> above its position, or its UI culture's, and a component that read it
/// builds again in the frame after it changes. Photon still lays out left to right; this is what lets a
/// component that asks the question answer the same on both targets.
/// </summary>
public class TextDirectionPhotonTests
{
    private sealed class Reader(List<string> log, string name) : StatelessComponent
    {
        public override VisualNode Build(ComponentContext context)
        {
            log.Add($"{name} {context.TextDirection}");
            return new Text(name, TypeRole.BodyM);
        }
    }

    private sealed class Holder(VisualNode child) : StatefulComponent
    {
        private TextDirection _direction = TextDirection.LeftToRight;

        public void Flip() => SetState(() => _direction = TextDirection.RightToLeft);

        public override VisualNode Build(ComponentContext context) => new Directionality(_direction, child);
    }

    private static void Frame(PhotonHost host) => host.RenderFrame(new DisplayListBuilder());

    [Fact]
    public void AComponent_ReadsTheNearestDirectionality_OrItsCulturesInAFrame()
    {
        var before = CultureInfo.CurrentUICulture;
        CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("ar");
        try
        {
            var log = new List<string>();
            var holder = new Holder(new Reader(log, "inside"));
            var host = new PhotonHost(new Column { new Reader(log, "page"), holder }, PhotonTheme.Instance,
                ThemeMode.Light, 300, 200);
            Frame(host);
            log.Should().Equal("page RightToLeft", "inside LeftToRight");

            log.Clear();
            holder.Flip();
            Frame(host);
            log.Should().Contain("inside RightToLeft", "the frame after the change builds what read it")
                .And.NotContain("inside LeftToRight");
        }
        finally
        {
            CultureInfo.CurrentUICulture = before;
        }
    }
}
