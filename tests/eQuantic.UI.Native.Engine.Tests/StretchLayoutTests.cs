using eQuantic.UI.Components;
using eQuantic.UI.Native.Components;
using eQuantic.UI.Native.Engine;
using eQuantic.UI.Native.Framework;
using eQuantic.UI.Primitives;
using FluentAssertions;
using Xunit;

namespace eQuantic.UI.Native.Engine.Tests;

/// <summary>
/// What `CrossAlign.Stretch` has to mean, which is the same thing `align-items: stretch` means on
/// the web realization of the same tree: the child is GIVEN the size, and lays its own contents out
/// inside it.
/// <para>
/// It used to be applied afterwards — the box was resized once its contents had already been placed
/// inside the smaller one. Everything looked right until something inside wanted the room: a
/// centred label sat against the left edge of its cell, a Row of actions aligned to End sat in the
/// middle of the dialog. The box was the right size and the contents had never heard about it.
/// </para>
/// </summary>
public class StretchLayoutTests
{
    private static LayoutNode Find(LayoutNode node, Func<LayoutNode, bool> match)
    {
        if (match(node)) return node;
        foreach (var child in node.Children)
        {
            var found = Find(child, match);
            if (found is not null) return found;
        }
        return null!;
    }

    private static LayoutNode TextNode(LayoutNode root, string content) =>
        Find(root, n => n.Source is Text t && t.PlainContent == content);

    private static LayoutNode Layout(VisualNode root, float width = 600, float height = 200)
    {
        var builder = new DisplayListBuilder();
        return PhotonRealizer.Realize(root, width, height, PhotonTheme.Instance, ThemeMode.Light, builder).Root;
    }

    [Fact]
    public void ACentredChildOfAStretchedRowIsActuallyCentred()
    {
        var column = new Column(gap: 0) { Width = 400 };
        var row = new Row(gap: 0) { Main = MainAlign.Center };
        row.Add(new Text("hi", TypeRole.BodyM));
        column.Add(row);

        var text = TextNode(Layout(column), "hi");
        var centre = text.Bounds.X + text.Bounds.Width / 2;
        centre.Should().BeApproximately(200, 1, "the row was given 400dp, so its centre is at 200");
    }

    [Fact]
    public void AndOneAlignedToTheEndSitsAtTheEnd()
    {
        var column = new Column(gap: 0) { Width = 400 };
        var row = new Row(gap: 0) { Main = MainAlign.End };
        row.Add(new Text("ok", TypeRole.BodyM));
        column.Add(row);

        var text = TextNode(Layout(column), "ok");
        (text.Bounds.X + text.Bounds.Width).Should().BeApproximately(400, 1,
            "a dialog's actions row is exactly this, and it was landing mid-card");
    }

    [Fact]
    public void StretchReachesTHROUGHaFlexible()
    {
        // A Flexible is layout-transparent. Before, it took the cell's width and handed its child
        // the child's own — which is what put every tab label against the left edge of its tab.
        var outer = new Row(gap: 0) { Width = 600 };
        var cell = new Column(gap: 0);
        var row = new Row(gap: 0) { Main = MainAlign.Center };
        row.Add(new Text("tab", TypeRole.BodyM));
        cell.Add(row);
        outer.Add(new Flexible(cell));

        var text = TextNode(Layout(outer), "tab");
        var centre = text.Bounds.X + text.Bounds.Width / 2;
        centre.Should().BeApproximately(300, 1);
    }

    [Fact]
    public void ATabLabelSitsOverItsOwnIndicator()
    {
        // The whole reason any of this was noticed. Three equal cells across 600dp: the middle
        // label belongs at 300, over the middle of its own tab.
        var tabs = new Tabs(["Overview", "Activity", "Reports"], 1);

        var text = TextNode(Layout(tabs, 600, 60), "Activity");
        var centre = text.Bounds.X + text.Bounds.Width / 2;
        centre.Should().BeApproximately(300, 1, "the second of three equal tabs is centred on 300");
    }

    [Fact]
    public void AHuggingParentStillHugs()
    {
        // The guard on the other side: stretch hands out a size the parent HAS. A parent measuring
        // itself from its content has none, and a child that took the maximum there would decide
        // the size of the thing that was supposed to measure it.
        var outer = new Row(gap: 0);              // hugs
        var inner = new Row(gap: 0) { Main = MainAlign.Center };
        inner.Add(new Text("tight", TypeRole.BodyM));
        outer.Add(inner);

        var root = Layout(outer);
        var text = TextNode(root, "tight");
        root.Bounds.Width.Should().BeLessThan(200, "the row still measures itself from its content");
        text.Bounds.X.Should().BeApproximately(0, 1, "with no slack there is nothing to centre in");
    }
}
