using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using eQuantic.UI.Compiler.CodeGen.Ir;

namespace eQuantic.UI.Compiler.CodeGen.Strategies;

/// <summary>
/// What C# does to a value on its way INTO a string — in <c>"a" + x</c> and in <c>$"{x}"</c> —
/// where JavaScript would do something else. The conversion is the same in both places, so it is
/// decided in one: a null is the empty string (JavaScript writes <c>null</c>), a bool is
/// <c>True</c>/<c>False</c> (JavaScript lowercases), a nullable value type follows its value or
/// the empty string, an enum is its member NAME, a fractional number is written in .NET's
/// notation (<c>1E+17</c>, <c>-0</c>, a float's own digits), and a value the browser holds as data
/// is its record text. Integers, chars, longs and decimals
/// already read the same on both sides; a string known to be non-null is left alone.
/// </summary>
public static class StringConversion
{
    /// <summary>The operand as the string C# would make of it.</summary>
    public static JsExpr ToDotNetString(ExpressionSyntax operand, JsExpr converted, ConversionContext context)
    {
        if (operand is LiteralExpressionSyntax { RawKind: (int)Microsoft.CodeAnalysis.CSharp.SyntaxKind.NullLiteralExpression })
            return JsExpr.Literal("''");

        var type = context.SemanticHelper.GetType(operand);
        if (type is null) return converted;

        // A member written by name folds to the name it is.
        if (type is INamedTypeSymbol { TypeKind: TypeKind.Enum } enumType)
            return JsExpr.Callish(Invocation.ToStringStrategy.EnumNameLookup(enumType, operand, JsExprWriter.Write(converted), context));

        // A string that MAY be null reads as itself or as nothing — the cheapest faithful spelling.
        // Annotated `string?` says so; a string from code with no nullable context (annotation
        // None) has not said it cannot be. Only a `string` under nullable-enabled code has.
        if (type.SpecialType == SpecialType.System_String)
            return type.NullableAnnotation == NullableAnnotation.NotAnnotated
                || operand is LiteralExpressionSyntax or InterpolatedStringExpressionSyntax
                ? converted
                : JsExpr.Binary(converted, "??", JsExpr.Literal("''"));

        return Of(type, converted, context);
    }

    /// <summary>
    /// A value of <paramref name="type"/> as the string C# would make of it, the type alone deciding:
    /// what a concatenation applies once its operand's syntax has had its say, and what
    /// <c>string.Join</c> applies to each element of a sequence of that type (#441).
    /// </summary>
    internal static JsExpr Of(ITypeSymbol type, JsExpr converted, ConversionContext context)
    {
        var text = JsExprWriter.Write(converted);
        // An enum prints its value's name, a [Flags] one its set flags, and a NULLABLE one nothing for
        // null (#452).
        if (type.UnwrapNullable() is INamedTypeSymbol { TypeKind: TypeKind.Enum } enumType)
            return JsExpr.Callish(Types.EnumShape.Text(enumType, text, context));

        // A fractional number, or a nullable one, reads the way .NET writes it (#336). JavaScript's
        // String() keeps fixed notation up to 1e21, drops the sign of -0, and gives a float the
        // digits of the double underneath: "v=" + 0.1f read "v=0.10000000149011612".
        var real = type.UnwrapNullable() ?? type;

        // A value the browser holds as DATA (`[TwinIsData]`, `Color`) is a plain object, whose own
        // string is `[object Object]`. It reads as the record text .NET writes, from the members
        // .NET prints, and a null one as nothing.
        if (real is INamedTypeSymbol data && data.TwinIsData())
        {
            context.UsedHelpers.Add(Eq.Import);
            var members = string.Join(", ", data.PrintedMembers().Select(member => $"'{member.Name}'"));
            return JsExpr.Callish($"{Eq.RecordText}({text}, '{data.Name}', [{members}])");
        }

        if (real.SpecialType is SpecialType.System_Double or SpecialType.System_Single)
        {
            context.UsedHelpers.Add(Eq.Import);
            var printer = real.SpecialType == SpecialType.System_Single ? Eq.Single : Eq.Double;
            return ReferenceEquals(real, type)
                ? JsExpr.Callish($"{printer}({text})")
                : JsExpr.Template($"({{0}} == null ? '' : {printer}({{0}}))", [converted]);
        }

        if (!NeedsFormatting(type)) return converted;

        context.UsedHelpers.Add(Eq.Import);
        return JsExpr.Callish($"{Eq.Format}({text}, null)");
    }

    /// <summary>Whether JavaScript's own string of this type differs from .NET's: booleans, anything
    /// nullable, and a bare <c>object</c> (which may hold either).</summary>
    private static bool NeedsFormatting(ITypeSymbol type) =>
        type.SpecialType is SpecialType.System_Boolean or SpecialType.System_Object
        || type.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T
        || (type.IsReferenceType && type.NullableAnnotation == NullableAnnotation.Annotated);
}
