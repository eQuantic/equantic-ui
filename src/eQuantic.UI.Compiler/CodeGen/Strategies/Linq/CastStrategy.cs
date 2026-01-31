using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace eQuantic.UI.Compiler.CodeGen.Strategies.Linq;

/// <summary>
/// Strategy for Cast LINQ method.
/// Handles: source.Cast&lt;T&gt;() -> source (passthrough with type assertion comment)
/// Cast is primarily a compile-time type assertion in TypeScript.
/// </summary>
public class CastStrategy : IConversionStrategy
{
    public bool CanConvert(SyntaxNode node, ConversionContext context)
    {
        return context.SemanticHelper.IsLinqMethod(node, "Cast");
    }

    public string Convert(SyntaxNode node, ConversionContext context)
    {
        var invocation = (InvocationExpressionSyntax)node;
        var memberAccess = (MemberAccessExpressionSyntax)invocation.Expression;
        var source = context.Converter.ConvertExpression(memberAccess.Expression);

        // Cast<T>() is a type assertion in C#, but in JavaScript/TypeScript
        // we can just pass through the array since JS is dynamically typed
        // TypeScript will handle the type assertion at compile time
        return source;
    }

    public int Priority => 10;
}
