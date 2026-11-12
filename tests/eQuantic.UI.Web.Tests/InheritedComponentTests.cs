using eQuantic.UI.Primitives;
using FluentAssertions;

namespace eQuantic.UI.Web.Tests;

/// <summary>
/// Inherited components on the element engine (docs/ELEMENT-TREE-PLAN.md, 3.3 and 3.4, #1095): a value a
/// subtree reads through <see cref="ComponentContext.DependOn{T}"/> builds again the components that read
/// it when it changes, in the same frame, and nothing else; a stateful one hears it first; and the
/// frame's own values (the theme, the density, the type scale) are read the same way.
/// </summary>
public class InheritedComponentTests
{
    private static readonly ComponentContext Comfortable = new(PhotonTheme.Instance);
    private static readonly ComponentContext Compact = new(PhotonTheme.Instance, density: Density.Compact);

    private static Element Frame(BuildOwner owner, VisualNode root, bool rebuildAll = false, ComponentContext? context = null)
    {
        var element = owner.Build(root, context ?? Comfortable, WindowSizeClass.Compact, rebuildAll);
        owner.EndFrame();
        return element;
    }

    /// <summary>A value an app hands its subtree.</summary>
    private sealed class Score(int value, VisualNode child) : InheritedComponent(child)
    {
        public int Value { get; } = value;

        protected override bool UpdateShouldNotify(InheritedComponent old) => ((Score)old).Value != Value;
    }

    /// <summary>Another value, which a lookup for a <see cref="Score"/> passes over.</summary>
    private sealed class Caption(string text, VisualNode child) : InheritedComponent(child)
    {
        public string Text { get; } = text;

        protected override bool UpdateShouldNotify(InheritedComponent old) => ((Caption)old).Text != Text;
    }

    private sealed class Log
    {
        public List<string> Entries { get; } = new();

        public int Count(string entry) => Entries.Count(e => e == entry);
    }

    /// <summary>Reads the score through DependOn, so it builds when the score changes.</summary>
    private sealed class Reader(Log log) : StatelessComponent
    {
        public override VisualNode Build(ComponentContext context)
        {
            var score = context.DependOn<Score>();
            log.Entries.Add($"reader {score?.Value}");
            return new Text($"{score?.Value}", TypeRole.BodyM);
        }
    }

    /// <summary>Reads the score once, through Find.</summary>
    private sealed class Glance(Log log) : StatelessComponent
    {
        public override VisualNode Build(ComponentContext context)
        {
            log.Entries.Add($"glance {context.Find<Score>()?.Value}");
            return new Text("glance", TypeRole.BodyM);
        }
    }

    /// <summary>Reads nothing a subtree or a frame holds.</summary>
    private sealed class Bystander(Log log) : StatelessComponent
    {
        public override VisualNode Build(ComponentContext context)
        {
            log.Entries.Add("bystander");
            return new Text("bystander", TypeRole.BodyM);
        }
    }

    /// <summary>A stateful dependent, which says when it heard that a dependency changed.</summary>
    private sealed class Listener(Log log) : StatefulComponent
    {
        protected override void OnDependenciesChanged() => log.Entries.Add("changed");

        public override VisualNode Build(ComponentContext context)
        {
            log.Entries.Add($"listener {context.DependOn<Score>()?.Value}");
            return new Text("listener", TypeRole.BodyM);
        }
    }

    /// <summary>
    /// Holds a score over a subtree it builds ONCE, as Flutter's dependents are found: the score's
    /// position gets a new value and the same child, so only what reads the value can build again.
    /// </summary>
    private sealed class Holder(VisualNode child) : StatefulComponent
    {
        private int _value = 1;

        public bool Shown { get; private set; } = true;

        public void Set(int value) => SetState(() => _value = value);

        public void Hide() => SetState(() => Shown = false);

        public override VisualNode Build(ComponentContext context) =>
            new Score(_value, Shown ? child : new Text("gone", TypeRole.BodyM));
    }

    [Fact]
    public void ADependent_BuildsAgainWhenItsValueChanges_AndNothingBesideItDoes()
    {
        var log = new Log();
        var holder = new Holder(new Column { new Reader(log), new Bystander(log), new Glance(log) });
        var owner = ComponentBoundary.Owner();
        Frame(owner, holder, rebuildAll: true);
        log.Entries.Should().Equal("reader 1", "bystander", "glance 1");

        log.Entries.Clear();
        holder.Set(2);
        Frame(owner, holder);

        log.Entries.Should().Equal(["reader 2"],
            "the reader depends on the score, and the bystander and the glance, which read it once, do not");
    }

    [Fact]
    public void AValueThatSaysNothingChanged_BuildsNoDependent()
    {
        var log = new Log();
        var holder = new Holder(new Reader(log));
        var owner = ComponentBoundary.Owner();
        Frame(owner, holder, rebuildAll: true);

        log.Entries.Clear();
        holder.Set(1);
        Frame(owner, holder);

        log.Entries.Should().BeEmpty("UpdateShouldNotify answered false");
    }

    [Fact]
    public void AStatefulDependent_HearsTheChangeBeforeItBuilds()
    {
        var log = new Log();
        var holder = new Holder(new Listener(log));
        var owner = ComponentBoundary.Owner();
        Frame(owner, holder, rebuildAll: true);
        log.Entries.Should().Equal("listener 1");

        log.Entries.Clear();
        holder.Set(3);
        Frame(owner, holder);

        log.Entries.Should().Equal("changed", "listener 3");
    }

    [Fact]
    public void ALookup_FindsTheNearestOfItsType_PassingOverAnyOther()
    {
        var log = new Log();
        var owner = ComponentBoundary.Owner();
        Frame(owner, new Score(1, new Caption("inner", new Score(2, new Caption("innermost", new Reader(log))))), rebuildAll: true);

        log.Entries.Should().Equal("reader 2");
    }

    [Fact]
    public void ADependentThatLeft_IsNotBuiltWhenTheValueChanges()
    {
        var log = new Log();
        var holder = new Holder(new Reader(log));
        var owner = ComponentBoundary.Owner();
        Frame(owner, holder, rebuildAll: true);

        holder.Hide();
        Frame(owner, holder);
        log.Entries.Clear();
        holder.Set(5);
        Frame(owner, holder);

        log.Entries.Should().BeEmpty("the reader's position left the tree with its dependency");
    }

    [Fact]
    public void AContextWithNoPosition_FindsNoInheritedValue()
    {
        Comfortable.DependOn<Score>().Should().BeNull("the web and the server build with no element per position until #1395");
        Comfortable.Find<Score>().Should().BeNull();
    }

    /// <summary>Reads the density, which a frame hands every build.</summary>
    private sealed class DensityReader(Log log) : StatelessComponent
    {
        public override VisualNode Build(ComponentContext context)
        {
            log.Entries.Add($"density {context.Density}");
            return new Text("density", TypeRole.BodyM);
        }
    }

    [Fact]
    public void ADensityChange_BuildsWhatReadsIt_AndNothingElse()
    {
        var log = new Log();
        var root = new Column { new DensityReader(log), new Bystander(log) };
        var owner = ComponentBoundary.Owner();
        Frame(owner, root, rebuildAll: true, context: Comfortable);
        log.Entries.Should().Equal("density Comfortable", "bystander");

        log.Entries.Clear();
        Frame(owner, root, context: Compact);

        log.Entries.Should().Equal(["density Compact"],
            "a density switch rebuilt every component until the components that read it were known");
    }

    /// <summary>Keeps the context it was handed, as a handler does.</summary>
    private sealed class Keeper : StatelessComponent
    {
        public ComponentContext? Context { get; private set; }

        public override VisualNode Build(ComponentContext context)
        {
            Context = context;
            return new Text($"{context.InFlow}", TypeRole.BodyM);
        }
    }

    [Fact]
    public void ANavigator_SendsADestinationToTheHostOfItsTree()
    {
        var keeper = new Keeper();
        var owner = ComponentBoundary.Owner();
        var sent = new List<string>();
        owner.NavigationRequested = sent.Add;
        Frame(owner, keeper, rebuildAll: true);

        Navigator.Of(keeper.Context!).Go("/orders");
        Navigator.Of(keeper.Context!).Go("");
        Navigator.Of(Comfortable).Go("/nowhere");

        sent.Should().Equal(["/orders"], "an empty destination goes nowhere, and a context with no position has no host");
    }

    [Fact]
    public void InFlow_IsReadFromThePosition()
    {
        var inside = new Keeper();
        var outside = new Keeper();
        var owner = ComponentBoundary.Owner();
        Frame(owner, new Column { new InFlow(inside), outside }, rebuildAll: true);

        inside.Context!.InFlow.Should().BeTrue();
        outside.Context!.InFlow.Should().BeFalse();
        InFlow.Current.Should().BeFalse("a position answers from the tree, and arms no ambient channel");
    }
}
