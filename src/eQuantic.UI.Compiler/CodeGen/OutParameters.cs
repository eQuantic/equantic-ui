using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using eQuantic.UI.Compiler.CodeGen.Ir;

namespace eQuantic.UI.Compiler.CodeGen;

/// <summary>
/// `out` and `ref` parameters, which JavaScript does not have.
/// <para>
/// What used to happen: the argument `out var caret` emitted the bare name `caret`, undeclared and
/// unassigned. In a module (always strict) that is a ReferenceError the first time the line runs —
/// and nothing said so at build time. A document model whose `Replace` handed back the new caret
/// position transpiled "successfully" and could not execute a single edit.
/// </para>
/// <para>
/// The shape used here: a method with outs returns an OBJECT — its own value under <c>$</c>, each
/// out and ref under its name — and its body moves inside a closure so that every `return` in it
/// still means what it meant. The call site unwraps with an arrow, which works in any expression
/// position (including inside an `if`), so no statement rewriting is needed anywhere:
/// </para>
/// <code>
/// // C#:  var next = document.Replace(range, text, out var caret);
/// let caret;
/// let next = ($o => (caret = $o.caret, $o.$))(document.replace(range, text));
/// </code>
/// </summary>
internal static class OutParameters
{
    /// <summary>The `out`/`ref` parameters of a signature, in order. Empty for the ordinary case,
    /// which is why every caller can ask without paying anything.</summary>
    public static IReadOnlyList<ParameterSyntax> Of(BaseMethodDeclarationSyntax? method) =>
        method is null ? [] : Of(method.ParameterList);

    public static IReadOnlyList<ParameterSyntax> Of(ParameterListSyntax? parameters) =>
        parameters is null
            ? []
            : parameters.Parameters.Where(IsByReference).ToArray();

    public static bool IsByReference(ParameterSyntax parameter) =>
        parameter.Modifiers.Any(SyntaxKind.OutKeyword) || parameter.Modifiers.Any(SyntaxKind.RefKeyword);

    /// <summary>An `out` parameter is not passed IN, so it leaves the JS signature; a `ref` one is
    /// read before it is written, so it stays.</summary>
    public static bool IsOut(ParameterSyntax parameter) => parameter.Modifiers.Any(SyntaxKind.OutKeyword);

    /// <summary>
    /// The body of a method, a lambda or a local function that has them: its outs declared, the body
    /// run in an arrow so that each of its returns still means what it meant, and one object carrying
    /// everything back. One owner, because the local function kept its outs as plain parameters while
    /// the lambda wrapped them, and every call site unwraps (#541).
    /// <para>
    /// Built as IR, so the body's statements keep the C# they came from wherever the writer places
    /// the arrow, as any lambda's block does (#384). The wrapper was TEXT: the body's first statement
    /// shared the wrapper's line, and none of them had a mapping, so a breakpoint in the body bound
    /// nowhere and a frame thrown there read as whatever was mapped before it (#487). The wrapper's
    /// own lines belong to the declaration whose parameters these are, the arrow's call among them,
    /// which is a frame of its own in a stack.
    /// </para>
    /// <para>
    /// The arrow never awaits: C# refuses a <c>ref</c> or an <c>out</c> parameter on an async method
    /// or lambda (CS1988), and on an iterator (CS1623).
    /// </para>
    /// </summary>
    /// <param name="block">The body, when it is a block.</param>
    /// <param name="expressionBody">The body, when it is an expression.</param>
    /// <param name="byReference">The <c>out</c> and <c>ref</c> parameters, in order: not empty.</param>
    /// <param name="converter">The conversion the body is part of, at the depth of the body's block.</param>
    public static JsStatement Body(BlockSyntax? block, ExpressionSyntax? expressionBody,
        IReadOnlyList<ParameterSyntax> byReference, CSharpToJsConverter converter)
    {
        var owner = byReference[0].Parent?.Parent ?? byReference[0];
        var declared = byReference.Where(IsOut).Select(p => p.Identifier.Text.ToJsIdentifier()).ToArray();
        var carried = byReference.Select(p => p.Identifier.Text.ToJsIdentifier());

        // The wrapper's statements stand one level into the body, the arrow's head among them, and
        // the arrow's block one level further: the body converts there, so what it lays out (a
        // lambda's block) indents as it will be read.
        var depth = converter.Depth + 1;
        var body = converter.InBlock(() => block != null
            ? converter.ConvertBlockIr(block)
            : expressionBody != null
                ? converter.ConvertExpressionBodyIr(expressionBody, returns: true)
                : JsStatement.Block([]));
        var run = JsExpr.Call(JsExpr.ArrowBlock("", body with { Origin = owner }, converter.Layout, depth));

        return JsStatement.Block([
            .. declared.Length > 0 ? [JsStatement.Raw($"let {string.Join(", ", declared)};")] : Array.Empty<JsStatement>(),
            JsStatement.Const("$r", run),
            JsStatement.Return(JsExpr.Opaque($"{{ $: $r, {string.Join(", ", carried)} }}")),
        ]) with { Origin = owner };
    }
}
