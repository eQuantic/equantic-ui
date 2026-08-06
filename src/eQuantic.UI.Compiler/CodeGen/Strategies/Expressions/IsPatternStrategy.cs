using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace eQuantic.UI.Compiler.CodeGen.Strategies.Expressions;

/// <summary>
/// Strategy for 'is' pattern expressions (<c>x is string s</c>, <c>x is { Prop: var p }</c>,
/// <c>x is [var a, ..]</c>). The match condition and bindings come from the shared
/// <see cref="PatternConverter"/>; the <c>x is Type</c> binary form is a plain type check.
/// </summary>
public class IsPatternStrategy : IConversionStrategy
{
    public bool CanConvert(SyntaxNode node, ConversionContext context)
    {
        return node is IsPatternExpressionSyntax || 
               (node is BinaryExpressionSyntax binary && binary.IsKind(SyntaxKind.IsExpression));
    }

    public string Convert(SyntaxNode node, ConversionContext context)
    {
        if (node is IsPatternExpressionSyntax isPattern)
        {
            var expr = context.Converter.ConvertExpression(isPattern.Expression);
            var exprType = context.SemanticHelper.GetType(isPattern.Expression);

            // Peel top-level `not`s FIRST: bindings live in the inner pattern and must assign when
            // the INNER pattern matches — C#'s definite-assignment-when-false, the guard idiom
            // (`if (x is not T t) return;` leaves t assigned after the guard). Negating the whole
            // bound form (`!(match && (t = x, true))`) keeps that: inner match → t assigned →
            // expression false; no match → t untouched → expression true. Negating BEFORE the
            // assigns would short-circuit past them exactly when C# guarantees the assignment.
            var pattern = isPattern.Pattern;
            var negated = false;
            while (pattern is UnaryPatternSyntax unary && unary.OperatorToken.IsKind(SyntaxKind.NotKeyword))
            {
                negated = !negated;
                pattern = unary.Pattern;
            }

            // Condition + bindings come from the shared PatternConverter (same logic as the switch forms:
            // Deconstruct-aware positional access, list patterns, nested var bindings). A bound `is` pattern
            // assigns its variables — to slots IfStatementStrategy hoisted (`let x;`) — inside the condition
            // via a comma sequence, guarded by `&&` so the reads only run once the pattern has matched.
            var condition = PatternConverter.BuildCondition(pattern, expr, context, exprType);
            var bindings = new List<(string Name, string Access)>();
            PatternConverter.CollectBindings(pattern, expr, context, bindings, exprType);

            // `x is { } y` over a CALL — the shape that reads "if this returns something, name it".
            // The subject appears in the condition and again in the binding, so it used to run
            // TWICE: wasted work when the call is pure, and a different answer when it is not.
            // Assigning inside the test runs it once, and gives TypeScript the narrowing too.
            // Only the NULL test: `x is string s` must not assign when the type does not match, and
            // `x is { } y` has nothing to check but presence, so assigning IS the test.
            if (bindings.Count == 1 && bindings[0].Access == expr
                && condition == $"{expr} != null")
            {
                var once = $"({bindings[0].Name} = {expr}) != null";
                return negated ? $"!({once})" : once;
            }

            var bound = bindings.Count == 0
                ? condition
                : $"({condition} && ({string.Concat(bindings.Select(b => $"{b.Name} = {b.Access}, "))}true))";
            return negated ? $"!({bound})" : bound;
        }

        if (node is BinaryExpressionSyntax binary)
        {
            var expr = context.Converter.ConvertExpression(binary.Left);
            // Handle binary 'is Type': x is string
            // Right is usually TypeSyntax
            var typeName = binary.Right.ToString();
            return ConvertTypeCheck(typeName, expr);
        }

        throw new InvalidOperationException($"Invalid node type for IsPatternStrategy: {node.GetType().Name}");
    }

    private static string ConvertTypeCheck(string type, string varName)
    {
        return type switch
        {
            "string" => $"typeof {varName} === 'string'",
            "int" or "double" or "float" or "long" or "decimal" or "number" => $"typeof {varName} === 'number'",
            "bool" or "boolean" => $"typeof {varName} === 'boolean'",
            _ => $"{varName} != null" // Default for objects/unknowns is null check
        };
    }

    public int Priority => 10;
}
