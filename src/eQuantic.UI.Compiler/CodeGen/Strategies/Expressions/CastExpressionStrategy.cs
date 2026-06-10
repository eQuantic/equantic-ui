using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

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

        // Specific cases for numeric truncation
        if (type == "int" || type == "long" || type == "short" || type == "byte")
        {
            return $"Math.trunc({inner})";
        }

        if (type == "string")
        {
            return $"String({inner})";
        }

        // Default passthrough for other types (compile-time assertion)
        return inner;
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
    private static string BuildNameToValueMap(INamedTypeSymbol enumType)
        => "{ " + string.Join(", ", EnumMembers(enumType)
            .Select(f => $"'{f.Name.ToCamelCase()}': {ToLong(f.ConstantValue).ToString(CultureInfo.InvariantCulture)}")) + " }";

    // { 0: 'low', 5: 'medium', 10: 'high' } — underlying value → member-name string.
    private static string BuildValueToNameMap(INamedTypeSymbol enumType)
        => "{ " + string.Join(", ", EnumMembers(enumType)
            .Select(f => $"{ToLong(f.ConstantValue).ToString(CultureInfo.InvariantCulture)}: '{f.Name.ToCamelCase()}'")) + " }";

    private static string? MemberNameForValue(INamedTypeSymbol enumType, long value)
        => EnumMembers(enumType).FirstOrDefault(f => ToLong(f.ConstantValue) == value)?.Name;

    private static IEnumerable<IFieldSymbol> EnumMembers(INamedTypeSymbol enumType)
        => enumType.GetMembers().OfType<IFieldSymbol>().Where(f => f.HasConstantValue);

    public int Priority => 10;
}
