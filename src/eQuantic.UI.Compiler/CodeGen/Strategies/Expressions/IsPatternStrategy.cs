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

            // Condition + bindings come from the shared PatternConverter (same logic as the switch forms:
            // Deconstruct-aware positional access, list patterns, nested var bindings). A bound `is` pattern
            // assigns its variables — to slots IfStatementStrategy hoisted (`let x;`) — inside the condition
            // via a comma sequence, guarded by `&&` so the reads only run once the pattern has matched.
            var condition = PatternConverter.BuildCondition(isPattern.Pattern, expr, context, exprType);
            var bindings = new List<(string Name, string Access)>();
            PatternConverter.CollectBindings(isPattern.Pattern, expr, context, bindings, exprType);

            if (bindings.Count == 0) return condition;
            var assigns = string.Concat(bindings.Select(b => $"{b.Name} = {b.Access}, "));
            return $"({condition} && ({assigns}true))";
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
