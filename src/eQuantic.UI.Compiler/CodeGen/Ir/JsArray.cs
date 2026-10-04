namespace eQuantic.UI.Compiler.CodeGen.Ir;

/// <summary>
/// An array literal, <c>[a, ...b, c]</c>: what a C# collection expression builds, so the lambdas
/// among its elements reach the writer as arrows whose blocks map line by line (#384). An element is
/// fenced by its commas, as an argument is; a <see cref="JsSpread"/> spreads another sequence in.
/// </summary>
public sealed record JsArray(IReadOnlyList<JsExpr> Elements) : JsExpr
{
    public override JsPrecedence Precedence => JsPrecedence.Primary;
}
