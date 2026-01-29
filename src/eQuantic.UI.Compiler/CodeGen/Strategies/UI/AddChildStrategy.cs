using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace eQuantic.UI.Compiler.CodeGen.Strategies.UI;

/// <summary>
/// Strategy for AddChild method.
/// Handles: component.AddChild(child) → component.Children.push(child)
/// This is a common pattern in imperative component building.
/// </summary>
public class AddChildStrategy : IConversionStrategy
{
    public bool CanConvert(SyntaxNode node, ConversionContext context)
    {
        if (node is not InvocationExpressionSyntax invocation)
            return false;

        if (invocation.Expression is not MemberAccessExpressionSyntax memberAccess)
            return false;

        return memberAccess.Name.Identifier.Text == "AddChild";
    }

    public string Convert(SyntaxNode node, ConversionContext context)
    {
        var invocation = (InvocationExpressionSyntax)node;
        var memberAccess = (MemberAccessExpressionSyntax)invocation.Expression;
        
        var component = context.Converter.ConvertExpression(memberAccess.Expression);
        
        var args = string.Join(", ", 
            invocation.ArgumentList.Arguments.Select(a => 
                context.Converter.ConvertExpression(a.Expression)));
        
        return $"{component}.Children.push({args})";
    }

    public int Priority => 10;
}
