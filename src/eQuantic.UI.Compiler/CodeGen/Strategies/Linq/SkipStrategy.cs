using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace eQuantic.UI.Compiler.CodeGen.Strategies.Linq;

/// <summary>
/// Converts LINQ .Skip(n) to JavaScript .slice(n)
/// </summary>
public class SkipStrategy : IConversionStrategy
{
    public bool CanConvert(SyntaxNode node, ConversionContext context)
    {
        if (node is not InvocationExpressionSyntax invocation)
            return false;

        if (invocation.Expression is not MemberAccessExpressionSyntax memberAccess)
            return false;

        if (memberAccess.Name.Identifier.Text != "Skip")
            return false;

        // Semantic Check
        var symbol = context.SemanticHelper.GetSymbol(invocation);
        if (symbol is IMethodSymbol ms && context.SemanticHelper.IsLinqExtension(ms.ContainingType))
        {
            return true;
        }

        // Name decides ONLY where guessing is honest — see ConversionContext.CanGuess. Under an
        // AUTHORITATIVE model, in-tree-but-unbindable is reported (EQ2006), never guessed.
        if (symbol == null && context.CanGuess(node))
        {
            return true;
        }

        return false;
    }

    public string Convert(SyntaxNode node, ConversionContext context)
    {
        var invocation = (InvocationExpressionSyntax)node;
        var memberAccess = (MemberAccessExpressionSyntax)invocation.Expression;

        var caller = context.Converter.ConvertExpression(memberAccess.Expression);
        var args = invocation.ArgumentList.Arguments;

        if (args.Count > 0)
        {
            var count = context.Converter.ConvertExpression(args[0].Expression);
            // A negative count skips NOTHING in .NET; in JavaScript it slices from the END, so
            // `Skip(-1)` quietly returned just the last element. A literal count needs no guard.
            return NonNegativeLiteral(count)
                ? $"{caller}.slice({count})"
                : $"{caller}.slice(Math.max(0, {count}))";
        }

        return caller;
    }

    /// <summary>Whether the emitted count is a literal that cannot be negative — the common
    /// case, which keeps its plain slice.</summary>
    private static bool NonNegativeLiteral(string count) =>
        count.Length > 0 && count.All(char.IsAsciiDigit);

    public int Priority => 10;
}
