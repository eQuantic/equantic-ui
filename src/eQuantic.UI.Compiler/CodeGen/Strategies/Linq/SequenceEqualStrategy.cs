using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace eQuantic.UI.Compiler.CodeGen.Strategies.Linq;

public class SequenceEqualStrategy : IConversionStrategy
{
    public bool CanConvert(SyntaxNode node, ConversionContext context)
    {
        if (node is not InvocationExpressionSyntax invocation) return false;
        if (invocation.Expression is not MemberAccessExpressionSyntax memberAccess) return false;
        if (memberAccess.Name.Identifier.Text != "SequenceEqual") return false;

        // An ARRAY receiver binds MemoryExtensions.SequenceEqual (through ReadOnlySpan) rather
        // than Enumerable's — a better overload the modern BCL added — and the LINQ gate alone
        // then refused the call, leaving `MemoryExtensions.sequenceEqual(…)`, a name that exists
        // nowhere. Both spell the same element-wise comparison.
        var symbol = context.SemanticHelper.GetSymbol(invocation);
        if (symbol is IMethodSymbol ms
            && (context.SemanticHelper.IsLinqExtension(ms.ContainingType)
                || ms.ContainingType?.Name == "MemoryExtensions")) return true;

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
            var other = context.Converter.ConvertExpression(args[0].Expression);
            // JSON.stringify approach for simplicity in UI context
            return $"(JSON.stringify({caller}) === JSON.stringify({other}))";
        }

        return caller;
    }

    public int Priority => 10;
}
