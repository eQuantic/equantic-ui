using eQuantic.UI.Native.Components;
using eQuantic.UI.Native.Engine;
using eQuantic.UI.Native.Framework;
using eQuantic.UI.Primitives;
using FluentAssertions;
using Xunit;

namespace eQuantic.UI.Native.Engine.Tests;

/// <summary>
/// A Flexible in a line whose width its content decides takes, at most, what the line has left once its rigid items
/// and gaps are laid (#1256). The line's max-content probe offered it the whole room, so content that takes what it
/// is offered (a text entry) took it all and pushed the items beside it out: the Studio's search field, an entry
/// between its icon and its clear button in a hugging column, ran 88dp into the next grid column, and the next
/// column's stepper took the press meant for the clear button. The web writes a Flexible <c>min-width: 0</c>, which
/// shrinks it into the line, and several such flexibles give back what they overrun together, each by its shrink
/// weight times its size, as a browser shrinks flex items from their content (Flexbox §9.7).
/// </summary>
public class AFlexibleInAHuggingLineTests
{
    /// <summary>Builds a FRESH tree each time, the way a real component does.</summary>
    private sealed class Page(Func<VisualNode> build) : StatefulComponent
    {
        public override VisualNode Build(ComponentContext context) => build();
    }

    private static LayoutNode Measure(Func<VisualNode> root, float width = 600)
    {
        var host = new PhotonHost(new Page(root), PhotonTheme.Instance, ThemeMode.Light, width, 300);
        host.RenderFrame(new DisplayListBuilder());
        return host.RenderFrame(new DisplayListBuilder(), 1000).Root;
    }

    private static IEnumerable<LayoutNode> All(LayoutNode node)
    {
        yield return node;
        foreach (var child in node.Children)
            foreach (var found in All(child))
                yield return found;
    }

    private static Box Fixed(float width) => new(new BoxStyle { Width = width, Height = 20 });

    /// <summary>A cell of a two-column grid whose column hugs its content, as the Studio's Labelled does: the field
    /// fills the column, and its row holds <paramref name="items"/>.</summary>
    private static VisualNode Cell(params VisualNode[] items)
    {
        var row = new Row(gap: 10) { Cross = CrossAlign.Center };
        foreach (var item in items) row.Add(item);
        var field = new Box(new BoxStyle { Width = SizeValue.Fill, Height = 40, Padding = EdgeInsets.Symmetric(14, 0) }, row);
        var labelled = new Column(gap: 6) { Cross = CrossAlign.Start };
        labelled.Add(field);
        var grid = new Grid([GridTrack.Flex(), GridTrack.Flex()], gap: 16) { Width = SizeValue.Fill };
        grid.Add(labelled);
        grid.Add(Fixed(100));
        return grid;
    }

    [Fact]
    public void AnEntryBetweenTwoRigidItems_TakesWhatTheLineLeaves_AndTheLastItemStaysInTheCell()
    {
        var tree = Measure(() => Cell(Fixed(20), new Flexible(new TextEntry("rent", null) { Placeholder = "Search" }, 1), Fixed(48)));

        var cell = All(tree).First(node => node.Source is Column).Bounds;
        var row = All(tree).First(node => node.Source is Row).Bounds;
        var entry = All(tree).First(node => node.Source is Flexible).Bounds;

        cell.Width.Should().Be(292, "the column is the cell, half of 600 less the 16dp gap");
        row.Width.Should().Be(264, "the line is the field's content box, 292 less 14dp of padding on each side");
        entry.Width.Should().Be(264 - 20 - 48 - 2 * 10, "the entry takes what its icon, its button and two gaps leave");
        row.Right.Should().BeLessThanOrEqualTo(cell.Right - 14, "the clear button ends inside its field, inside its cell");
    }

    [Fact]
    public void TwoEntriesThatEachFitTheLine_GiveBackWhatTheyOverrunTogether()
    {
        var tree = Measure(() => Cell(
            new Flexible(new TextEntry("a", null) { Placeholder = "From" }, 1),
            new Flexible(new TextEntry("b", null) { Placeholder = "To" }, 1),
            Fixed(48)));

        var row = All(tree).First(node => node.Source is Row).Bounds;
        var entries = All(tree).Where(node => node.Source is Flexible).Select(node => node.Bounds.Width).ToList();

        entries.Should().HaveCount(2);
        entries.Sum().Should().BeApproximately(264 - 48 - 2 * 10, 0.5f, "the two share what the button and the gaps leave");
        entries[0].Should().BeApproximately(entries[1], 0.5f, "two alike shrink alike, each by its size");
        row.Width.Should().BeLessThanOrEqualTo(264);
    }

    [Fact]
    public void AShortLabel_KeepsItsOwnWidth()
    {
        var tree = Measure(() => Cell(Fixed(20), new Flexible(new Text("Hi", TypeRole.BodyM), 1), Fixed(48)));

        var label = All(tree).First(node => node.Source is Flexible).Bounds;
        label.Width.Should().BeLessThan(40, "a flexible's content contributes its own size where its line hugs, as before");
    }
}
