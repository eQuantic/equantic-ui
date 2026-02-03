using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace eQuantic.UI.Compiler.CodeGen.Strategies.Expressions;

public class TypeofExpressionStrategy : IConversionStrategy
{
    public bool CanConvert(SyntaxNode node, ConversionContext context)
    {
        return node is TypeOfExpressionSyntax;
    }

    public string Convert(SyntaxNode node, ConversionContext context)
    {
        var typeOf = (TypeOfExpressionSyntax)node;
        // typeof(T) -> "T"
        // This is a simplification. Real Type objects don't exist in JS usually.
        // We return the type name as a string literal.
        var typeName = typeOf.Type.ToString();
        return $"'{typeName}'";
    }

    public int Priority => 10;
}
