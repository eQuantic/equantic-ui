using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;
using eQuantic.UI.Compiler.CodeGen.Extensions;
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

    /// <summary>
    /// A COMPOUND assignment on a bool — <c>|=</c>, <c>&amp;=</c>, <c>^=</c> — written back as the
    /// logical operator, or null when it is not one. The TARGET is evaluated once, as C# evaluates it:
    /// <c>GetState().Flag |= Next()</c> calls <c>GetState()</c> a single time, and a dictionary entry
    /// is read through the guard that throws for a missing key (<c>$eq.dictGet</c>) with its receiver
    /// and its key each evaluated once. The template's writer does the binding; a plain name or
    /// <c>this</c> is simply inlined.
    /// </summary>
    public static JsExpr? LowerCompound(AssignmentExpressionSyntax assignment, ConversionContext context)
    {
        if (!OnBools(assignment, context)) return null;
        var op = assignment.OperatorToken.Text[..^1];
        context.UsedHelpers.Add(Eq.Import);
        var right = context.Converter.ConvertIr(assignment.Right);

        if (DictionaryEntry.Of(assignment.Left, context) is { } found)
        {
            var (entry, form) = found;
            return JsExpr.Template($"({form.Write("{0}", "{1}", Combine(op, form.Read("{0}", "{1}"), "{2}"))})",
                [context.Converter.ConvertIr(entry.Expression),
                 context.Converter.ConvertIr(entry.ArgumentList.Arguments[0].Expression),
                 right],
                context.TypeAnnotations);
        }

        var left = context.Converter.ConvertIr(assignment.Left);
        return left switch
        {
            // A bound part becomes a parameter of the writer's arrow, which a type-checked module
            // refuses untyped — hence the annotation wherever the output is TypeScript.
            JsMember member => JsExpr.Template(
                $"({{0}}.{member.Name} = {Combine(op, $"{{0}}.{member.Name}", "{1}")})",
                [member.Target, right], context.TypeAnnotations),
            JsIndex index => JsExpr.Template(
                $"({{0}}[{{1}}] = {Combine(op, "{0}[{1}]", "{2}")})",
                [index.Target, index.IndexExpression, right], context.TypeAnnotations),
            // A plain name has no receiver to evaluate twice.
            _ => JsExpr.Binary(left, "=", op == "^"
                ? JsExpr.Binary(left, "!==", right)
                : JsExpr.Call(JsExpr.Identifier(op == "|" ? Eq.LogicOr : Eq.LogicAnd), left, right)),
        };
    }

    /// <summary>The logical combination of two template operands — shared with the dictionaries a
    /// runtime map backs, whose compound assignment is theirs to write.</summary>
    internal static string Combine(string op, string left, string right) => op switch
    {
        "|" => $"{Eq.LogicOr}({left}, {right})",
        "&" => $"{Eq.LogicAnd}({left}, {right})",
        _ => $"({left} !== {right})",
    };

    internal static bool OnBools(SyntaxNode node, ConversionContext context)
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
