namespace eQuantic.UI.Compiler.CodeGen;

/// <summary>
/// WHICH framework base a class ultimately reaches by walking its base chain — the answer
/// classification needs, where <c>TypeSymbolExtensions.IsUiComponent</c> only says whether
/// it reaches one at all.
///
/// <para>
/// The four members are the abstract bases the framework itself defines, and the list is
/// deliberately tiny: an intermediate base the app or a library wrote is REACHED by the walk, never
/// named here. That is the whole point — a component written over <c>CardBase</c> resolves to the
/// <c>StatelessComponent</c> behind it, and nothing downstream has to know that <c>CardBase</c>
/// exists.
/// </para>
///
/// <para>
/// This type is why the parser stopped comparing <c>BaseList.Types.First().ToString()</c> against
/// string literals. That comparison could not see past the written name, so a class over an
/// app-owned base fell into a fourth arm that parsed almost nothing — and because each arm carried
/// its own list of sub-parsers, the disagreement was invisible: no compile error, no diagnostic, no
/// failing test, just a page that dies at first render. See #268, #269 and #245, which are one
/// defect reached three ways.
/// </para>
/// </summary>
public enum ComponentBaseKind
{
    /// <summary>The chain reaches no framework base: not a component.</summary>
    None,

    /// <summary>Reaches <c>StatefulComponent</c>.</summary>
    StatefulComponent,

    /// <summary>Reaches <c>StatelessComponent</c>.</summary>
    StatelessComponent,

    /// <summary>Reaches <c>HtmlElement</c> — the one shape that parses down the primitive path.</summary>
    HtmlElement,

    /// <summary>Reaches <c>ComponentState</c>: a stateful component's state class, emitted by its
    /// own page rather than as a standalone module.</summary>
    ComponentState,
}
