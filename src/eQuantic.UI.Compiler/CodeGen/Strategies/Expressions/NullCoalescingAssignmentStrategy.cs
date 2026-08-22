using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using eQuantic.UI.Compiler.CodeGen.Ir;

namespace eQuantic.UI.Compiler.CodeGen.Strategies.Expressions;

/// <summary>
/// Strategy for null-coalescing assignment operator.
/// Handles: x ??= y -> x = x ?? y (or x ?? (x = y))
/// </summary>
public class NullCoalescingAssignmentStrategy : IExpressionIrStrategy
{
    public bool CanConvert(SyntaxNode node, ConversionContext context)
    {
        if (node is not AssignmentExpressionSyntax assignment)
            return false;

        return assignment.Kind() == SyntaxKind.CoalesceAssignmentExpression;
    }

    public JsExpr ConvertIr(SyntaxNode node, ConversionContext context)
    {
        var assignment = (AssignmentExpressionSyntax)node;

        // A DICTIONARY entry reads before it coalesces, and .NET throws for a key that is not
        // there — `m[k] ??= v` on a missing key is a KeyNotFoundException, not an insert. The
        // guarded read cannot stand in `a ?? (a = b)`, so it is lowered the way the compound
        // assignment is: read through the guard, write the result. Writing back a value that was
        // already there is not observable on a plain object, and `??` still short-circuits the
        // right-hand side. The template binds the receiver and key once each.
        if (assignment.Left is ElementAccessExpressionSyntax { ArgumentList.Arguments.Count: 1 } target
            && context.SemanticHelper.GetType(target.Expression).IsDictionaryLike(out _))
        {
            context.UsedHelpers.Add(Eq.Import);
            return JsExpr.Template($"{{0}}[{{1}}] = {Eq.DictGet}({{0}}, {{1}}) ?? {{2}}",
                context.Converter.ConvertIr(target.Expression),
                context.Converter.ConvertIr(target.ArgumentList.Arguments[0].Expression),
                context.Converter.ConvertIr(assignment.Right));
        }

        var left = context.Converter.ConvertExpression(assignment.Left);
        var right = context.Converter.ConvertExpression(assignment.Right);

        // x ??= y converts to: x ?? (x = y) — the target is named twice but evaluated once, and
        // assigned only when null/undefined. Built as a `??` NODE so the writer knows to fence it
        // off from a surrounding && or ||, which JavaScript refuses to parse beside it.
        return JsExpr.Binary(JsExpr.Opaque(left), "??", JsExpr.Opaque($"({left} = {right})"));
    }

    public int Priority => 15; // Higher than AssignmentExpressionStrategy (10)
}
