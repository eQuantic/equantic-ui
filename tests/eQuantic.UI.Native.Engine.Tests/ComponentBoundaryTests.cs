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

    /// <summary>
    /// The OTHER way a component fails to produce a subtree, and the one that used to walk past this
    /// boundary: it never throws, so nothing catches it — measurement expands what Build returned,
    /// which is the component again, forever. A <see cref="StackOverflowException"/> cannot be
    /// caught in .NET, so on a window it is not a missing card, it is a missing app.
    /// </summary>
    private sealed class OuroborosCard : StatelessComponent
    {
        public override VisualNode Build(ComponentContext context) => this;
    }

    private sealed class PingCard : StatelessComponent
    {
        public override VisualNode Build(ComponentContext context) => new PongCard();
    }

    private sealed class PongCard : StatelessComponent
    {
        public override VisualNode Build(ComponentContext context) => new PingCard();
    }

    [Fact]
    public void Measurement_survives_a_component_that_builds_itself()
    {
        var layout = () => LayoutEngine.Layout(new OuroborosCard(), 400, 300, Layout);

        layout.Should().NotThrow();
        LayoutEngine.Layout(new OuroborosCard(), 400, 300, Layout).Bounds.Width.Should().BeGreaterThan(0);
    }

    [Fact]
    public void Measurement_survives_two_components_that_build_each_other()
    {
        // Neither instance ever repeats, so nothing watching for a component meeting ITSELF would
        // see this one.
        var layout = () => LayoutEngine.Layout(new PingCard(), 400, 300, Layout);

        layout.Should().NotThrow();
    }

    [Fact]
    public void A_cyclic_component_inside_a_row_leaves_its_siblings_measured()
    {
        var row = new Row(gap: 8);
        row.Add(new Text("before", TypeRole.BodyM));
        row.Add(new OuroborosCard());
        row.Add(new Text("after", TypeRole.BodyM));

        var node = LayoutEngine.Layout(row, 600, 200, Layout);

        node.Children.Should().HaveCount(3);
        node.Children[0].Bounds.Width.Should().BeGreaterThan(0);
        node.Children[2].Bounds.Width.Should().BeGreaterThan(0);
    }

    [Fact]
    public void A_cycle_is_tallied_exactly_as_a_throw_is()
    {
        ComponentBoundary.ClearContained();

        LayoutEngine.Layout(new OuroborosCard(), 400, 300, Layout);

        // A host that refuses to exit zero while the tally is non-empty catches a cycle the same way
        // it catches a throw — which is the whole reason the bound lives at the boundary and not in
        // the layout engine.
        ComponentBoundary.Contained.Should().Contain("OuroborosCard");
        ComponentBoundary.ClearContained();
    }

    [Fact]
    public void The_min_content_pass_contains_a_cycle_too()
    {
        // `MeasureComponent` is not the only door either. A row that has to SHRINK asks every
        // shrinkable child for its min-content width, and that walk expands components on its own,
        // through a different call site — so it needs its own regression or the two doors drift.
        var row = new Row(gap: 8) { Width = SizeValue.Fixed(80) };
        row.Add(new Text("a line far too long to fit inside eighty pixels of row", TypeRole.BodyM));
        row.Add(new OuroborosCard());

        var layout = () => LayoutEngine.Layout(row, 80, 200, Layout);

        layout.Should().NotThrow();
    }

    [Fact]
    public void An_ordinary_chain_of_components_is_not_a_cycle()
    {
        // The bound is for what never terminates; a component building a component is ordinary
        // composition and must be untouched by it.
        //
        // CLEARED FIRST, and the reason is the assertion below: `Contained` is ambient state scoped
        // to the RUN, not to this render, and the cyclic tests in this class fill it. Without this
        // the result depends on which test ran before — a green assertion that a neighbour can turn
        // red is exactly the instrument this epic exists to remove.
        ComponentBoundary.ClearContained();

        var node = LayoutEngine.Layout(new WrapperCard(depth: 12), 400, 300, Layout);

        node.Bounds.Width.Should().BeGreaterThan(0);
        ComponentBoundary.Contained.Should().BeEmpty();
    }

    private sealed class WrapperCard(int depth) : StatelessComponent
    {
        public override VisualNode Build(ComponentContext context) =>
            depth <= 0 ? new Text("bottom", TypeRole.BodyM) : new WrapperCard(depth - 1);
    }
}
