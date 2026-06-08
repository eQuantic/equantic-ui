using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using eQuantic.UI.Compiler.Services;

namespace eQuantic.UI.Compiler.CodeGen.Strategies.Expressions;

/// <summary>
/// Strategy for binary expressions (operators).
/// Handles:
/// - == -> === (strict)
/// - != -> !==
/// - &&, || pass through
/// </summary>
public class BinaryExpressionStrategy : IConversionStrategy
{
    public bool CanConvert(SyntaxNode node, ConversionContext context)
    {
        return node is BinaryExpressionSyntax;
    }

    private static readonly SpecialType[] IntegralTypes =
    {
        SpecialType.System_SByte, SpecialType.System_Byte,
        SpecialType.System_Int16, SpecialType.System_UInt16,
        SpecialType.System_Int32, SpecialType.System_UInt32,
        SpecialType.System_Int64, SpecialType.System_UInt64,
    };

    private static bool IsIntegral(ITypeSymbol? type)
    {
        if (type == null) return false;

        // Unwrap Nullable<T> (int? / int? still divides as integers).
        if (type is INamedTypeSymbol named
            && named.OriginalDefinition?.SpecialType == SpecialType.System_Nullable_T
            && named.TypeArguments.Length == 1)
        {
            type = named.TypeArguments[0];
        }

        return Array.IndexOf(IntegralTypes, type.SpecialType) >= 0;
    }

    public string Convert(SyntaxNode node, ConversionContext context)
    {
        var binary = (BinaryExpressionSyntax)node;
        var left = context.Converter.ConvertExpression(binary.Left);
        var right = context.Converter.ConvertExpression(binary.Right);
        var op = binary.OperatorToken.Text;

        // C# integer division truncates toward zero; JS `/` is always float division.
        // When the result type is integral, emit Math.trunc to preserve C# semantics
        // (7 / 2 == 3, not 3.5). Chained divisions nest correctly.
        if (op == "/" && IsIntegral(context.SemanticHelper.GetType(binary)))
        {
            return $"Math.trunc({left} / {right})";
        }

        // Convert C# operators to JS equivalents
        // Use loose equality for null checks to catch both null and undefined
        if ((left == "null" || right == "null") && (op == "==" || op == "!="))
        {
            // Keep op as == or != (loose)
        }
        else
        {
            op = op switch
            {
                "&&" => "&&",
                "||" => "||",
                "==" => "===", // Use strict equality in JS for non-null
                "!=" => "!==",
                _ => op
            };
        }

        return $"{left} {op} {right}";
    }

    public int Priority => 0; 
}
