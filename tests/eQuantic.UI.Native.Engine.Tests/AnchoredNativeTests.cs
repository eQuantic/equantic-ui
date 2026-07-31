using eQuantic.UI.Components;
using eQuantic.UI.Native.Components;
using eQuantic.UI.Native.Engine;
using eQuantic.UI.Primitives;
using FluentAssertions;

namespace eQuantic.UI.Native.Engine.Tests;

/// <summary>
/// Wave 3 — the anchored overlay on Photon: the panel realizes as a SYNTHETIC overlay layer
/// Positioned from the anchor's absolute bounds; the scrim is an ordinary full-viewport pressable
/// (tap-outside dismisses, topmost-last routing keeps panel items clickable). Menu/Select ride the
/// same machinery end-to-end through the host's press pipeline.
/// </summary>
public class AnchoredNativeTests
{
    [Fact]
    public void OpenPanel_PositionsBelowTheAnchor_WithTheGap()
    {
        var itemPressed = false;
        var anchor = new Box(new BoxStyle { Width = 100, Height = 40 });
        var panel = new Pressable(new Box(new BoxStyle { Width = 120, Height = 48 }), () => itemPressed = true);
        var root = new Column(gap: 0) { Width = SizeValue.Fill };
        root.Add(new Anchored(anchor, panel) { Open = true, OnDismiss = () => { } });

        var host = new PhotonHost(root, PhotonTheme.Instance, ThemeMode.Light, 360, 400);
        var frame = host.RenderFrame(new DisplayListBuilder());

        frame.HitRegions.Should().HaveCount(2, "the scrim and the panel item");
        var scrim = frame.HitRegions[0];
        scrim.Bounds.Width.Should().Be(360, "the scrim covers the viewport");
        scrim.Bounds.Height.Should().Be(400);

        var item = frame.HitRegions[^1];
        item.Bounds.X.Should().Be(0, "BottomStart aligns the panel to the anchor's start edge");
        item.Bounds.Y.Should().Be(40 + Anchored.DefaultGap, "the panel sits below the anchor plus the gap");
        itemPressed.Should().BeFalse();
    }

    [Fact]
    public void ScrimTap_Dismisses_AndPanelItemStillWins()
    {
        var dismissed = false;
        var itemPressed = false;
        var anchor = new Box(new BoxStyle { Width = 100, Height = 40 });
        var panel = new Pressable(new Box(new BoxStyle { Width = 120, Height = 48 }), () => itemPressed = true);
        var root = new Column(gap: 0) { Width = SizeValue.Fill };
        root.Add(new Anchored(anchor, panel) { Open = true, OnDismiss = () => dismissed = true });

        var host = new PhotonHost(root, PhotonTheme.Instance, ThemeMode.Light, 360, 400);
        var frame = host.RenderFrame(new DisplayListBuilder());

        // A tap INSIDE the panel routes to the item (topmost-last), never the scrim.
        var item = frame.HitRegions[^1];
        host.PressDown(item.Bounds.Center.X, item.Bounds.Center.Y);
        host.PressUp(item.Bounds.Center.X, item.Bounds.Center.Y);
        itemPressed.Should().BeTrue();
        dismissed.Should().BeFalse();

        // A tap OUTSIDE lands on the scrim — dismiss fires.
        host.PressDown(300, 380);
        host.PressUp(300, 380);
        dismissed.Should().BeTrue();
    }

    [Fact]
    public void MatchAnchorWidth_PanelRefusesToBeNarrower()
    {
        var anchor = new Box(new BoxStyle { Width = 200, Height = 40 });
        var panel = new Pressable(new Box(new BoxStyle { Height = 48, Width = SizeValue.Fill }), () => { });
        var root = new Column(gap: 0) { Width = SizeValue.Fill };
        root.Add(new Anchored(anchor, panel) { Open = true, MatchAnchorWidth = true });

        var host = new PhotonHost(root, PhotonTheme.Instance, ThemeMode.Light, 360, 400);
        var frame = host.RenderFrame(new DisplayListBuilder());

        frame.HitRegions[^1].Bounds.Width.Should().BeGreaterThanOrEqualTo(200);
    }

    [Fact]
    public void Menu_OpensOnTriggerTap_SelectsAndCloses()
    {
        var selected = -1;
        var menu = new Menu(new Text("Open menu", TypeRole.Label),
            new[] { new MenuItem("Rename"), new MenuItem("Delete") { Destructive = true } },
            onSelect: i => selected = i);
        var root = new Column(gap: 0) { Padding = EdgeInsets.All(Space.S3) };
        root.Add(menu);

        var host = new PhotonHost(root, PhotonTheme.Instance, ThemeMode.Light, 360, 400);
        var frame = host.RenderFrame(new DisplayListBuilder());
        frame.HitRegions.Should().HaveCount(1, "closed: only the trigger is pressable");

        // Tap the trigger → the panel opens (internal state re-render).
        var trigger = frame.HitRegions[0];
        host.PressDown(trigger.Bounds.Center.X, trigger.Bounds.Center.Y);
        host.PressUp(trigger.Bounds.Center.X, trigger.Bounds.Center.Y);
        frame = host.RenderFrame(new DisplayListBuilder());
        frame.HitRegions.Count.Should().Be(4, "trigger + scrim + two item rows");

        // Tap the second item → OnSelect(1) and the menu closes.
        var second = frame.HitRegions[^1];
        host.PressDown(second.Bounds.Center.X, second.Bounds.Center.Y);
        host.PressUp(second.Bounds.Center.X, second.Bounds.Center.Y);
        selected.Should().Be(1);
        host.RenderFrame(new DisplayListBuilder()).HitRegions.Should().HaveCount(1, "selection closes the panel");
    }

    [Fact]
    public void Select_TapsThroughTheContract_AndMatchesFieldWidth()
    {
        var changed = -1;
        var select = new Select(new[] { "Small", "Medium", "Large" }, selectedIndex: 0,
            onChanged: i => changed = i);
        var root = new Column(gap: 0) { Width = SizeValue.Fill, Padding = EdgeInsets.All(Space.S4) };
        root.Add(select);

        var host = new PhotonHost(root, PhotonTheme.Instance, ThemeMode.Light, 360, 400);
        var frame = host.RenderFrame(new DisplayListBuilder());
        var field = frame.HitRegions[0];

        host.PressDown(field.Bounds.Center.X, field.Bounds.Center.Y);
        host.PressUp(field.Bounds.Center.X, field.Bounds.Center.Y);
        frame = host.RenderFrame(new DisplayListBuilder());
        frame.HitRegions.Count.Should().Be(5, "field + scrim + three option rows");

        // The option panel matches the field width (the Select contract).
        frame.HitRegions[^1].Bounds.Width.Should().BeGreaterThanOrEqualTo(field.Bounds.Width);

        host.PressDown(frame.HitRegions[^1].Bounds.Center.X, frame.HitRegions[^1].Bounds.Center.Y);
        host.PressUp(frame.HitRegions[^1].Bounds.Center.X, frame.HitRegions[^1].Bounds.Center.Y);
        changed.Should().Be(2);
        host.RenderFrame(new DisplayListBuilder()).HitRegions.Should().HaveCount(1);
    }
}
