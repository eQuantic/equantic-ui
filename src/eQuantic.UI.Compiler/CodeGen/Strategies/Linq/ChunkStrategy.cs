using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace eQuantic.UI.Compiler.CodeGen.Strategies.Linq;

/// <summary>
/// Converts LINQ .Chunk(size) to an array of size-bounded slices (JS has no native Chunk).
/// </summary>
public class ChunkStrategy : IConversionStrategy
{
    public bool CanConvert(SyntaxNode node, ConversionContext context)
    {
        if (node is not InvocationExpressionSyntax invocation) return false;
        if (invocation.Expression is not MemberAccessExpressionSyntax memberAccess) return false;
        if (memberAccess.Name.Identifier.Text != "Chunk") return false;

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
        if (args.Count == 0) return caller;

        var size = context.Converter.ConvertExpression(args[0].Expression);
        return $"(arr => {{ const n = {size}; const out = []; for (let i = 0; i < arr.length; i += n) " +
               $"out.push(arr.slice(i, i + n)); return out; }})({caller})";
    }

    public int Priority => 10;
}
