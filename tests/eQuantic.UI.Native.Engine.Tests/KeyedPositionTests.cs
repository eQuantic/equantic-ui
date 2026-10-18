using eQuantic.UI.Native.Components;
using eQuantic.UI.Native.Engine;
using eQuantic.UI.Primitives;
using FluentAssertions;
using Xunit;

namespace eQuantic.UI.Native.Engine.Tests;

/// <summary>
/// A child that says <see cref="VisualNode.Key"/> keeps its position whatever its siblings do, and
/// the position is what focus, hover, a press, a scroll offset and a drag in flight follow (#976).
/// The web's keyed reconciliation, arriving on the second realizer through the same one property.
/// <para>
/// These were path tests, and the path spelled a keyed child's segment as <c>[key]</c>, escaped a
/// <c>/</c> inside a key so it could not read as a boundary, and kept a key that looks like an index
/// apart from the index. With the path gone, a key is compared as the string it is and nothing is
/// spelled, so what is left to prove is the identity itself.
/// </para>
/// </summary>
public class KeyedPositionTests
{
    private static readonly IAppTheme Theme = PhotonTheme.Instance;

    /// <summary>A root that shows one tree and then the other, as a list window that slides does.</summary>
    private sealed class Slide(VisualNode before, VisualNode after) : StatefulComponent
    {
        private bool _slid;

        public void Next() => SetState(() => _slid = true);

        public override VisualNode Build(ComponentContext context) => _slid ? after : before;
    }

    private static Pressable Button(string label, string? key = null) =>
        new(new Text(label, TypeRole.BodyM, Theme.TextPrimary), () => { }) { Label = label, Key = key };

    /// <summary>The position of the control labelled <paramref name="label"/> before the tree slides
    /// and after, in one host.</summary>
    private static (Element Before, Element After) PositionsOf(VisualNode before, VisualNode after, string label)
    {
        var slide = new Slide(before, after);
        var host = new PhotonHost(slide, Theme, ThemeMode.Light, 400, 400);
        var first = host.RenderFrame(new DisplayListBuilder()).HitRegions.Single(r => r.Node.Label == label).Element;
        slide.Next();
        var second = host.RenderFrame(new DisplayListBuilder()).HitRegions.Single(r => r.Node.Label == label).Element;
        return (first, second);
    }

    [Fact]
    public void AKeyedChild_KeepsItsPosition_WhateverItsSiblingsDo()
    {
        // The window of a virtualized list, two frames apart: a spacer and a row gone from the top,
        // a row arrived at the bottom. "two" moved from the third place to the first.
        var before = new Column();
        before.Add(Spacer.Fixed(28));
        before.Add(Button("one", "1"));
        before.Add(Button("two", "2"));
        var after = new Column();
        after.Add(Button("two", "2"));
        after.Add(Button("three", "3"));

        var (was, now) = PositionsOf(before, after, "two");
        now.Should().BeSameAs(was);
    }

    [Fact]
    public void AnUnkeyedChild_IsMatchedByItsPlace()
    {
        var before = new Column();
        before.Add(Spacer.Fixed(28));
        before.Add(Button("one"));
        var after = new Column();
        after.Add(Button("one"));

        var (was, now) = PositionsOf(before, after, "one");
        now.Should().NotBeSameAs(was, "without a key its place is all there is, and the place held a spacer");
    }

    [Fact]
    public void TheKey_ReachesEveryContainer_ThatOrdersSiblings()
    {
        // Row, Stack and a Flexible's place in a row match siblings by key too.
        var (rowWas, rowNow) = PositionsOf(Row(Spacer.Fixed(10), Button("in-row", "r")),
            Row(Spacer.Fixed(10), Spacer.Fixed(20), Button("in-row", "r")), "in-row");
        rowNow.Should().BeSameAs(rowWas);

        var (stackWas, stackNow) = PositionsOf(Stack(Spacer.Fixed(10), Button("in-stack", "s")),
            Stack(Spacer.Fixed(10), Spacer.Fixed(20), Button("in-stack", "s")), "in-stack");
        stackNow.Should().BeSameAs(stackWas);

        var (flexWas, flexNow) = PositionsOf(Row(Spacer.Fixed(10), new Flexible(Button("in-flex")) { Key = "f" }),
            Row(Spacer.Fixed(10), Spacer.Fixed(20), new Flexible(Button("in-flex")) { Key = "f" }), "in-flex");
        flexNow.Should().BeSameAs(flexWas, "a Flexible is the sibling; its child keeps the place inside it");
    }

    private static Row Row(params VisualNode[] children)
    {
        var row = new Row(gap: 0);
        foreach (var child in children) row.Add(child);
        return row;
    }

    private static Stack Stack(params VisualNode[] children)
    {
        var stack = new Stack();
        foreach (var child in children) stack.Add(child);
        return stack;
    }
}
