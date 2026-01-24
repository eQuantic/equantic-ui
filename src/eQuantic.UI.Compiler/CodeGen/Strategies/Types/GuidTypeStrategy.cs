using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using eQuantic.UI.Compiler.Services;

namespace eQuantic.UI.Compiler.CodeGen.Strategies.Types;

/// <summary>
/// Converts Guid.NewGuid(), Guid.Empty, and Guid.Parse methods.
/// </summary>
public class GuidTypeStrategy : IConversionStrategy
{
    public bool CanConvert(SyntaxNode node, ConversionContext context)
    {
        // Check for Guid.Member usage
        if (node is MemberAccessExpressionSyntax access)
        {
            return IsGuidAccess(access, context);
        }
        
        if (node is InvocationExpressionSyntax invocation && 
            invocation.Expression is MemberAccessExpressionSyntax methodAccess)
        {
            return IsGuidAccess(methodAccess, context);
        }

        return false;
    }

    private bool IsGuidAccess(MemberAccessExpressionSyntax access, ConversionContext context)
    {
        // Semantic Check
        var symbol = context.SemanticHelper.GetSymbol(access.Expression);
        if (symbol is ITypeSymbol typeSymbol && typeSymbol.ToDisplayString() == "System.Guid")
        {
            return true;
        }

        // Fallback: Check for "Guid" identifier
        if (access.Expression is IdentifierNameSyntax identifier && identifier.Identifier.Text == "Guid")
        {
            return true;
        }

        return false;
    }

    public string Convert(SyntaxNode node, ConversionContext context)
    {
        if (node is MemberAccessExpressionSyntax access && access.Name.Identifier.Text == "Empty")
        {
            return "\"00000000-0000-0000-0000-000000000000\"";
        }

        if (node is InvocationExpressionSyntax invocation && 
            invocation.Expression is MemberAccessExpressionSyntax methodAccess)
        {
            var methodName = methodAccess.Name.Identifier.Text;
            if (methodName == "NewGuid")
            {
                return "crypto.randomUUID()";
            }
            if (methodName == "Parse" && invocation.ArgumentList.Arguments.Count > 0)
            {
                // Guid.Parse(string) -> just use string
                return context.Converter.ConvertExpression(invocation.ArgumentList.Arguments[0].Expression);
            }
        }

        return node.ToString();
    }

    public int Priority => 10;
}
