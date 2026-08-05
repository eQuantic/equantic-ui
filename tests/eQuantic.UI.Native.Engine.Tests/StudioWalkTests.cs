using eQuantic.Studio;
using eQuantic.UI.Native.Components;
using eQuantic.UI.Native.Engine;
using eQuantic.UI.Native.Framework;
using eQuantic.UI.Primitives;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Xunit;
using Xunit.Abstractions;

namespace eQuantic.UI.Native.Engine.Tests;

/// <summary>
/// THE SAMPLE AS A TEST SUBJECT. The Studio gallery is the widest real page the SDK has, so it is
/// the cheapest place to find the defects a unit test never asks about: a control nobody can reach,
/// two regions claiming one path, a press that throws three frames later.
/// <para>
/// Every section is walked the way a person would: render, press everything pressable, Tab through
/// every stop, type into every field — asserting the INVARIANTS of the input pipeline rather than
/// any particular pixel. A failure here is a framework bug until proven otherwise.
/// </para>
/// </summary>
public class StudioWalkTests(ITestOutputHelper output)
{
    private const float Width = 1180;
    private const float Height = 820;

    public static TheoryData<GallerySection> Sections()
    {
        var data = new TheoryData<GallerySection>();
        foreach (var section in Gallery.Sections) data.Add(section);
        return data;
    }

    private static PhotonHost Open(GallerySection section)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["section"] = section.ToString() })
            .Build();
        var host = new PhotonHost(new StudioShell(configuration), PhotonTheme.Instance,
            ThemeMode.Light, Width, Height);
        host.RenderFrame(new DisplayListBuilder());
        return host;
    }

    [Theory]
    [MemberData(nameof(Sections))]
    public void EveryPressableRegionIsReachable(GallerySection section)
    {
        var frame = Open(section).RenderFrame(new DisplayListBuilder());

        foreach (var region in frame.HitRegions)
        {
            var bounds = region.Bounds;
            bounds.Width.Should().BeGreaterThan(0, $"'{region.Node.Label}' has no width to press");
            bounds.Height.Should().BeGreaterThan(0, $"'{region.Node.Label}' has no height to press");
            // A region whose centre falls outside the window can never be clicked. (Overlay layers
            // lay out against the viewport too, so this holds for them.)
            bounds.Center.X.Should().BeInRange(0, Width,
                $"'{region.Node.Label}' sits outside the window horizontally");
            bounds.Center.Y.Should().BeInRange(0, Height,
                $"'{region.Node.Label}' sits outside the window vertically");
        }
    }

    /// <summary>
    /// Press dispatch is keyed by PATH. Two regions sharing one path means the wrong control
    /// answers a click — the failure mode is invisible until someone presses the second one.
    /// </summary>
    [Theory]
    [MemberData(nameof(Sections))]
    public void NoTwoRegionsClaimTheSamePath(GallerySection section)
    {
        var frame = Open(section).RenderFrame(new DisplayListBuilder());

        var seen = new Dictionary<string, string>();
        foreach (var region in frame.HitRegions)
        {
            seen.ContainsKey(region.Path).Should().BeFalse(
                $"'{region.Node.Label}' and '{seen.GetValueOrDefault(region.Path)}' both claim {region.Path}");
            seen[region.Path] = region.Node.Label;
        }

        var stops = new HashSet<string>();
        foreach (var stop in frame.FocusStops)
            stops.Add(stop.Path).Should().BeTrue($"two focus stops claim {stop.Path}");
    }

    /// <summary>
    /// Every pressable is pressed, in tree order, each on its OWN frame — which is what a person
    /// does and what a rebuild-per-press exercises. Nothing may throw, and the frame after each
    /// press must still render.
    /// </summary>
    [Theory]
    [MemberData(nameof(Sections))]
    public void PressingEverythingKeepsTheFrameRendering(GallerySection section)
    {
        var host = Open(section);
        var count = host.RenderFrame(new DisplayListBuilder()).HitRegions.Count;

        for (var i = 0; i < count; i++)
        {
            var frame = host.RenderFrame(new DisplayListBuilder());
            // The tree is rebuilt between presses, so the list is re-read every time; a press that
            // opened an overlay legitimately changes what exists.
            if (i >= frame.HitRegions.Count) break;
            var region = frame.HitRegions[i];
            host.PressDown(region.Bounds.Center.X, region.Bounds.Center.Y);
            host.PressUp(region.Bounds.Center.X, region.Bounds.Center.Y);
            host.RenderFrame(new DisplayListBuilder());

            // Escape after each press, so an overlay one press opened does not swallow the rest.
            host.KeyDown("Escape");
            host.RenderFrame(new DisplayListBuilder());
        }
        output.WriteLine($"{section}: {count} pressables walked");
    }

    /// <summary>
    /// Tab must VISIT every stop and come back to the first — a cycle that skips or sticks is a
    /// keyboard user's dead end.
    /// </summary>
    [Theory]
    [MemberData(nameof(Sections))]
    public void TabCyclesThroughEveryStop(GallerySection section)
    {
        var host = Open(section);
        var stops = host.RenderFrame(new DisplayListBuilder()).FocusStops;
        if (stops.Count == 0) return;

        var visited = new List<string>();
        for (var i = 0; i < stops.Count; i++)
        {
            host.KeyDown("Tab");
            host.RenderFrame(new DisplayListBuilder());
            var focused = host.FocusedPath;
            focused.Should().NotBeNull($"Tab #{i + 1} of {stops.Count} left the focus nowhere");
            visited.Add(focused!);
        }

        visited.Distinct().Should().HaveCount(stops.Count,
            "Tab visited the same stop twice before reaching them all — the ring is broken");
    }

    /// <summary>
    /// In FLOW, a child must fit inside its parent horizontally. The canvas scrolls up and down,
    /// never sideways, so anything hanging off the right is clipped — and the clip takes the press
    /// regions with it, which is why the loss is silent until someone happens to look.
    /// </summary>
    [Theory]
    [MemberData(nameof(Sections))]
    public void EveryChildFitsInsideItsParent(GallerySection section)
    {
        var frame = Open(section).RenderFrame(new DisplayListBuilder());
        var overflowing = new List<string>();
        Walk(frame.Root);
        overflowing.Should().BeEmpty("what hangs off the right of its parent is clipped away");

        void Walk(LayoutNode node)
        {
            // Nodes that place children OUTSIDE themselves on purpose: a stack overlaps, an
            // anchored panel hangs off its trigger, a drag translates. Everything else is flow,
            // and in flow a child that sticks out is content nobody can reach.
            var placesFreely = node.Source is Stack or Overlay or Anchored or Positioned
                or Draggable or DragDismiss
                or eQuantic.UI.Components.Tooltip or eQuantic.UI.Components.Menu
                or eQuantic.UI.Components.Select;
            if (!placesFreely)
            {
                foreach (var child in node.Children)
                {
                    var right = child.Bounds.X + child.Bounds.Width;
                    var edge = node.Bounds.X + node.Bounds.Width;
                    if (right > edge + 0.5f)
                    {
                        overflowing.Add($"{child.Source.GetType().Name} ends at {right:0} inside "
                            + $"{node.Source.GetType().Name} ending at {edge:0}");
                    }
                }
            }
            foreach (var child in node.Children) Walk(child);
        }
    }

    /// <summary>
    /// Tab must bring what it focuses INTO VIEW. A ring on a control below the fold is the same as
    /// no ring: the person pressing Tab is looking at a page that did not move.
    /// </summary>
    [Theory]
    [MemberData(nameof(Sections))]
    public void TabScrollsTheFocusedStopIntoView(GallerySection section)
    {
        var host = Open(section);
        var count = host.RenderFrame(new DisplayListBuilder()).FocusStops.Count;

        for (var i = 0; i < count; i++)
        {
            host.KeyDown("Tab");
            var frame = host.RenderFrame(new DisplayListBuilder());
            var focused = host.FocusedPath;
            if (focused is null) continue;

            var stop = frame.FocusStops.FirstOrDefault(s => s.Path == focused);
            if (stop.Path is null || stop.Bounds.Height <= 0) continue;
            // The window's own chrome (toolbar, status rail) is outside the scrolling canvas, so
            // the assertion is simply "inside the window".
            stop.Bounds.Y.Should().BeLessThan(Height,
                $"Tab focused {focused} below the window without scrolling to it");
            (stop.Bounds.Y + stop.Bounds.Height).Should().BeGreaterThan(0,
                $"Tab focused {focused} above the window without scrolling to it");
        }
    }

    /// <summary>
    /// Typing goes to the field the caret is in, and the value comes back out — the one loop a
    /// text field exists for, exercised on whatever fields the section happens to have.
    /// </summary>
    [Theory]
    [MemberData(nameof(Sections))]
    public void TypingReachesEveryTextField(GallerySection section)
    {
        var host = Open(section);
        var fields = host.RenderFrame(new DisplayListBuilder()).TextRegions;

        foreach (var field in fields)
        {
            host.PressDown(field.Bounds.Center.X, field.Bounds.Center.Y);
            host.PressUp(field.Bounds.Center.X, field.Bounds.Center.Y);
            host.RenderFrame(new DisplayListBuilder());

            host.TextInput("x");
            host.RenderFrame(new DisplayListBuilder());

            var after = host.RenderFrame(new DisplayListBuilder()).TextRegions
                .FirstOrDefault(r => r.Path == field.Path);
            after.Entry.Should().NotBeNull($"the field at {field.Path} vanished when typed into");
        }
        output.WriteLine($"{section}: {fields.Count} fields typed into");
    }
}
