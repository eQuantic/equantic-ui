using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace eQuantic.UI.Compiler.CodeGen.Strategies.Expressions;

/// <summary>
/// Strategy for assignment expressions.
/// Handles:
/// - x = y
/// - x += y
/// - (var a, var b) = (1, 2)
/// </summary>
public class AssignmentExpressionStrategy : IConversionStrategy
{
    public bool CanConvert(SyntaxNode node, ConversionContext context)
    {
        return node is AssignmentExpressionSyntax;
    }

    public string Convert(SyntaxNode node, ConversionContext context)
    {
        var assignment = (AssignmentExpressionSyntax)node;

        // Deconstructing a record/struct (a plain object, not a tuple array) -> object destructuring
        // keyed by the type's Deconstruct order: `var (a, b) = point` -> `let { x: a, y: b } = point`.
        if (assignment.Left is DeclarationExpressionSyntax { Designation: ParenthesizedVariableDesignationSyntax design })
        {
            var rhsType = context.SemanticHelper.GetType(assignment.Right);
            if (rhsType is { IsTupleType: false } && rhsType.DeconstructElementNames() is { } fields)
            {
                var vars = design.Variables.ToList();
                var pairs = new List<string>();
                for (var i = 0; i < vars.Count && i < fields.Count; i++)
                {
                    if (vars[i] is SingleVariableDesignationSyntax s && s.Identifier.Text != "_")
                        pairs.Add($"{fields[i]}: {s.Identifier.Text}");
                }
                var rhsObj = context.Converter.ConvertExpression(assignment.Right);
                return $"let {{ {string.Join(", ", pairs)} }} = {rhsObj}";
            }
        }

        var left = context.Converter.ConvertExpression(assignment.Left);
        var right = context.Converter.ConvertExpression(assignment.Right);
        var op = assignment.OperatorToken.Text;

        // Handle discard _ = ...
        if (left == "_" || left == "this._") return right;

        // If it's a declaration deconstruction, prefix with 'let ' if not already handled
        if (assignment.Left is DeclarationExpressionSyntax && !left.StartsWith("let "))
        {
            return $"let {left} {op} {right}";
        }

        // COMPOUND assignment on a decimal is arithmetic, and a decimal crosses as a runtime
        // Decimal rather than a JS number — so `total += amount` concatenates their text. A running
        // money total read "R$ 01240.5089.90640.00": the seed, then each amount, glued end to end.
        // The binary form already routes here; this is the same operation spelled shorter.
        if (op.Length == 2 && op[1] == '=' && "+-*/".Contains(op[0])
            && context.SemanticHelper.GetType(assignment.Left).IsDecimal())
        {
            context.UsedHelpers.Add(Eq.Import);
            var method = op[0] switch { '+' => "add", '-' => "sub", '*' => "mul", _ => "div" };
            return $"{left} = {Eq.Dec}({left}).{method}({Eq.Dec}({right}))";
        }

        return $"{left} {op} {right}";
    }

    public int Priority => 10;
}
