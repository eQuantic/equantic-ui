using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace eQuantic.UI.Compiler.CodeGen.Strategies.Expressions;

public class SizeOfExpressionStrategy : IConversionStrategy
{
    public bool CanConvert(SyntaxNode node, ConversionContext context)
    {
        return node is SizeOfExpressionSyntax;
    }

    public string Convert(SyntaxNode node, ConversionContext context)
    {
        var sizeOf = (SizeOfExpressionSyntax)node;
        var type = sizeOf.Type.ToString();

        // Standard C# sizes
        return type switch
        {
            "bool" or "byte" or "sbyte" => "1",
            "char" or "short" or "ushort" => "2",
            "int" or "uint" or "float" => "4",
            "long" or "ulong" or "double" => "8",
            "decimal" => "16",
            _ => "4" // Fallback to 4 for pointers/references
        };
    }

    public int Priority => 10;
}
