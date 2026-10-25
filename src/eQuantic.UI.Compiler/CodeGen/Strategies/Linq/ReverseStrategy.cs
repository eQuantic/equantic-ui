using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace eQuantic.UI.Compiler.CodeGen.Strategies.Linq;

/// <summary>
/// Converts LINQ's <c>Reverse()</c>, a reversed COPY of the sequence, to <c>[...source].reverse()</c>, which
/// leaves the source as it was. <c>List&lt;T&gt;.Reverse()</c> is not this: it reverses the list in place and
/// answers nothing, and it is <see cref="Primitives.ListMethodStrategy"/>'s (#1076).
/// </summary>
public class ReverseStrategy : IConversionStrategy
{
    public bool CanConvert(SyntaxNode node, ConversionContext context)
    {
        if (node is not InvocationExpressionSyntax invocation)
            return false;

        if (invocation.Expression is not MemberAccessExpressionSyntax memberAccess)
            return false;

        if (memberAccess.Name.Identifier.ValueText != "Reverse")
            return false;

        var symbol = context.SemanticHelper.GetSymbol(invocation);
        if (symbol is IMethodSymbol ms)
        {
            // Handle LINQ Enumerable.Reverse<T>()
            if (context.SemanticHelper.IsLinqExtension(ms.ContainingType))
                return true;

        }

        // Name decides ONLY where guessing is honest — see ConversionContext.CanGuess. Under an
        // AUTHORITATIVE model, in-tree-but-unbindable is reported (EQ2006), never guessed.
        if (symbol == null && context.CanGuess(node))
            return true;

        return false;
    }

    public string Convert(SyntaxNode node, ConversionContext context)
    {
        var invocation = (InvocationExpressionSyntax)node;
        var memberAccess = (MemberAccessExpressionSyntax)invocation.Expression;

        var caller = LinqSource.Text(memberAccess.Expression, context);

        // Use spread to create a copy before reversing (JS reverse mutates in place)
        return $"[...{caller}].reverse()";
    }

    public int Priority => 10;
}
