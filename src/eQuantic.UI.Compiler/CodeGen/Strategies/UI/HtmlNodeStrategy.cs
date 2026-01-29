using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace eQuantic.UI.Compiler.CodeGen.Strategies.UI;

/// <summary>
/// Strategy for HtmlNode.Text method.
/// Handles: HtmlNode.Text(stringValue) → { tag: '#text', textContent: stringValue }
/// </summary>
public class HtmlNodeStrategy : IConversionStrategy
{
    public bool CanConvert(SyntaxNode node, ConversionContext context)
    {
        if (node is not InvocationExpressionSyntax invocation)
            return false;

        if (invocation.Expression is not MemberAccessExpressionSyntax memberAccess)
            return false;

        // Check for specific method call
        if (memberAccess.Name.Identifier.Text != "Text")
            return false;

        // Check context via semantic model if available
        var symbol = context.SemanticHelper.GetSymbol(invocation) as IMethodSymbol;
        if (symbol != null)
        {
            var typeName = symbol.ContainingType.ToDisplayString();
            return typeName.EndsWith("HtmlNode"); // eQuantic.UI.Core.HtmlNode
        }

        // Fallback: check static access "HtmlNode.Text"
        var caller = memberAccess.Expression.ToString();
        return caller.EndsWith("HtmlNode");
    }

    public string Convert(SyntaxNode node, ConversionContext context)
    {
        var invocation = (InvocationExpressionSyntax)node;
        
        if (invocation.ArgumentList.Arguments.Count == 0)
            return "{ tag: '#text', textContent: '' }";

        var textContent = context.Converter.ConvertExpression(invocation.ArgumentList.Arguments[0].Expression);
        return $"{{ tag: '#text', textContent: {textContent} }}";
    }

    public int Priority => 10;
}
