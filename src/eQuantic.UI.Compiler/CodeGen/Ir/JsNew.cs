namespace eQuantic.UI.Compiler.CodeGen.Ir;

/// <summary>
/// <c>new Target(arguments)</c>: a construction, its arguments fenced by their commas as a call's
/// are, so a lambda among them reaches the writer as an arrow whose block maps line by line (#492).
/// Spliced as text, a creation took every line of such a lambda's block with it. It binds as a member
/// access does, so it stands as a receiver without parentheses.
/// </summary>
public sealed record JsNew(JsExpr Target, IReadOnlyList<JsExpr> Arguments) : JsExpr
{
    public override JsPrecedence Precedence => JsPrecedence.Call;
}
