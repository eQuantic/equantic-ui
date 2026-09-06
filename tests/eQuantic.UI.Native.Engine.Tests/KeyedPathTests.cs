using eQuantic.UI.Native.Components;
using eQuantic.UI.Native.Engine;
using eQuantic.UI.Primitives;
using FluentAssertions;
using Xunit;

namespace eQuantic.UI.Native.Engine.Tests;

/// <summary>
/// A path is identity on Photon — focus, hover, scroll offsets and a drag in flight are all
/// remembered by it — and a child that says <see cref="VisualNode.Key"/> is identified by the key,
/// not by where it happens to stand among its siblings this frame. The web's keyed reconciliation,
/// arriving on the second realizer through the same one property.
/// </summary>
public class KeyedPathTests
{
    private static readonly IAppTheme Theme = PhotonTheme.Instance;

    private static IReadOnlyList<HitRegion> Regions(VisualNode root)
    {
        var host = new PhotonHost(root, Theme, ThemeMode.Light, 400, 400);
        return host.RenderFrame(new DisplayListBuilder()).HitRegions;
    }

    private static Pressable Button(string label, string? key = null) =>
        new(new Text(label, TypeRole.BodyM, Theme.TextPrimary), () => { }) { Label = label, Key = key };

    private static string PathOf(VisualNode root, string label) =>
        Regions(root).Single(r => r.Node.Label == label).Path;

    [Fact]
    public void AKeyedChild_KeepsItsPath_WhateverItsSiblingsDo()
    {
        // The window of a virtualized list, two frames apart: a spacer and a row gone from the top,
        // a row arrived at the bottom. "two" moved from the third position to the first.
        var before = new Column();
        before.Add(Spacer.Fixed(28));
        before.Add(Button("one", "1"));
        before.Add(Button("two", "2"));
        var after = new Column();
        after.Add(Button("two", "2"));
        after.Add(Button("three", "3"));

        PathOf(after, "two").Should().Be(PathOf(before, "two"));
        PathOf(before, "two").Should().Be("r/[2]", "the key is the segment, spelled so it cannot be a position");
    }

    [Fact]
    public void AnUnkeyedChild_IsNamedByItsPosition()
    {
        var before = new Column();
        before.Add(Spacer.Fixed(28));
        before.Add(Button("one"));
        var after = new Column();
        after.Add(Button("one"));

        PathOf(before, "one").Should().Be("r/1");
        PathOf(after, "one").Should().Be("r/0", "without a key the position is all there is");
    }

    [Fact]
    public void KeyedAndPositional_Siblings_NeverCollide()
    {
        // A key that reads like an index still cannot be confused with one.
        var column = new Column();
        column.Add(Button("first"));
        column.Add(Button("second", "0"));

        var regions = Regions(column);
        regions.Select(r => r.Path).Should().OnlyHaveUniqueItems();
        regions.Single(r => r.Node.Label == "second").Path.Should().Be("r/[0]");
    }

    /// <summary>
    /// Keys are generated as often as they are written — a row id, a heading slug, a URL — so one
    /// may carry a '/', which is the path's own separator. The segment escapes it rather than
    /// letting it read as a boundary, and the escape is injective: the two keys below differ by
    /// exactly the escape's own spelling and must not meet.
    /// </summary>
    [Fact]
    public void AKeyCarryingTheSeparator_StaysOneSegment_AndCollidesWithNothing()
    {
        var slashed = new Column();
        slashed.Add(Button("slashed", "docs/intro"));
        var path = PathOf(slashed, "slashed");
        path.Should().Be("r/[docs~sintro]", "the separator is spelled, never emitted");
        path.Split('/').Should().HaveCount(2, "one parent, one child — not three");

        // The literal escape sequence as a key, and the key it encodes, must stay distinct.
        var literal = new Column();
        literal.Add(Button("literal", "docs~sintro"));
        PathOf(literal, "literal").Should().NotBe(path);

        var tilde = new Column();
        tilde.Add(Button("tilde", "a~b"));
        PathOf(tilde, "tilde").Should().Be("r/[a~~b]");
    }

    [Fact]
    public void TheKey_ReachesEveryContainer_ThatOrdersSiblings()
    {
        // Row, Stack and Grid place siblings by index too; the key takes the segment in each.
        var row = new Row(gap: 0);
        row.Add(Spacer.Fixed(10));
        row.Add(Button("in-row", "r"));
        PathOf(row, "in-row").Should().Be("r/[r]");

        var stack = new Stack();
        stack.Add(Spacer.Fixed(10));
        stack.Add(Button("in-stack", "s"));
        PathOf(stack, "in-stack").Should().Be("r/[s]");

        var flexed = new Row(gap: 0);
        flexed.Add(Spacer.Fixed(10));
        flexed.Add(new Flexible(Button("in-flex")) { Key = "f" });
        PathOf(flexed, "in-flex").Should().Be("r/[f]/0", "a Flexible is the sibling; its child sits inside it");
    }
}
