namespace eQuantic.UI.Compiler.CodeGen.Ir;

/// <summary><c>...operand</c> — a sequence spread into an array or an argument list, the only places
/// it can stand. Its operand is an assignment-level expression, so only a sequence would need its
/// own parentheses.</summary>
public sealed record JsSpread(JsExpr Operand) : JsExpr
{
    public override JsPrecedence Precedence => JsPrecedence.Assignment;
}
