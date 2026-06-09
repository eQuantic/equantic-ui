using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace eQuantic.UI.Compiler.CodeGen.Strategies.Expressions;

/// <summary>
/// Strategy for array creation.
/// Handles:
/// - new[] { 1, 2, 3 }      → [1, 2, 3]   (implicit)
/// - new int[] { 1, 2, 3 }  → [1, 2, 3]   (explicit, with initializer)
/// - new int[5]             → new Array(5).fill(0)  (sized, default-initialized)
/// </summary>
public class ArrayCreationStrategy : IConversionStrategy
{
    public bool CanConvert(SyntaxNode node, ConversionContext context)
    {
        return node is ArrayCreationExpressionSyntax or ImplicitArrayCreationExpressionSyntax;
    }

    public string Convert(SyntaxNode node, ConversionContext context)
    {
        var initializer = node switch
        {
            ImplicitArrayCreationExpressionSyntax implicitArray => implicitArray.Initializer,
            ArrayCreationExpressionSyntax array => array.Initializer,
            _ => null
        };

        // Initialized array → JS array literal.
        if (initializer != null)
        {
            var elements = initializer.Expressions.Select(e => context.Converter.ConvertExpression(e));
            return $"[{string.Join(", ", elements)}]";
        }

        // Sized array without initializer: new T[n] → new Array(n).fill(<default for T>).
        if (node is ArrayCreationExpressionSyntax sized
            && sized.Type.RankSpecifiers.Count > 0
            && sized.Type.RankSpecifiers[0].Sizes.Count > 0
            && sized.Type.RankSpecifiers[0].Sizes[0] is { } sizeExpr
            && sizeExpr is not OmittedArraySizeExpressionSyntax)
        {
            var sizeJs = context.Converter.ConvertExpression(sizeExpr);
            return $"new Array({sizeJs}).fill({DefaultFill(sized.Type.ElementType)})";
        }

        return "[]";
    }

    private static string DefaultFill(TypeSyntax elementType) => elementType.ToString() switch
    {
        "int" or "long" or "short" or "byte" or "sbyte" or "uint" or "ulong" or "ushort"
            or "double" or "float" or "decimal" => "0",
        "bool" => "false",
        _ => "null"
    };

    public int Priority => 0;
}
