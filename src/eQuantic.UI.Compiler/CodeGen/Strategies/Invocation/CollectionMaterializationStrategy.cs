using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace eQuantic.UI.Compiler.CodeGen.Strategies.Invocation;

/// <summary>
/// Strategy for removing LINQ materialization calls that are redundant in JS.
/// Handles: ToList(), ToArray() → passthrough
/// </summary>
public class CollectionMaterializationStrategy : IConversionStrategy
{
    public bool CanConvert(SyntaxNode node, ConversionContext context)
    {
        if (node is not InvocationExpressionSyntax invocation)
            return false;

        if (invocation.Expression is not MemberAccessExpressionSyntax memberAccess)
            return false;

        var methodName = memberAccess.Name.Identifier.Text;
        return methodName is "ToList" or "ToArray";
    }

    public string Convert(SyntaxNode node, ConversionContext context)
    {
        var invocation = (InvocationExpressionSyntax)node;
        var memberAccess = (MemberAccessExpressionSyntax)invocation.Expression;
        
        // Passthrough: just convert the expression on which ToList/ToArray was called
        // Since our LINQ strategies (Select, Where) return arrays/iterables that work fine in JS
        return context.Converter.ConvertExpression(memberAccess.Expression);
    }

    public int Priority => 10;
}
