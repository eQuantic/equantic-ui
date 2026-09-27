using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

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
    /// The body of a method that has them: outs declared, the original body run as a closure so its
    /// returns keep working, and one object carrying everything back.
    /// </summary>
    public static string WrapBody(string body, IReadOnlyList<ParameterSyntax> byReference, bool isAsync)
    {
        var declared = byReference.Where(IsOut).Select(p => p.Identifier.Text.ToJsIdentifier()).ToArray();
        var carried = byReference.Select(p => p.Identifier.Text.ToJsIdentifier());

        var declaration = declared.Length > 0 ? $"let {string.Join(", ", declared)}; " : "";
        var call = isAsync ? $"await (async () => {{ {body} }})()" : $"(() => {{ {body} }})()";
        return $"{declaration}const $r = {call}; return {{ $: $r, {string.Join(", ", carried)} }};";
    }
}
