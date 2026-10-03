using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace eQuantic.UI.Compiler.CodeGen.Strategies.Invocation;

/// <summary>
/// <c>ToList()</c> and <c>ToArray()</c>, LINQ's and a list's own: a new array of the source's elements
/// (<see cref="Linq.LinqSource.Copy"/>). They were a passthrough, so a copy and its source were one
/// array, and a set, a dictionary or a string stayed what it was.
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
        if (methodName is not ("ToList" or "ToArray")) return false;
        // LINQ's, or a BCL collection's own (a list's, a queue's, an immutable array's): each answers a
        // new array of its elements in the order it enumerates them, which the runtime's twins and
        // arrays give by iteration. A type of the app's own keeps the method it wrote.
        return context.SemanticHelper.GetSymbol(invocation) switch
        {
            IMethodSymbol method => context.SemanticHelper.IsLinqExtension(method.ContainingType)
                || method.ContainingType.ContainingNamespace?.ToDisplayString() is { } space
                    && (space == "System" || space.StartsWith("System.", System.StringComparison.Ordinal)),
            _ => context.CanGuess(node),
        };
    }

    public string Convert(SyntaxNode node, ConversionContext context)
    {
        var invocation = (InvocationExpressionSyntax)node;
        var memberAccess = (MemberAccessExpressionSyntax)invocation.Expression;
        
        return Ir.JsExprWriter.Write(Linq.LinqSource.Copy(memberAccess.Expression, context));
    }

    public int Priority => 10;
}
