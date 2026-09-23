using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Operations;
using eQuantic.UI.Compiler.CodeGen.Ir;

namespace eQuantic.UI.Compiler.CodeGen.Strategies.Expressions;

/// <summary>
/// C#'s NON-short-circuit logical operators on <c>bool</c> — <c>|</c>, <c>&amp;</c>, <c>^</c> and
/// their compound forms — lowered to JavaScript that means the same thing.
/// <para>
/// JavaScript has no operator that is both of the things these are. Its <c>|</c> and <c>&amp;</c>
/// evaluate both operands but answer a NUMBER, so <c>typed |= Type(c)</c> stored <c>1</c> in a bool
/// and TypeScript refused to compile it at all (TS2447). Its <c>||</c> and <c>&amp;&amp;</c> answer a
/// bool but SKIP the right side, so the same line written with <c>||</c> would stop typing after the
/// first character that took. So <c>|</c> and <c>&amp;</c> become a call — whose arguments are
/// evaluated left to right before its body runs — and <c>^</c> becomes <c>!==</c>, which already
/// evaluates both and answers a bool.
/// </para>
/// <para>
/// Asked of the BOUND TREE: it is the operator's own operand types that decide, and a lifted
/// (<c>bool?</c>) operator is three-valued logic this does not model, so it is left alone.
/// </para>
/// </summary>
internal static class BoolLogic
{
    /// <summary>The lowering of <paramref name="op"/> (<c>|</c>, <c>&amp;</c> or <c>^</c>) when both
    /// operands are <c>bool</c>; null for anything else.</summary>
    public static JsExpr? Lower(SyntaxNode node, string op, JsExpr left, JsExpr right, ConversionContext context)
    {
        if (op is not ("|" or "&" or "^")) return null;
        if (!OnBools(node, context)) return null;

        if (op == "^") return JsExpr.Binary(left, "!==", right);
        context.UsedHelpers.Add(Eq.Import);
        return JsExpr.Call(JsExpr.Identifier(op == "|" ? Eq.LogicOr : Eq.LogicAnd), left, right);
    }

    private static bool OnBools(SyntaxNode node, ConversionContext context)
    {
        var operation = context.SemanticHelper.GetOperation(node);
        return operation switch
        {
            IBinaryOperation { IsLifted: false, OperatorMethod: null } binary =>
                IsBool(binary.LeftOperand.Type) && IsBool(binary.RightOperand.Type),
            ICompoundAssignmentOperation { IsLifted: false, OperatorMethod: null } compound =>
                IsBool(compound.Target.Type) && IsBool(compound.Value.Type),
            _ => false,
        };
    }

    private static bool IsBool(ITypeSymbol? type) => type?.SpecialType == SpecialType.System_Boolean;
}
