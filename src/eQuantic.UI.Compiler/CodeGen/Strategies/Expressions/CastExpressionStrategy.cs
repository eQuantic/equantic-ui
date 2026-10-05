using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;
using eQuantic.UI.Compiler.CodeGen.Ir;

namespace eQuantic.UI.Compiler.CodeGen.Strategies.Expressions;

/// <summary>
/// An explicit cast. The bound tree names the conversion the cast performs, and the ONE conversion
/// table (<see cref="ValueFlow.Apply"/>) applies it — the same table that settles implicit flows
/// and <c>foreach</c> elements, so <c>(int)aLong</c> slices the BigInt's low 32 bits instead of
/// putting a BigInt into Math.trunc, <c>checked((byte)n)</c> throws where C# throws, and an enum
/// converts as its underlying type does, read off and held back as its key (#551). What stays HERE
/// is the spelled-type fallback for the worlds without a semantic model.
/// </summary>
public class CastExpressionStrategy : IExpressionIrStrategy
{
    public bool CanConvert(SyntaxNode node, ConversionContext context)
    {
        return node is CastExpressionSyntax;
    }

    public JsExpr ConvertIr(SyntaxNode node, ConversionContext context)
    {
        var cast = (CastExpressionSyntax)node;

        // The bound tree names the conversion — user-defined operator, numeric with its widths and
        // representations, an enum's with its keys, nullable with its null propagation, checked with
        // its throw — and the one table applies it, exactly as it would the implicit form of the same
        // conversion.
        if (context.SemanticHelper.GetOperation(cast) is IConversionOperation operation)
        {
            var operand = context.Converter.ConvertIr(cast.Expression);
            // An enum's conversion is checked where its context is, which the bound tree does not
            // report for it (ArithmeticContext.IsCheckedAt).
            var isChecked = operation.IsChecked
                || (Types.EnumOperators.EnumOf(operation.Operand.Type) ?? Types.EnumOperators.EnumOf(operation.Type)) is not null
                && ArithmeticContext.IsCheckedAt(cast, context);
            return ValueFlow.Apply(operation.GetConversion(), operation.Operand.Type, operation.Type,
                operation.ConstantValue.HasValue ? operation.ConstantValue.Value : null,
                operation.Operand.ConstantValue.HasValue ? operation.Operand.ConstantValue.Value : null,
                operand, context, isChecked);
        }

        // No bound tree (a rewritten node, a model-less world): the SPELLED type decides, with the
        // same masks (IntegerWidth) over a truncation — the operand's type is unknowable here, so
        // the truncation stays even for sources that would not need it.
        var inner = context.Converter.ConvertIr(cast.Expression);
        var text = JsExprWriter.WriteIn(inner, JsPrecedence.Call);
        return cast.Type.ToString() switch
        {
            "char" => JsExpr.Callish($"String.fromCharCode({text})"),
            "string" => JsExpr.Callish($"String({text})"),
            "sbyte" => IntegerWidth.Wrap(Truncate(text), (8, false)),
            "byte" => IntegerWidth.Wrap(Truncate(text), (8, true)),
            "short" => IntegerWidth.Wrap(Truncate(text), (16, false)),
            "ushort" => IntegerWidth.Wrap(Truncate(text), (16, true)),
            "int" => IntegerWidth.Wrap(Truncate(text), (32, false)),
            "uint" => IntegerWidth.Wrap(Truncate(text), (32, true)),
            // 64-bit has no plain-number wrap; without a model the truncation is all there is.
            "long" or "ulong" => Truncate(text),
            // Default passthrough for other types (compile-time assertion)
            _ => inner,
        };
    }

    /// <summary>The integer part of a value only spelled, never bound — Math.trunc as text.</summary>
    private static JsExpr Truncate(string text) => JsExpr.Callish($"Math.trunc({text})");

    public int Priority => 10;
}
