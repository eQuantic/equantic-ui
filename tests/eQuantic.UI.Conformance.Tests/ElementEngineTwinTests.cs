using System.Text.Json;
using eQuantic.UI.Conformance.Tests.Infrastructure;
using eQuantic.UI.Primitives;
using FluentAssertions;
using Xunit;

namespace eQuantic.UI.Conformance.Tests;

/// <summary>
/// The element engine is written once (docs/ELEMENT-TREE-PLAN.md 3.9): the C# that Photon and the
/// server run is transpiled into the runtime, and the browser runs THAT. Each case here is one of the
/// cases <c>ElementTreeTests</c> holds the C# engine to, run twice, on .NET and on the transpiled
/// engine the served runtime exports, with the same components on both sides and the same steps; each
/// side writes a trace of what the engine did (what built, what it kept, what mounted), and the two
/// traces are equal.
/// <para>
/// The C# owner builds through <see cref="ComponentBoundary"/>, as every .NET target does; the browser
/// has no boundary of its own yet (the lowering is its door until E3b), so its owner is handed the plain
/// build. The cases exercise no throw, so the two builds answer alike.
/// </para>
/// </summary>
public class ElementEngineTwinTests
{
    private static readonly ComponentContext Context = new(PhotonTheme.Instance);

    // ---- the components, on .NET ------------------------------------------------------------------

    private sealed class Counter(string label, List<string>? builds = null) : StatefulComponent
    {
        public string Label { get; private set; } = label;
        public int Count { get; private set; }
        public int Builds { get; private set; }

        public void Press() => SetState(() => Count++);

        public override void AdoptConfig(UiComponent next)
        {
            if (next is Counter counter) Label = counter.Label;
        }

        public override VisualNode Build(ComponentContext context)
        {
            Builds++;
            builds?.Add(Label);
            return new Text($"{Label}: {Count}", TypeRole.BodyM);
        }
    }

    private sealed class Page(string[] labels, List<string>? builds = null) : StatefulComponent
    {
        private string[] _labels = labels;

        public int Builds { get; private set; }
        public bool Keyed { get; init; }

        public void Show(params string[] labels) => SetState(() => _labels = labels);

        public override VisualNode Build(ComponentContext context)
        {
            Builds++;
            builds?.Add("page");
            var column = new Column();
            foreach (var label in _labels)
                column.Add(Keyed ? new Counter(label, builds) { Key = label } : new Counter(label, builds));
            return column;
        }
    }

    private sealed class Swapping : StatefulComponent
    {
        private bool _other;

        public void Toggle() => SetState(() => _other = !_other);

        public override VisualNode Build(ComponentContext context) =>
            _other ? new Text("another type", TypeRole.BodyM) : new Counter("counter");
    }

    private sealed class Logged(string label, List<string> log) : StatefulComponent
    {
        public string Label { get; } = label;
        public int Count { get; private set; }

        public void Press() => SetState(() => Count++);

        protected override void OnMount() => log.Add($"mount:{Label}");
        protected override void OnUnmount() => log.Add($"unmount:{Label}");

        public override VisualNode Build(ComponentContext context) => new Text($"{Label}: {Count}", TypeRole.BodyM);
    }

    private sealed class TwinKeys(List<string> log) : StatefulComponent
    {
        public void Rebuild() => SetState(() => { });

        public override VisualNode Build(ComponentContext context)
        {
            var column = new Column();
            column.Add(new Logged("first", log) { Key = "same" });
            column.Add(new Logged("second", log) { Key = "same" });
            return column;
        }
    }

    private sealed class Tracked(List<string> log) : StatefulComponent
    {
        protected override void OnMount() => log.Add("mounted");
        protected override void OnUnmount() => log.Add("unmounted");
        public override VisualNode Build(ComponentContext context) => new Text("tracked", TypeRole.BodyM);
    }

    private sealed class ReadsInFlow(List<string> seen, string name) : StatefulComponent
    {
        public void Touch() => SetState(() => { });

        public override VisualNode Build(ComponentContext context)
        {
            seen.Add($"{name}:{(context.InFlow ? "in" : "out")}");
            return new Text("x", TypeRole.BodyM);
        }
    }

    private sealed class ReadsDensity(List<string> seen, string name) : StatefulComponent
    {
        protected override void OnDependenciesChanged() => seen.Add($"{name}:changed");

        public override VisualNode Build(ComponentContext context)
        {
            seen.Add($"{name}:{(context.Density == Density.Compact ? "compact" : "comfortable")}");
            return new Text("x", TypeRole.BodyM);
        }
    }

    // ---- the same components, on the transpiled engine ---------------------------------------------

    /// <summary>The twins of the components above, over the runtime's classes, and the frame helper.</summary>
    private static string Prelude(string runtime) => $$"""
        import { BuildOwner, StatefulComponent, Column, Text, InFlow, ComponentContext } from '{{runtime}}';
        const trace = [];
        // The browser's own context, as a component's build receives it: what it reports is what the
        // C# components read through theirs (InFlow, above all).
        const context = new ComponentContext(null);
        const owner = new BuildOwner((component, context, depth) => component.build(context));
        const frame = (root, rebuildAll = true) => { const element = owner.build(root, context, 400, rebuildAll); owner.endFrame(); return element; };
        const componentAt = (element) => element.component;
        class Counter extends StatefulComponent {
          constructor(label, builds = null) { super(); this.label = label; this.count = 0; this.builds = 0; this._log = builds; }
          press() { this.setState(() => this.count++); }
          adoptConfig(next) { if (next instanceof Counter) this.label = next.label; }
          build(context) { this.builds++; if (this._log) this._log.push(this.label); return new Text(this.label + ': ' + this.count, 'bodyM'); }
        }
        class Page extends StatefulComponent {
          constructor(labels, builds = null, keyed = false) { super(); this._labels = labels; this.builds = 0; this._log = builds; this.keyed = keyed; }
          show(...labels) { this.setState(() => this._labels = labels); }
          build(context) {
            this.builds++; if (this._log) this._log.push('page');
            const column = new Column();
            for (const label of this._labels) { const counter = new Counter(label, this._log); if (this.keyed) counter.key = label; column.children.push(counter); }
            return column;
          }
        }
        class Swapping extends StatefulComponent {
          constructor() { super(); this._other = false; }
          toggle() { this.setState(() => this._other = !this._other); }
          build(context) { return this._other ? new Text('another type', 'bodyM') : new Counter('counter'); }
        }
        class Logged extends StatefulComponent {
          constructor(label, log) { super(); this.label = label; this.count = 0; this._log = log; }
          press() { this.setState(() => this.count++); }
          onMount() { this._log.push('mount:' + this.label); }
          onUnmount() { this._log.push('unmount:' + this.label); }
          build(context) { return new Text(this.label + ': ' + this.count, 'bodyM'); }
        }
        class TwinKeys extends StatefulComponent {
          constructor(log) { super(); this._log = log; }
          rebuild() { this.setState(() => {}); }
          build(context) {
            const column = new Column();
            const first = new Logged('first', this._log); first.key = 'same';
            const second = new Logged('second', this._log); second.key = 'same';
            column.children.push(first, second);
            return column;
          }
        }
        class Tracked extends StatefulComponent {
          constructor(log) { super(); this._log = log; }
          onMount() { this._log.push('mounted'); }
          onUnmount() { this._log.push('unmounted'); }
          build(context) { return new Text('tracked', 'bodyM'); }
        }
        class ReadsInFlow extends StatefulComponent {
          constructor(seen, name) { super(); this._seen = seen; this._name = name; }
          touch() { this.setState(() => {}); }
          build(context) { this._seen.push(this._name + ':' + (context.inFlow ? 'in' : 'out')); return new Text('x', 'bodyM'); }
        }
        class ReadsDensity extends StatefulComponent {
          constructor(seen, name) { super(); this._seen = seen; this._name = name; }
          onDependenciesChanged() { this._seen.push(this._name + ':changed'); }
          build(context) { this._seen.push(this._name + ':' + context.density); return new Text('x', 'bodyM'); }
        }
        const placed = (first, second) => { const column = new Column(); column.children.push(first, second); return column; };
        """;

    private static Element Frame(BuildOwner owner, VisualNode root, bool rebuildAll = true)
    {
        var element = owner.Build(root, Context, 400f, rebuildAll);
        owner.EndFrame();
        return element;
    }

    private static T ComponentAt<T>(Element element) where T : UiComponent => (T)((ComponentElement)element).Component;

    private static Column Placed(VisualNode first, VisualNode second)
    {
        var column = new Column();
        column.Add(first);
        column.Add(second);
        return column;
    }

    /// <summary>Runs the scenario on the transpiled engine and returns its trace.</summary>
    private static List<string> OnTheWeb(string scenario)
    {
        var runtime = ConformanceRunner.RuntimeJsUrl() ?? throw new InvalidOperationException("No served runtime.js.");
        var program = Prelude(runtime) + scenario + "\nconsole.log('=' + JSON.stringify(trace));\n";
        var line = JsExecutor.Run(program, timeoutMs: 60_000).Split('\n').Single(l => l.StartsWith('='));
        return JsonSerializer.Deserialize<List<string>>(line.TrimEnd('\r')[1..])!;
    }

    private static void SkipUnlessBun() =>
        Skip.IfNot(JsExecutor.EngineName == "bun", "The twin runs in the embedded Bun, and the engine here is not Bun.");

    // ---- the cases --------------------------------------------------------------------------------

    [SkippableFact]
    public void AParentRebuilds_TheChildKeepsItsStateAndTakesItsNewLabel()
    {
        SkipUnlessBun();
        var trace = new List<string>();
        var owner = ComponentBoundary.Owner();
        var page = new Page(["first"]);
        var root = Frame(owner, page);
        var counter = ComponentAt<Counter>(root.ChildAt(0)!.ChildAt(0)!);
        counter.Press();
        counter.Press();
        counter.Press();
        page.Show("second");
        Frame(owner, page, rebuildAll: false);
        var after = ComponentAt<Counter>(root.ChildAt(0)!.ChildAt(0)!);
        trace.Add($"same:{ReferenceEquals(after, counter)}");
        trace.Add($"count:{counter.Count}");
        trace.Add($"label:{counter.Label}");
        trace.Add($"text:{((Text)root.ChildAt(0)!.ChildAt(0)!.ChildAt(0)!.Configuration).Content}");

        OnTheWeb("""
            const page = new Page(['first']);
            const root = frame(page);
            const counter = componentAt(root.childAt(0).childAt(0));
            counter.press(); counter.press(); counter.press();
            page.show('second');
            frame(page, false);
            const after = componentAt(root.childAt(0).childAt(0));
            trace.push('same:' + (after === counter ? 'True' : 'False'), 'count:' + counter.count, 'label:' + counter.label,
              'text:' + root.childAt(0).childAt(0).childAt(0).configuration.content);
            """).Should().Equal(trace);
    }

    [SkippableFact]
    public void KeyedRowsSwap_EachKeepsItsOwnState()
    {
        SkipUnlessBun();
        var trace = new List<string>();
        var owner = ComponentBoundary.Owner();
        var page = new Page(["a", "b"]) { Keyed = true };
        var root = Frame(owner, page);
        var a = ComponentAt<Counter>(root.ChildAt(0)!.ChildAt(0)!);
        var b = ComponentAt<Counter>(root.ChildAt(0)!.ChildAt(1)!);
        a.Press();
        page.Show("b", "a");
        Frame(owner, page, rebuildAll: false);
        trace.Add($"first-is-b:{ReferenceEquals(ComponentAt<Counter>(root.ChildAt(0)!.ChildAt(0)!), b)}");
        trace.Add($"second-is-a:{ReferenceEquals(ComponentAt<Counter>(root.ChildAt(0)!.ChildAt(1)!), a)}");
        trace.Add($"counts:{a.Count},{b.Count}");

        OnTheWeb("""
            const page = new Page(['a', 'b'], null, true);
            const root = frame(page);
            const a = componentAt(root.childAt(0).childAt(0));
            const b = componentAt(root.childAt(0).childAt(1));
            a.press();
            page.show('b', 'a');
            frame(page, false);
            trace.push('first-is-b:' + (componentAt(root.childAt(0).childAt(0)) === b ? 'True' : 'False'),
              'second-is-a:' + (componentAt(root.childAt(0).childAt(1)) === a ? 'True' : 'False'),
              'counts:' + a.count + ',' + b.count);
            """).Should().Equal(trace);
    }

    [SkippableFact]
    public void UnkeyedRowsSwap_TheStateStaysAtItsIndex()
    {
        SkipUnlessBun();
        var owner = ComponentBoundary.Owner();
        var page = new Page(["a", "b"]);
        var root = Frame(owner, page);
        ComponentAt<Counter>(root.ChildAt(0)!.ChildAt(0)!).Press();
        page.Show("b", "a");
        Frame(owner, page, rebuildAll: false);
        var first = ComponentAt<Counter>(root.ChildAt(0)!.ChildAt(0)!);
        List<string> trace = [$"count:{first.Count}", $"label:{first.Label}"];

        OnTheWeb("""
            const page = new Page(['a', 'b']);
            const root = frame(page);
            componentAt(root.childAt(0).childAt(0)).press();
            page.show('b', 'a');
            frame(page, false);
            const first = componentAt(root.childAt(0).childAt(0));
            trace.push('count:' + first.count, 'label:' + first.label);
            """).Should().Equal(trace);
    }

    [SkippableFact]
    public void ATypeChangesAtAPosition_TheStateStartsAgain()
    {
        SkipUnlessBun();
        var owner = ComponentBoundary.Owner();
        var swapping = new Swapping();
        var root = Frame(owner, swapping);
        var first = ComponentAt<Counter>(root.ChildAt(0)!);
        first.Press();
        swapping.Toggle();
        Frame(owner, swapping, rebuildAll: false);
        swapping.Toggle();
        Frame(owner, swapping, rebuildAll: false);
        var second = ComponentAt<Counter>(root.ChildAt(0)!);
        List<string> trace = [$"same:{ReferenceEquals(second, first)}", $"count:{second.Count}"];

        OnTheWeb("""
            const swapping = new Swapping();
            const root = frame(swapping);
            const first = componentAt(root.childAt(0));
            first.press();
            swapping.toggle(); frame(swapping, false);
            swapping.toggle(); frame(swapping, false);
            const second = componentAt(root.childAt(0));
            trace.push('same:' + (second === first ? 'True' : 'False'), 'count:' + second.count);
            """).Should().Equal(trace);
    }

    [SkippableFact]
    public void TwoMarkedPositions_TheAncestorBuildsFirst_AndEachBuildsOnce()
    {
        SkipUnlessBun();
        var builds = new List<string>();
        var owner = ComponentBoundary.Owner();
        var page = new Page(["row"], builds);
        var root = Frame(owner, page);
        var counter = ComponentAt<Counter>(root.ChildAt(0)!.ChildAt(0)!);
        builds.Clear();
        counter.Press();
        page.Show("row");
        Frame(owner, page, rebuildAll: false);

        OnTheWeb("""
            const page = new Page(['row'], trace);
            const root = frame(page);
            const counter = componentAt(root.childAt(0).childAt(0));
            trace.length = 0;
            counter.press();
            page.show('row');
            frame(page, false);
            """).Should().Equal(builds);
    }

    [SkippableFact]
    public void OnlyWhatWasMarkedBuilds_AndRebuildingAllBuildsEachOnce()
    {
        SkipUnlessBun();
        var owner = ComponentBoundary.Owner();
        var page = new Page(["a", "b"]);
        var root = Frame(owner, page);
        var (a, b) = (ComponentAt<Counter>(root.ChildAt(0)!.ChildAt(0)!), ComponentAt<Counter>(root.ChildAt(0)!.ChildAt(1)!));
        var trace = new List<string>();
        Frame(owner, page, rebuildAll: false);
        trace.Add($"nothing:{page.Builds},{a.Builds},{b.Builds}");
        b.Press();
        Frame(owner, page, rebuildAll: false);
        trace.Add($"one:{page.Builds},{a.Builds},{b.Builds}");
        Frame(owner, page, rebuildAll: true);
        trace.Add($"all:{page.Builds},{a.Builds},{b.Builds}");

        OnTheWeb("""
            const page = new Page(['a', 'b']);
            const root = frame(page);
            const a = componentAt(root.childAt(0).childAt(0)), b = componentAt(root.childAt(0).childAt(1));
            frame(page, false); trace.push('nothing:' + page.builds + ',' + a.builds + ',' + b.builds);
            b.press(); frame(page, false); trace.push('one:' + page.builds + ',' + a.builds + ',' + b.builds);
            frame(page, true); trace.push('all:' + page.builds + ',' + a.builds + ',' + b.builds);
            """).Should().Equal(trace);
    }

    [SkippableTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void TwoSiblingsWithOneKey_EachKeepsItsOwnPosition(bool rebuildAll)
    {
        SkipUnlessBun();
        var log = new List<string>();
        var parent = new TwinKeys(log);
        var owner = ComponentBoundary.Owner();
        var column = Frame(owner, parent).ChildAt(0)!;
        var second = ComponentAt<Logged>(column.ChildAt(1)!);
        second.Press();
        parent.Rebuild();
        Frame(owner, parent, rebuildAll);
        log.Add($"kept:{ReferenceEquals(ComponentAt<Logged>(column.ChildAt(1)!), second)}");
        log.Add($"text:{((Text)column.ChildAt(1)!.ChildAt(0)!.Configuration).Content}");

        OnTheWeb($$"""
            const parent = new TwinKeys(trace);
            const column = frame(parent).childAt(0);
            const second = componentAt(column.childAt(1));
            second.press();
            parent.rebuild();
            frame(parent, {{(rebuildAll ? "true" : "false")}});
            trace.push('kept:' + (componentAt(column.childAt(1)) === second ? 'True' : 'False'),
              'text:' + column.childAt(1).childAt(0).configuration.content);
            """).Should().Equal(log);
    }

    [SkippableTheory]
    [InlineData(0)]
    [InlineData(1)]
    public void OneInstanceAtTwoPositions_UnmountsWhenTheLastLeaves(int leavesFirst)
    {
        SkipUnlessBun();
        var log = new List<string>();
        var shared = new Tracked(log);
        var owner = ComponentBoundary.Owner();
        Frame(owner, Placed(shared, shared));
        Frame(owner, leavesFirst == 0 ? Placed(new Text("gone", TypeRole.BodyM), shared)
            : Placed(shared, new Text("gone", TypeRole.BodyM)));
        log.Add("then");
        Frame(owner, Placed(new Text("gone", TypeRole.BodyM), new Text("gone too", TypeRole.BodyM)));

        OnTheWeb($$"""
            const shared = new Tracked(trace);
            frame(placed(shared, shared));
            frame({{(leavesFirst == 0 ? "placed(new Text('gone', 'bodyM'), shared)" : "placed(shared, new Text('gone', 'bodyM'))")}});
            trace.push('then');
            frame(placed(new Text('gone', 'bodyM'), new Text('gone too', 'bodyM')));
            """).Should().Equal(log);
    }

    [SkippableFact]
    public void AComponentUnderInFlow_BuildsInFlow_EvenWhenItBuildsAlone()
    {
        SkipUnlessBun();
        var seen = new List<string>();
        var column = new Column();
        column.Add(new InFlow(new ReadsInFlow(seen, "inside")));
        column.Add(new ReadsInFlow(seen, "outside"));
        var owner = ComponentBoundary.Owner();
        var root = Frame(owner, column);
        ComponentAt<ReadsInFlow>(root.ChildAt(0)!.ChildAt(0)!).Touch();
        Frame(owner, column, rebuildAll: false);
        seen.Add($"after:{(InFlow.Current ? "in" : "out")}");

        OnTheWeb("""
            const column = new Column();
            column.children.push(new InFlow(new ReadsInFlow(trace, 'inside')), new ReadsInFlow(trace, 'outside'));
            const root = frame(column);
            componentAt(root.childAt(0).childAt(0)).touch();
            frame(column, false);
            trace.push('after:' + (InFlow.current ? 'in' : 'out'));
            """).Should().Equal(seen);
    }

    [SkippableFact]
    public void AFrameWithAnotherDensity_BuildsWhatReadIt_AndNothingElse()
    {
        SkipUnlessBun();
        var seen = new List<string>();
        var root = Placed(new ReadsDensity(seen, "reader"), new ReadsInFlow(seen, "other"));
        var owner = ComponentBoundary.Owner();
        Frame(owner, root);
        owner.Build(root, new ComponentContext(PhotonTheme.Instance, density: Density.Compact), 400f, rebuildAll: false);
        owner.EndFrame();

        // Spelled out as well as compared, so the two sides cannot agree on building nothing.
        seen.Should().Equal("reader:comfortable", "other:out", "reader:changed", "reader:compact");
        OnTheWeb("""
            const root = placed(new ReadsDensity(trace, 'reader'), new ReadsInFlow(trace, 'other'));
            frame(root);
            owner.build(root, new ComponentContext(null, 1, 'compact'), 400, false);
            owner.endFrame();
            """).Should().Equal(seen);
    }
}
