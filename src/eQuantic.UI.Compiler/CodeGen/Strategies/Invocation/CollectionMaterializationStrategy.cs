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
        // LINQ's, or a List's own ToArray; a runtime collection's own (a queue's, a stack's) is its
        // class's method, and a type of the app's is its own.
        return context.SemanticHelper.GetSymbol(invocation) switch
        {
            IMethodSymbol method => context.SemanticHelper.IsLinqExtension(method.ContainingType)
                || method.ContainingType.OriginalDefinition is { Name: "List", ContainingNamespace: var space }
                    && space.ToDisplayString() == "System.Collections.Generic",
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
