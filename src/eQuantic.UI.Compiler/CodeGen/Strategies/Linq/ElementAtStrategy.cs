using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace eQuantic.UI.Compiler.CodeGen.Strategies.Linq;

public class ElementAtStrategy : IConversionStrategy
{
    public bool CanConvert(SyntaxNode node, ConversionContext context)
    {
        if (node is not InvocationExpressionSyntax invocation) return false;
        if (invocation.Expression is not MemberAccessExpressionSyntax memberAccess) return false;
        if (memberAccess.Name.Identifier.Text != "ElementAt" && memberAccess.Name.Identifier.Text != "ElementAtOrDefault") return false;

        var symbol = context.SemanticHelper.GetSymbol(invocation);
        if (symbol is IMethodSymbol ms && context.SemanticHelper.IsLinqExtension(ms.ContainingType)) return true;

        return symbol == null && context.CanGuess(node);
    }

    public string Convert(SyntaxNode node, ConversionContext context)
    {
        var invocation = (InvocationExpressionSyntax)node;
        var memberAccess = (MemberAccessExpressionSyntax)invocation.Expression;
        var caller = context.Converter.ConvertExpression(memberAccess.Expression);
        var args = invocation.ArgumentList.Arguments;

        if (args.Count == 1)
        {
            var index = context.Converter.ConvertExpression(args[0].Expression);
            // ElementAt(i) is source[i]; ElementAtOrDefault(i) answers the ELEMENT's default when
            // the index is out of range, where a bare lookup would hand back undefined.
            if (memberAccess.Name.Identifier.Text != "ElementAtOrDefault") return $"{caller}[{index}]";
            var fallback = DefaultValue.OfElement(context.SemanticHelper.GetType(memberAccess.Expression), context);
            return $"({caller}[{index}] ?? {fallback})";
        }

        return caller;
    }

    public int Priority => 10;
}
