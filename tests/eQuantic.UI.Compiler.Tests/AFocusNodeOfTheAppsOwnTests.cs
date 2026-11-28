using FluentAssertions;
using eQuantic.UI.Compiler.Tests.ReferencedProjects;

namespace eQuantic.UI.Compiler.Tests;

/// <summary>
/// A focus node of the app's own crosses to the browser (#1897): a component holds a <c>FocusNode</c> in a field, adds
/// a lambda to its <c>Changed</c>, asks for the focus from a handler and wraps its children in <c>Focus</c> and
/// <c>FocusScope</c>. Built as an app is built, with the factories in scope, so what the focus manager's twin is
/// handed in the browser is what an app writes: a construct eqc refuses here is the SDK's gap, filed and fixed, never
/// written around (PRINCIPLES 14).
/// </summary>
public class AFocusNodeOfTheAppsOwnTests
{
    private const string Search =
        """
        using eQuantic.UI.Primitives;

        namespace eQuantic.Console;

        public sealed class Search : StatefulComponent
        {
            private readonly FocusNode _field = new();
            private readonly FocusScopeNode _results = new();
            private string _status = "idle";

            public Search()
            {
                _field.Changed += () => SetState(() => _status = _field.HasFocus ? "field" : "idle");
            }

            private void Open() => _results.RequestFocus();

            private void Leave()
            {
                _field.Unfocus();
                _field.NextFocus();
            }

            public override VisualNode Build(ComponentContext context) =>
                Column(children:
                [
                    Focus(focusNode: _field, autofocus: true, child: Button("Search", onPressed: Open)),
                    FocusScope(node: _results, traversalEdgeBehavior: TraversalEdgeBehavior.ParentScope,
                        onFocusChange: hasFocus => SetState(() => _status = hasFocus ? "results" : "idle"),
                        child: Column(children: [Focus(skipTraversal: true, child: Text(_status))])),
                    Button("Leave", onPressed: Leave),
                ]);
        }
        """;

    [Fact]
    public void AComponentThatHoldsAFocusNode_CompilesForTheBrowser()
    {
        using var solution = new Solution()
            .App("Console", globalUsings: SharedLibrary.Factories, files: [("Components/Search.cs", Search)]);

        var built = solution.Build();

        built.Errors.Split('\n').Where(line => line.Contains(": error ", StringComparison.Ordinal)
                || line.Contains(": warning ", StringComparison.Ordinal))
            .Should().BeEmpty("a focus node is ordinary client C#, and eqc translates every construct the component uses");
        var module = built.Module("Search");
        module.Should().Contain("new FocusNode()", "the node a component holds is the runtime's twin");
        module.Should().Contain("new FocusScopeNode()", "so is its scope, whose edge the FocusScope node writes at every build");
        module.Should().Contain("requestFocus()", "a handler asks for the focus through the node");
        module.Should().Contain("nextFocus()");
        module.Should().Contain("unfocus()");
        module.Should().Contain("$eq.delegates.combine(", "a lambda added to Changed is added to a multicast delegate, not assigned");
        module.Should().Contain("UI.focus(", "Focus and FocusScope are written through the factories");
        module.Should().Contain("UI.focusScope(");
    }

    private const string WithInitializer =
        """
        using eQuantic.UI.Primitives;

        namespace eQuantic.Console;

        public sealed class Results : StatelessComponent
        {
            private readonly FocusScopeNode _scope = new() { TraversalEdgeBehavior = TraversalEdgeBehavior.ParentScope };

            public override VisualNode Build(ComponentContext context) =>
                FocusScope(node: _scope, child: Text("results"));
        }
        """;

    /// <summary>
    /// An app that sets a scope's edge with an object initializer, as C# writes it. eqc hands the twin a config object,
    /// `new FocusScopeNode({ traversalEdgeBehavior: 'parentScope' })`, and the twin of a TRANSPILED vocabulary class
    /// has a constructor with no parameter for one: the edge is dropped in the browser, and the runtime's tsc refuses
    /// the call (TS2554). A hand-written twin takes the config (#1823); a transpiled one does not.
    /// </summary>
    [Fact(Skip = "A gap of eqc, to be filed (PRINCIPLES 14): an object initializer on a transpiled vocabulary class is emitted as a config argument its twin has no parameter for.")]
    public void AnObjectInitializerOnAFocusScopeNode_ReachesTheNodeInTheBrowser()
    {
        using var solution = new Solution()
            .App("Console", globalUsings: SharedLibrary.Factories, files: [("Components/Results.cs", WithInitializer)]);

        var module = solution.Build().Module("Results");

        module.Should().NotMatchRegex(@"new FocusScopeNode\(\{", "the twin's constructor takes no config, so the edge would be dropped");
        module.Should().Contain("parentScope");
    }
}
