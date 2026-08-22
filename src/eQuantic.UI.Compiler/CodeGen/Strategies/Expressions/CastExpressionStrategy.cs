using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using eQuantic.UI.Compiler.CodeGen.Ir;

namespace eQuantic.UI.Compiler.CodeGen.Strategies.Expressions;

public class CastExpressionStrategy : IConversionStrategy
{
    public bool CanConvert(SyntaxNode node, ConversionContext context)
    {
        return node is CastExpressionSyntax;
    }

    public string Convert(SyntaxNode node, ConversionContext context)
    {
        var cast = (CastExpressionSyntax)node;

        // An EXPLICIT user-defined conversion on an in-source type calls the operator its twin
        // carries (`(int)money` → Money.toInt(money)); a framework wrapper passes through below.
        if (context.SemanticHelper.GetOperation(cast) is Microsoft.CodeAnalysis.Operations.IConversionOperation
            { Conversion: { IsUserDefined: true, MethodSymbol: { } conversionMethod } }
            && UserDefinedOperators.Conversion(conversionMethod, context.Converter.ConvertExpression(cast.Expression)) is { } call)
            return JsExprWriter.Write(call);

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
                return ToLong(constant).ToString(CultureInfo.InvariantCulture);

            var expr = context.Converter.ConvertExpression(cast.Expression);
            // A [Flags] enum is already numeric at runtime — the cast is the identity. A normal (string)
            // enum needs its member-name string mapped back to the underlying value.
            return enumOperand.IsFlagsEnum() ? expr : $"({BuildNameToValueMap(enumOperand)})[{expr}]";
        }

        // (EnumType)int → a value of the enum.
        if (targetType is INamedTypeSymbol { TypeKind: TypeKind.Enum } enumTarget)
        {
            var flags = enumTarget.IsFlagsEnum();
            if (context.SemanticHelper.TryGetConstantValue(cast.Expression, out var constant))
            {
                // Flags enums are numeric — keep the literal value. Normal enums map the value to the
                // member-name string (so it stays comparable to other enum members).
                if (flags) return ToLong(constant).ToString(CultureInfo.InvariantCulture);
                var name = MemberNameForValue(enumTarget, ToLong(constant));
                if (name != null) return $"'{name.ToCamelCase()}'";
            }

            var expr = context.Converter.ConvertExpression(cast.Expression);
            // Flags: the int IS the runtime value (identity). Normal: map value → member-name string.
            return flags ? expr : $"({BuildValueToNameMap(enumTarget)})[{expr}]";
        }

        var inner = context.Converter.ConvertExpression(cast.Expression);
        var type = cast.Type.ToString();

        // (char)numeric → the character for the code unit. The transpiled char IS a 1-length
        // string, so without this the cast silently vanished and `(char)('A' + i)` stayed a number
        // (or, worse, a concatenation the char-arithmetic branch had already prevented).
        if (UnwrapNullable(targetType)?.SpecialType == SpecialType.System_Char
            || (targetType is null && type == "char"))
        {
            if (operandType?.SpecialType == SpecialType.System_Char) return inner;   // identity
            return $"String.fromCharCode({inner})";
        }

        // Integral casts carry C#'s RANGE WRAPPING, not just float truncation: `(byte)(rgb >> 8)`
        // keeps only the low 8 bits. Emitting bare Math.trunc left the high bits in — found when
        // every brand color built through `Color.FromRgb((byte)(v >> 16), …)` corrupted on the
        // client (#9b8cff became #9b9b8c9b8cff) while the C# SSR was fine.
        // A NULLABLE target keeps C#'s null propagation: `(int?)null` is null, never 0 — the
        // wrap only applies to a non-null operand (IIFE avoids double evaluation).
        var special = UnwrapNullable(targetType)?.SpecialType;
        var nullableTarget = targetType is INamedTypeSymbol { OriginalDefinition.SpecialType: SpecialType.System_Nullable_T };
        string? wrapped = special switch
        {
            SpecialType.System_Byte => WrapIntegral(inner, "(Math.trunc({0}) & 0xFF)", nullableTarget),
            SpecialType.System_SByte => WrapIntegral(inner, "((Math.trunc({0}) << 24) >> 24)", nullableTarget),
            SpecialType.System_Int16 => WrapIntegral(inner, "((Math.trunc({0}) << 16) >> 16)", nullableTarget),
            SpecialType.System_UInt16 => WrapIntegral(inner, "(Math.trunc({0}) & 0xFFFF)", nullableTarget),
            SpecialType.System_Int32 => WrapIntegral(inner, "(Math.trunc({0}) | 0)", nullableTarget),
            SpecialType.System_UInt32 => WrapIntegral(inner, "(Math.trunc({0}) >>> 0)", nullableTarget),
            // 64-bit wrapping has no plain-number twin; truncation is the JS-representable part.
            SpecialType.System_Int64 or SpecialType.System_UInt64 => WrapIntegral(inner, "Math.trunc({0})", nullableTarget),
            _ => null,
        };
        if (wrapped != null) return wrapped;

        // No semantic model — fall back on the spelled type (same table).
        switch (type)
        {
            case "byte": return $"(Math.trunc({inner}) & 0xFF)";
            case "sbyte": return $"((Math.trunc({inner}) << 24) >> 24)";
            case "short": return $"((Math.trunc({inner}) << 16) >> 16)";
            case "ushort": return $"(Math.trunc({inner}) & 0xFFFF)";
            case "int": return $"(Math.trunc({inner}) | 0)";
            case "uint": return $"(Math.trunc({inner}) >>> 0)";
            case "long" or "ulong": return $"Math.trunc({inner})";
        }

        if (type == "string")
        {
            return $"String({inner})";
        }

        // Default passthrough for other types (compile-time assertion)
        return inner;
    }

    /// <summary>The integral wrap, null-propagating when the TARGET is nullable — `(int?)x` is
    /// null for a null x (an IIFE binds the operand once; `{0}` is the format slot).</summary>
    private static string WrapIntegral(string inner, string format, bool nullableTarget)
    {
        if (!nullableTarget) return string.Format(CultureInfo.InvariantCulture, format, inner);
        var body = string.Format(CultureInfo.InvariantCulture, format, "__v");
        return $"((__v) => __v == null ? null : {body})({inner})";
    }

    private static ITypeSymbol? UnwrapNullable(ITypeSymbol? type)
        => type is INamedTypeSymbol named && named.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T
            ? named.TypeArguments[0]
            : type;

    private static bool IsIntegral(ITypeSymbol? type) => type?.SpecialType is
        SpecialType.System_Int32 or SpecialType.System_Int64 or SpecialType.System_Int16 or
        SpecialType.System_Byte or SpecialType.System_SByte or SpecialType.System_UInt16 or
        SpecialType.System_UInt32 or SpecialType.System_UInt64;

    private static long ToLong(object? value) => System.Convert.ToInt64(value, CultureInfo.InvariantCulture);

    // { 'low': 0, 'medium': 5, 'high': 10 } — member-name string → underlying value.
    internal static string BuildNameToValueMap(INamedTypeSymbol enumType)
        => "{ " + string.Join(", ", EnumMembers(enumType)
            .Select(f => $"'{f.Name.ToCamelCase()}': {ToLong(f.ConstantValue).ToString(CultureInfo.InvariantCulture)}")) + " }";

    // { 0: 'low', 5: 'medium', 10: 'high' } — underlying value → member-name string.
    internal static string BuildValueToNameMap(INamedTypeSymbol enumType)
        => "{ " + string.Join(", ", EnumMembers(enumType)
            .Select(f => $"{ToLong(f.ConstantValue).ToString(CultureInfo.InvariantCulture)}: '{f.Name.ToCamelCase()}'")) + " }";

    private static string? MemberNameForValue(INamedTypeSymbol enumType, long value)
        => EnumMembers(enumType).FirstOrDefault(f => ToLong(f.ConstantValue) == value)?.Name;

    private static IEnumerable<IFieldSymbol> EnumMembers(INamedTypeSymbol enumType)
        => enumType.GetMembers().OfType<IFieldSymbol>().Where(f => f.HasConstantValue);

    public int Priority => 10;
}
