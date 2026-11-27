using eQuantic.UI.Components;
using eQuantic.UI.Primitives;
using static eQuantic.UI.Components.UI;

namespace eQuantic.UI.Web.Tests.Fixtures;

/// <summary>
/// A component written as an app writes one around the focus (#1897): a <see cref="FocusNode"/> held in a field, a
/// lambda added to its <c>Changed</c> in the constructor, a scope whose edge goes on in the parent, a
/// <c>RequestFocus()</c> from a handler, and <c>Focus</c> and <c>FocusScope</c> in its build. Compiled here, transpiled
/// by eqc into the runtime's <c>shared/__fixtures__/</c>, and executed by <c>focus-traversal.spec.ts</c> against the
/// transpiled manager, so the C# an app writes is the C# that runs in the browser (and in
/// <c>AFocusNodeOfTheAppsOwnTests</c>, built the way an app is).
/// </summary>
public sealed class FocusSearch : StatefulComponent
{
    private readonly FocusNode _field = new();
    private readonly FocusScopeNode _results = new() { TraversalEdgeBehavior = TraversalEdgeBehavior.ParentScope };
    private string _status = "idle";
    private int _heard;

    public FocusSearch()
    {
        _field.Changed += () => SetState(() => _status = _field.HasFocus ? "field" : "idle");
    }

    /// <summary>What the component says it holds: the field's node answers, and so does the scope's.</summary>
    public string Status => _status;

    /// <summary>How many times the scope's <c>onFocusChange</c> was called.</summary>
    public int Heard => _heard;

    /// <summary>A handler asks for the focus on the results, as an Enter in the field would.</summary>
    public void OpenResults() => _results.RequestFocus();

    /// <summary>A handler gives the focus up and moves it on, as a toolbar that takes none would.</summary>
    public void Leave()
    {
        _field.Unfocus();
        _field.NextFocus();
    }

    public override VisualNode Build(ComponentContext context) =>
        Column(children:
        [
            Focus(focusNode: _field, autofocus: true, child: Button("Search", onPressed: OpenResults)),
            FocusScope(node: _results, traversalEdgeBehavior: TraversalEdgeBehavior.ParentScope,
                onFocusChange: hasFocus => _heard++,
                child: Column(children: [Focus(skipTraversal: true, child: Text(_status))])),
        ]);
}
