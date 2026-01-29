using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace eQuantic.UI.Compiler.CodeGen.Strategies.Types;

/// <summary>
/// Strategy for TimeSpan.
/// Handles: 
/// - TimeSpan.FromSeconds(x) -> x * 1000
/// - TimeSpan.TotalMilliseconds -> value
/// </summary>
public class TimeSpanStrategy : IConversionStrategy
{
    public bool CanConvert(SyntaxNode node, ConversionContext context)
    {
        if (node is InvocationExpressionSyntax inv && inv.Expression is MemberAccessExpressionSyntax ma)
        {
             var symbol = context.SemanticHelper.GetSymbol(ma);
             if (symbol != null) return symbol.ContainingType?.ToDisplayString() == "System.TimeSpan";
             
             // Heuristic
             return ma.Expression.ToString() == "TimeSpan";
        }
        
        if (node is MemberAccessExpressionSyntax memberAccess)
        {
             var symbol = context.SemanticHelper.GetSymbol(node);
             if (symbol != null) return symbol.ContainingType?.ToDisplayString() == "System.TimeSpan";
             
             // Heuristic
             return memberAccess.Expression.ToString() == "TimeSpan";
        }
        
        return false;
    }

    public string Convert(SyntaxNode node, ConversionContext context)
    {
        if (node is InvocationExpressionSyntax invocation)
        {
            var memberAccess = (MemberAccessExpressionSyntax)invocation.Expression;
            var name = memberAccess.Name.Identifier.Text;
            var args = invocation.ArgumentList.Arguments;
            
            var isStatic = context.SemanticHelper.IsStatic(memberAccess) || memberAccess.Expression.ToString() == "TimeSpan";
            
            if (isStatic)
            {
                var val = args.Count > 0 ? context.Converter.ConvertExpression(args[0].Expression) : "0";
                
                return name switch
                {
                    "FromMilliseconds" => val,
                    "FromSeconds" => $"({val} * 1000)",
                    "FromMinutes" => $"({val} * 60000)",
                    "FromHours" => $"({val} * 3600000)",
                    "FromDays" => $"({val} * 86400000)",
                    _ => $"{val}" 
                };
            }
        }
        else if (node is MemberAccessExpressionSyntax memberAccess)
        {
            var expr = context.Converter.ConvertExpression(memberAccess.Expression);
            var name = memberAccess.Name.Identifier.Text;
            
            return name switch
            {
                "TotalMilliseconds" => expr,
                "TotalSeconds" => $"({expr} / 1000)",
                _ => expr
            };
        }

        return node.ToString();
    }

    public int Priority => 10;
}
