using eQuantic.UI.Components;
using eQuantic.UI.Native.Components;
using eQuantic.UI.Native.Engine;
using eQuantic.UI.Primitives;
using FluentAssertions;
using Xunit;

namespace eQuantic.UI.Native.Engine.Tests;

/// <summary>
/// A Select's panel is as wide as its FIELD — not as wide as the window. Three layout rules had to
/// hold hands for this, and each is pinned separately below by the layers of the same scene:
/// Fill INHERITS indeterminacy (a Fill in a context that grants no width grants none), leftover
/// does not distribute on an indeterminate main axis (a Flexible spacer was swallowing the
/// viewport), and Min/Max REFLOW hands a clamped width down to children measured before the clamp.
/// </summary>
public class AnchorPanelWidthTests
{
    private static (PhotonHost Host, Rect Field) OpenSelect(float column = 550, float window = 1180)
    {
        var col = new Column(gap: 0) { Width = column };
        col.Add(new Select(["Checking ··· 4821", "Savings ··· 1190"], 0, _ => { }));
        var page = new Row(gap: 0) { Width = SizeValue.Fill };
        page.Add(col);

        var host = new PhotonHost(page, PhotonTheme.Instance, ThemeMode.Light, window, 820);
        host.RenderFrame(new DisplayListBuilder());
        var trigger = host.RenderFrame(new DisplayListBuilder()).HitRegions[0].Bounds;
        host.PressDown(trigger.Center.X, trigger.Center.Y);
        host.PressUp(trigger.Center.X, trigger.Center.Y);
        host.RenderFrame(new DisplayListBuilder());
        return (host, trigger);
    }

    [Fact]
    public void TheOptionsMatchTheFieldWidth_NotTheWindow()
    {
        var (host, field) = OpenSelect();
        var frame = host.RenderFrame(new DisplayListBuilder());

        // Overlay regions register after the page's. The scrim is deliberately viewport-wide —
        // the OPTIONS are what MatchAnchorWidth governs.
        var options = frame.HitRegions
            .Where(r => r.Path.StartsWith("ov") && r.Node.Label != "Dismiss")
            .ToArray();
        options.Should().NotBeEmpty();
        foreach (var option in options)
            option.Bounds.Width.Should().BeApproximately(field.Width, 1,
                "MatchAnchorWidth means the FIELD's width — a dropdown as wide as the window is a bug");
    }

    [Fact]
    public void AFlexibleSpacerInsideAHuggingPanel_DoesNotSwallowTheViewport()
    {
        // The exact inner shape of a selected option row: text + Flexible(Spacer) + icon, inside a
        // Fill box inside a hugging panel. The spacer must not claim the window's leftover.
        var row = new Row(gap: 0) { Width = SizeValue.Fill };
        row.Add(new Text("Checking", TypeRole.BodyM));
        row.Add(new Flexible(new Spacer()));
        row.Add(new Icon(Icons.Check, IconSize.Sm, PhotonTheme.Instance.TextPrimary));
        var option = new Box(new BoxStyle { Width = SizeValue.Fill, Height = 40 }, row);
        var panel = new Box(new BoxStyle { Clip = true }, option);
        var hugging = new Row(gap: 0);
        hugging.Add(panel);

        var builder = new DisplayListBuilder();
        var frame = PhotonRealizer.Realize(hugging, 1000, 100, PhotonTheme.Instance, ThemeMode.Light, builder);

        // The ROOT is the page's body and stretches to the window; the PANEL inside it hugs.
        frame.Root.Children[0].Bounds.Width.Should().BeLessThan(300,
            "the panel hugs its content; 1000 would mean the spacer measured against the viewport");
    }

    [Fact]
    public void TheBaseLayerRootIsTheBody_AnAutoRootStretchesToTheWindow()
    {
        // CSS block semantics: a padding-only root box is the page's body — its Fill child gets
        // the window, not an intrinsic zero. (Overlay layers stay shrink-to-fit; the Select test
        // above is what pins that side.)
        var content = new Box(new BoxStyle { Width = SizeValue.Fill, Height = 30 });
        var root = new Box(new BoxStyle { Padding = EdgeInsets.All(10) }, content);

        var frame = PhotonRealizer.Realize(root, 140, 120, PhotonTheme.Instance, ThemeMode.Light,
            new DisplayListBuilder());

        frame.Root.Bounds.Width.Should().Be(140);
        frame.Root.Children[0].Bounds.Width.Should().Be(120,
            "the body hands its width down; an empty page was the symptom of it hugging to zero");
    }

    [Fact]
    public void AMinWidthReflowsIntoTheChildrenMeasuredBeforeIt()
    {
        var inner = new Box(new BoxStyle { Width = SizeValue.Fill, Height = 10 });
        var clamped = new Box(new BoxStyle { MinWidth = 200 }, inner);
        var hugging = new Row(gap: 0);
        hugging.Add(clamped);

        var builder = new DisplayListBuilder();
        var frame = PhotonRealizer.Realize(hugging, 1000, 100, PhotonTheme.Instance, ThemeMode.Light, builder);

        var clampedNode = frame.Root.Children[0];
        clampedNode.Bounds.Width.Should().Be(200);
        clampedNode.Children[0].Bounds.Width.Should().Be(200,
            "the child measured before the clamp has to hear about the width the clamp decided");
    }
}
