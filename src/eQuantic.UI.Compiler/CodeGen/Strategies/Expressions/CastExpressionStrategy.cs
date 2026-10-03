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
/// putting a BigInt into Math.trunc, and <c>checked((byte)n)</c> throws where C# throws. What
/// stays HERE is what only a cast does: the enum name↔value maps (the runtime enum is its
/// member-name string), and the spelled-type fallback for the worlds without a semantic model.
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

        // Enums are represented at runtime by their member-name string (SizeVariant.Medium -> 'medium'),
        // not by their numeric value (see EnumStrategy). A numeric cast therefore can't reach the value the
        // way .NET does — `(int)'medium'` is NaN. Bridge it with the enum's compile-time name↔value table:
        // constant-fold to a literal when we can, else inline a tiny generated map indexed by the operand.
        var targetType = context.SemanticHelper.GetType(cast);
        var operandType = UnwrapNullable(context.SemanticHelper.GetType(cast.Expression));

        // (int)enum / (long)enum / … → the underlying integral value.
        if (operandType is INamedTypeSymbol { TypeKind: TypeKind.Enum } enumOperand
            && IsIntegral(UnwrapNullable(targetType)))
        {
            if (context.SemanticHelper.TryGetConstantValue(cast.Expression, out var constant))
                return JsExpr.Literal(ToLong(constant).ToString(CultureInfo.InvariantCulture));

            // A [Flags] enum is already numeric at runtime — the cast is the identity. A normal (string)
            // enum needs its member-name string mapped back to the underlying value.
            return Types.EnumShape.ValueOf(enumOperand, context.Converter.ConvertIr(cast.Expression), context);
        }

        // (EnumType)int → a value of the enum.
        if (targetType is INamedTypeSymbol { TypeKind: TypeKind.Enum } enumTarget)
        {
            var flags = enumTarget.IsFlagsEnum();
            if (context.SemanticHelper.TryGetConstantValue(cast.Expression, out var constant))
            {
                // Flags enums are numeric — keep the literal value. Normal enums map the value to the
                // member-name string (so it stays comparable to other enum members).
                // A value no member names is held as its number.
                var number = System.Convert.ToDecimal(constant, CultureInfo.InvariantCulture);
                var key = flags ? null : Types.EnumShape.KeyOf(enumTarget, number);
                return JsExpr.Literal(key != null ? $"'{key}'" : number.ToString(CultureInfo.InvariantCulture));
            }

            var operandIr = context.Converter.ConvertIr(cast.Expression);
            // The enum itself is what the browser holds already. An object holding one
            // (`(Status)Enum.Parse(typeof(Status), text)`) is unboxed, as the runtime unboxes it: the
            // member it holds, a boxed number's member, and a refusal for anything else, where the
            // value→key table answered undefined and a boxed string passed through.
            if (SymbolEqualityComparer.Default.Equals(operandType, enumTarget)) return operandIr;
            if (operandType is not null && !IsNumeric(operandType)
                && operandType is not INamedTypeSymbol { TypeKind: TypeKind.Enum })
            {
                context.UsedHelpers.Add(Eq.Import);
                return JsExpr.Call(JsExpr.Identifier(Eq.EnumUnbox), operandIr,
                    JsExpr.Literal(Types.EnumShape.Of(enumTarget)), JsExpr.Literal(JsStringLiteral.Quote(enumTarget.Name)));
            }
            // Another enum's value: its own table gives the number, and this one's gives the key.
            if (operandType is INamedTypeSymbol { TypeKind: TypeKind.Enum } sourceEnum)
                operandIr = Types.EnumShape.ValueOf(sourceEnum, operandIr, context);
            // Flags: the int IS the runtime value (identity). Normal: map value → member-name string.
            return Types.EnumShape.Held(enumTarget, operandIr, context);
        }

        // The bound tree names the conversion — user-defined operator, numeric with its widths and
        // representations, nullable with its null propagation, checked with its throw — and the one
        // table applies it, exactly as it would the implicit form of the same conversion.
        if (context.SemanticHelper.GetOperation(cast) is IConversionOperation operation)
        {
            var operand = context.Converter.ConvertIr(cast.Expression);
            return ValueFlow.Apply(operation.GetConversion(), operation.Operand.Type, operation.Type,
                operation.ConstantValue.HasValue ? operation.ConstantValue.Value : null,
                operation.Operand.ConstantValue.HasValue ? operation.Operand.ConstantValue.Value : null,
                operand, context, operation.IsChecked);
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

    private static ITypeSymbol? UnwrapNullable(ITypeSymbol? type)
        => type is INamedTypeSymbol named && named.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T
            ? named.TypeArguments[0]
            : type;

    private static bool IsIntegral(ITypeSymbol? type) => type?.SpecialType is
        SpecialType.System_Int32 or SpecialType.System_Int64 or SpecialType.System_Int16 or
        SpecialType.System_Byte or SpecialType.System_SByte or SpecialType.System_UInt16 or
        SpecialType.System_UInt32 or SpecialType.System_UInt64;

    private static bool IsNumeric(ITypeSymbol type) => IsIntegral(type)
        || type.SpecialType is SpecialType.System_Single or SpecialType.System_Double or SpecialType.System_Decimal
            or SpecialType.System_Char;

    private static long ToLong(object? value) => System.Convert.ToInt64(value, CultureInfo.InvariantCulture);

    public int Priority => 10;
}
