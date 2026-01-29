using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace eQuantic.UI.Compiler.CodeGen.Strategies.Expressions;

/// <summary>
/// Strategy for 'is' pattern expressions.
/// Handles: 
/// - x is string s
/// - x is { Prop: val }
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
            return ConvertPattern(isPattern.Pattern, expr, context);
        }
        
        if (node is BinaryExpressionSyntax binary)
        {
            var expr = context.Converter.ConvertExpression(binary.Left);
            // Handle binary 'is Type': x is string
            // Right is usually TypeSyntax
            var typeNode = binary.Right;
            var typeName = typeNode.ToString(); 
            // Reuse declaration pattern logic for type check
            return ConvertTypeCheck(typeName, expr);
        }
        
        throw new InvalidOperationException($"Invalid node type for IsPatternStrategy: {node.GetType().Name}");
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
                return ConvertDeclarationPattern(declaration, varName);

            case RecursivePatternSyntax recursive:
                return ConvertRecursivePattern(recursive, varName, context);

            case UnaryPatternSyntax unary when unary.OperatorToken.IsKind(SyntaxKind.NotKeyword):
                return $"!({ConvertPattern(unary.Pattern, varName, context)})";

            case BinaryPatternSyntax binary:
                 var left = ConvertPattern(binary.Left, varName, context);
                 var rightPattern = ConvertPattern(binary.Right, varName, context);
                 var logicOp = binary.OperatorToken.IsKind(SyntaxKind.OrKeyword) ? "||" : "&&";
                 return $"({left} {logicOp} {rightPattern})";

            case DiscardPatternSyntax _:
            case VarPatternSyntax _:
                return "true";
                
            default:
                return "false";
        }
    }

    private string ConvertDeclarationPattern(DeclarationPatternSyntax declaration, string varName)
    {
        var type = declaration.Type.ToString();
        var name = declaration.Designation is SingleVariableDesignationSyntax variable ? variable.Identifier.Text : null;
        
        string typeCheck = ConvertTypeCheck(type, varName);

        if (name != null && name != "_")
        {
            // IIFE to allow assignment and check
            return $"((() => {{ {name} = {varName}; return {typeCheck}; }})())";
        }
        return typeCheck;
    }

    private string ConvertTypeCheck(string type, string varName)
    {
        return type switch
        {
            "string" => $"typeof {varName} === 'string'",
            "int" or "double" or "float" or "long" or "decimal" or "number" => $"typeof {varName} === 'number'",
            "bool" or "boolean" => $"typeof {varName} === 'boolean'",
            _ => $"{varName} != null" // Default for objects/unknowns is null check
        };
    }

    private string ConvertRecursivePattern(RecursivePatternSyntax recursive, string varName, ConversionContext context)
    {
        var checks = new List<string>();
        if (recursive.Type != null) checks.Add($"{varName} != null");
        
        if (recursive.PositionalPatternClause != null)
        {
            for (int i = 0; i < recursive.PositionalPatternClause.Subpatterns.Count; i++)
            {
                var subVar = $"{varName}[{i}]";
                checks.Add(ConvertPattern(recursive.PositionalPatternClause.Subpatterns[i].Pattern, subVar, context));
            }
        }

        if (recursive.PropertyPatternClause != null)
        {
            foreach (var sub in recursive.PropertyPatternClause.Subpatterns)
            {
                var propName = sub.NameColon?.Name.ToString();
                if (propName != null)
                {
                    var subVar = $"{varName}.{ToCamelCase(propName)}";
                    checks.Add(ConvertPattern(sub.Pattern, subVar, context));
                }
            }
        }

        return checks.Count > 0 ? string.Join(" && ", checks) : $"{varName} != null";
    }

    private static string ToCamelCase(string name)
    {
        if (string.IsNullOrEmpty(name)) return name;
        return char.ToLowerInvariant(name[0]) + name[1..];
    }

    public int Priority => 10;
}
