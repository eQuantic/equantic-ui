using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace eQuantic.UI.Compiler.CodeGen.Strategies.Types;

/// <summary>
/// Strategy for Regex.
/// Handles: Regex.IsMatch(s, p) -> new RegExp(p).test(s)
/// </summary>
public class RegexStrategy : IConversionStrategy
{
    public bool CanConvert(SyntaxNode node, ConversionContext context)
    {
        if (node is InvocationExpressionSyntax inv && inv.Expression is MemberAccessExpressionSyntax ma)
        {
             var symbol = context.SemanticHelper.GetSymbol(ma);
             if (symbol != null) return symbol.ContainingType?.ToDisplayString() == "System.Text.RegularExpressions.Regex";
             
             return ma.Expression.ToString() == "Regex";
        }
        return false;
    }

    public string Convert(SyntaxNode node, ConversionContext context)
    {
        var invocation = (InvocationExpressionSyntax)node;
        var memberAccess = (MemberAccessExpressionSyntax)invocation.Expression;
        var args = invocation.ArgumentList.Arguments;
        var name = memberAccess.Name.Identifier.Text;

        if (name == "IsMatch" && args.Count >= 2)
        {
            var input = context.Converter.ConvertExpression(args[0].Expression);
            var pattern = context.Converter.ConvertExpression(args[1].Expression);
            
            return $"new RegExp({pattern}).test({input})";
        }

        return node.ToString();
    }

    public int Priority => 10;
}
