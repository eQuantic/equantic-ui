using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace eQuantic.UI.Compiler.CodeGen.Strategies.Expressions;

/// <summary>
/// Strategy for stackalloc array creation.
/// Handles: stackalloc int[10] -> new Int32Array(10) or generic Array
/// </summary>
public class StackAllocArrayCreationStrategy : IConversionStrategy
{
    public bool CanConvert(SyntaxNode node, ConversionContext context)
    {
        return node is StackAllocArrayCreationExpressionSyntax;
    }

    public string Convert(SyntaxNode node, ConversionContext context)
    {
        var stackAlloc = (StackAllocArrayCreationExpressionSyntax)node;
        
        string typeName = "var"; // default
        if (stackAlloc.Type is ArrayTypeSyntax arrayType)
        {
            typeName = arrayType.ElementType.ToString();
        }
        else 
        {
            typeName = stackAlloc.Type.ToString();
        }
        
        string jsType = typeName switch
        {
            "int" => "Int32Array",
            "uint" => "Uint32Array",
            "byte" => "Uint8Array",
            "sbyte" => "Int8Array",
            "float" => "Float32Array",
            "double" => "Float64Array",
            "short" => "Int16Array",
            "ushort" => "Uint16Array",
            _ => "Array"
        };
        
        // stackalloc int[length] or stackalloc [] { 1, 2 }
        if (stackAlloc.Initializer != null)
        {
             var elements = string.Join(", ", stackAlloc.Initializer.Expressions
                 .Select(e => context.Converter.ConvertExpression(e)));
                 
             return $"new {jsType}([{elements}])";
        }
        else
        {
             // Try to find length from the ArrayRankSpecifier
             // Syntax: stackalloc int [ expr ]
             // The Type Syntax usually contains the rank if parsed that way, but StackAllocArrayCreationExpressionSyntax has a Type property of type TypeSyntax.
             // Wait, stackalloc syntax is `stackalloc Type [ expr ]`.
             // Roslyn syntax tree structure for `stackalloc int[10]`:
             // StackAllocArrayCreationExpression
             //   Type: ArrayType
             //     ElementType: PredefinedType (int)
             //     RankSpecifiers: [10]
             
             if (stackAlloc.Type is ArrayTypeSyntax arrayTypeForSize && arrayTypeForSize.RankSpecifiers.Count > 0)
             {
                 var size = context.Converter.ConvertExpression(arrayTypeForSize.RankSpecifiers[0].Sizes[0]);
                 return $"new {jsType}({size})";
             }
             
             return $"new {jsType}(0)";
        }
    }

    public int Priority => 10;
}
