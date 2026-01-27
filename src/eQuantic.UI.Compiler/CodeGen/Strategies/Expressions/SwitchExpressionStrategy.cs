using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using eQuantic.UI.Compiler.Services;
using Microsoft.CodeAnalysis.CSharp;

namespace eQuantic.UI.Compiler.CodeGen.Strategies.Expressions;

public class SwitchExpressionStrategy : IConversionStrategy
{
    public bool CanConvert(SyntaxNode node, ConversionContext context)
    {
        return node is SwitchExpressionSyntax;
    }

    public string Convert(SyntaxNode node, ConversionContext context)
    {
        var switchExpr = (SwitchExpressionSyntax)node;
        var governingExpr = context.Converter.ConvertExpression(switchExpr.GoverningExpression);

        var sb = new StringBuilder();

        // Wrap in IIFE to allow complex logic blocks
        sb.Append("(() => {");
        sb.Append($" const _s = {governingExpr};");

        foreach (var arm in switchExpr.Arms)
        {
            if (arm.Pattern is DiscardPatternSyntax)
            {
                var result = context.Converter.ConvertExpression(arm.Expression);
                sb.Append($" return {result};");
                // Discard matches everything, so we stop here
                break;
            }

            var condition = ConvertPattern(arm.Pattern, "_s", context);
            if (arm.WhenClause != null)
            {
                var whenCond = context.Converter.ConvertExpression(arm.WhenClause.Condition);
                condition = $"({condition}) && ({whenCond})";
            }

            var armResult = context.Converter.ConvertExpression(arm.Expression);
            sb.Append($" if ({condition}) return {armResult};");
        }

        // Default fallback if no discard pattern exists
        if (!switchExpr.Arms.Any(a => a.Pattern is DiscardPatternSyntax))
        {
            sb.Append(" return null;");
        }

        sb.Append(" })()");
        return sb.ToString();
    }

    private string ConvertPattern(PatternSyntax pattern, string varName, ConversionContext context)
    {
        switch (pattern)
        {
            case ConstantPatternSyntax constant:
                var val = context.Converter.ConvertExpression(constant.Expression);
                return $"{varName} === {val}";

            case RelationalPatternSyntax relational:
                var op = relational.OperatorToken.Text;
                var right = context.Converter.ConvertExpression(relational.Expression);
                return $"{varName} {op} {right}";

            case DeclarationPatternSyntax declaration:
                var type = declaration.Type.ToString();
                // Simple type check for primitives
                if (type == "string") return $"typeof {varName} === 'string'";
                if (type == "int" || type == "double") return $"typeof {varName} === 'number'";
                if (type == "bool") return $"typeof {varName} === 'boolean'";
                // For classes, we might check instance? But in JS everything is object.
                // Assuming truthy check for object presence
                return $"{varName} != null";

            case RecursivePatternSyntax recursive:
                var checks = new List<string>();

                // Type check if present
                if (recursive.Type != null)
                {
                    checks.Add($"{varName} != null");
                }

                // Positional patterns (a, b)
                if (recursive.PositionalPatternClause != null)
                {
                    for (int i = 0; i < recursive.PositionalPatternClause.Subpatterns.Count; i++)
                    {
                        var sub = recursive.PositionalPatternClause.Subpatterns[i];
                        // Assuming target is an array or has indexed properties
                        var subVar = $"{varName}[{i}]";
                        checks.Add(ConvertPattern(sub.Pattern, subVar, context));
                    }
                }

                // Property patterns { Prop: 1 }
                if (recursive.PropertyPatternClause != null)
                {
                    foreach (var subPattern in recursive.PropertyPatternClause.Subpatterns)
                    {
                        var propName = subPattern.NameColon?.Name.ToString();
                        if (propName != null)
                        {
                            var subVar = $"{varName}.{char.ToLowerInvariant(propName[0])}{propName.Substring(1)}";
                            checks.Add(ConvertPattern(subPattern.Pattern, subVar, context));
                        }
                    }
                }

                return checks.Count > 0 ? string.Join(" && ", checks) : $"{varName} != null";

            case UnaryPatternSyntax unary:
                if (unary.OperatorToken.IsKind(SyntaxKind.NotKeyword))
                {
                    return $"!({ConvertPattern(unary.Pattern, varName, context)})";
                }
                return "false";

            case VarPatternSyntax _:
                return "true"; // Always matches

            case BinaryPatternSyntax binary:
                 var left = ConvertPattern(binary.Left, varName, context);
                 var rightPattern = ConvertPattern(binary.Right, varName, context);
                 var logicOp = binary.OperatorToken.IsKind(SyntaxKind.OrKeyword) ? "||" : "&&";
                 return $"({left} {logicOp} {rightPattern})";

            default:
                return "false";
        }
    }

    public int Priority => 0;
}
