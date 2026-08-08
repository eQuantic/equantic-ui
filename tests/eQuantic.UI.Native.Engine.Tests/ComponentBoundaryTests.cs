using eQuantic.UI.Native.Framework;
using eQuantic.UI.Primitives;
using FluentAssertions;

namespace eQuantic.UI.Native.Engine.Tests;

/// <summary>
/// The BOUNDARY on the NATIVE side. A window has no reload button: a component's throw travelled
/// out of measurement into the host, the frame never arrived, and the app was simply gone. Same
/// seam, same panel, same write-once surface the web realizer draws.
/// </summary>
public class ComponentBoundaryTests
{
    private static readonly ComponentContext Ctx = new(PhotonTheme.Instance);
    private static readonly LayoutContext Layout = new(PhotonTheme.Instance, ApproximateTextMeasurer.Instance);

    private sealed class BrokenCard : StatelessComponent
    {
        public override VisualNode Build(ComponentContext context) =>
            throw new InvalidOperationException("Sequence contains no elements");
    }

    [Fact]
    public void Measurement_survives_a_component_that_throws()
    {
        var layout = () => LayoutEngine.Layout(new BrokenCard(), 400, 300, Layout);

        layout.Should().NotThrow();
    }

    [Fact]
    public void The_contained_panel_takes_the_failed_subtree_s_place()
    {
        var node = LayoutEngine.Layout(new BrokenCard(), 400, 300, Layout);

        // It draws something, and it fills the width it was given — a panel nobody can see is the
        // silent failure this exists to end.
        node.Bounds.Width.Should().BeGreaterThan(0);
        node.Bounds.Height.Should().BeGreaterThan(0);
    }

    [Fact]
    public void A_failed_component_inside_a_row_leaves_its_siblings_measured()
    {
        var row = new Row(gap: 8);
        row.Add(new Text("before", TypeRole.BodyM));
        row.Add(new BrokenCard());
        row.Add(new Text("after", TypeRole.BodyM));

        var node = LayoutEngine.Layout(row, 600, 200, Layout);

        node.Children.Should().HaveCount(3);
        node.Children[0].Bounds.Width.Should().BeGreaterThan(0);
        node.Children[2].Bounds.Width.Should().BeGreaterThan(0);
    }

    [Fact]
    public void The_panel_is_built_from_the_vocabulary_so_both_targets_draw_the_same_thing()
    {
        var contained = new BrokenCard().BuildContained(Ctx);

        // Box → Row → [Icon, Flexible(Column)] — the shape ComponentBoundary.Describe produces; the
        // web realizer lowers this exact tree, which is what "write-once" means for a failure too.
        var box = contained.Should().BeOfType<Box>().Subject;
        var row = box.Child.Should().BeOfType<Row>().Subject;
        row.Children[0].Should().BeOfType<Icon>();
        row.Children[1].Should().BeOfType<Flexible>()
            .Which.Child.Should().BeOfType<Column>();
    }
}
