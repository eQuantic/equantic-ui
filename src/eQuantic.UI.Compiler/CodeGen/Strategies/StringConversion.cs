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
/// the empty string, an enum is its member NAME, a value the browser holds as data is its record
/// text, and a number and a date are written in the culture in force, as .NET writes them (#454): a
/// fraction with the culture's separator (<c>1,5</c> in pt-BR), a negative integer with its minus
/// sign (sv-SE's is U+2212), a float in its own digits, a date in its type's own pattern. An
/// unsigned integer and a char read the same in every culture, and are left as they are, as is a
/// string known to be non-null and an integer constant that is not negative.
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

        // A literal or an interpolated string is never null.
        if (type.SpecialType == SpecialType.System_String
            && operand is LiteralExpressionSyntax or InterpolatedStringExpressionSyntax)
            return converted;

        // An integer constant that is not negative is its digits in every culture.
        if (IsNonNegativeIntegerConstant(operand, context)) return converted;

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

        // A string that MAY be null reads as itself or as nothing — the cheapest faithful spelling.
        // Annotated `string?` says so; a string from code with no nullable context (annotation
        // None) has not said it cannot be. Only a `string` under nullable-enabled code has.
        if (type.SpecialType == SpecialType.System_String)
            return type.NullableAnnotation == NullableAnnotation.NotAnnotated
                ? converted
                : JsExpr.Binary(converted, "??", JsExpr.Literal("''"));

        var real = type.UnwrapNullable() ?? type;

        // A value the browser holds as DATA (`[TwinIsData]`, `Color`) is a plain object, whose own
        // string is `[object Object]`. It reads as the record text .NET writes, from the members
        // .NET prints, and a null one as nothing.
        if (real is INamedTypeSymbol data && data.TwinIsData())
            return JsExpr.Callish(RecordText(text, data.Name, data.PrintedMembers()
                .Select(member => (member.Name, (ITypeSymbol?)(member as IFieldSymbol)?.Type ?? (member as IPropertySymbol)?.Type)),
                context));

        // A number or a date is its text in the culture in force, through the formatter that writes
        // every other number and date (#454): JavaScript's own string of it is invariant, so a
        // pt-BR page read `1.5` where the server had rendered `1,5`, and a float spelled the double
        // underneath. A null one writes nothing, which the formatter answers too.
        if (WritesInTheCulture(real))
        {
            context.UsedHelpers.Add(Eq.Import);
            var kind = FormatKind.OfText(real) is { } named ? $", undefined, undefined, '{named}'" : "";
            return JsExpr.Callish($"{Eq.Format}({text}, null{kind})");
        }

        if (!NeedsFormatting(type)) return converted;

        context.UsedHelpers.Add(Eq.Import);
        return JsExpr.Callish($"{Eq.Format}({text}, null)");
    }

    /// <summary>
    /// A record's text, as its PrintMembers writes it (<c>Name { A = 1, B = x }</c>), through the
    /// runtime's record text, which reads each member under its twin's name: a record the browser holds
    /// as a class (its <c>toString</c>) and one it holds as data alike. Each member is written as a
    /// concatenation would write it, which the runtime's formatter answers from the value alone where
    /// its type says nothing more, and from the member's kind or a function of the value where it does:
    /// a float's own digits, an integer's unsigned zero, an enum's member name, a data twin's own text.
    /// A member whose type is not known is written from its value.
    /// </summary>
    internal static string RecordText(string value, string name,
        IEnumerable<(string Name, ITypeSymbol? Type)> members, ConversionContext context)
    {
        context.UsedHelpers.Add(Eq.Import);
        string Member((string Name, ITypeSymbol? Type) member)
        {
            var label = JsStringLiteral.Quote(member.Name);
            var real = member.Type.UnwrapNullable() ?? member.Type;
            if (real is INamedTypeSymbol named && (named.TypeKind == TypeKind.Enum || named.TwinIsData()))
                return $"[{label}, {JsExprWriter.Write(JsExpr.Arrow(context.TypeAnnotations ? "v: any" : "v",
                    Of(member.Type!, JsExpr.Identifier("v"), context)))}]";
            return real is not null && WritesInTheCulture(real) && FormatKind.OfText(real) is { } kind
                ? $"[{label}, '{kind}']"
                : label;
        }
        return $"{Eq.RecordText}({value}, {JsStringLiteral.Quote(name)}, [{string.Join(", ", members.Select(Member))}])";
    }

    /// <summary>
    /// The value's text, as its <c>ToString()</c> writes it: what a concatenation writes, and
    /// <c>String()</c> of what a concatenation leaves as it is, since a number on its own is not text.
    /// </summary>
    internal static JsExpr ToText(ExpressionSyntax operand, JsExpr converted, ConversionContext context)
    {
        var text = ToDotNetString(operand, converted, context);
        return ReferenceEquals(text, converted)
            && context.SemanticHelper.GetType(operand)?.SpecialType != SpecialType.System_String
            ? JsExpr.Callish($"String({JsExprWriter.Write(converted)})")
            : text;
    }

    /// <summary>
    /// The value's text in the INVARIANT culture, as <c>ToString(CultureInfo.InvariantCulture)</c>
    /// writes it: a number or a date through the formatter in the invariant culture, and anything else
    /// as it reads in every culture.
    /// </summary>
    internal static JsExpr InvariantText(ExpressionSyntax operand, JsExpr converted, ConversionContext context)
    {
        var type = context.SemanticHelper.GetType(operand);
        var real = type.UnwrapNullable() ?? type;
        if (real is null || !WritesInTheCulture(real)) return ToText(operand, converted, context);
        context.UsedHelpers.Add(Eq.Import);
        var kind = FormatKind.OfText(real) is { } named ? $", '{named}'" : "";
        return JsExpr.Callish($"{Eq.Format}({JsExprWriter.Write(converted)}, null, undefined, true{kind})");
    }

    /// <summary>
    /// Whether the culture changes this type's text: a fraction's separator, a signed integer's
    /// minus sign and a date's patterns. An unsigned integer, a char and a TimeSpan (whose text is
    /// invariant in .NET) read the same everywhere.
    /// </summary>
    private static bool WritesInTheCulture(ITypeSymbol type) =>
        type.SpecialType is SpecialType.System_Double or SpecialType.System_Single
            or SpecialType.System_Decimal or SpecialType.System_SByte or SpecialType.System_Int16
            or SpecialType.System_Int32 or SpecialType.System_Int64 or SpecialType.System_IntPtr
        || type.IsDate();

    /// <summary>An integer constant that is not negative: its digits are its text in every culture.</summary>
    private static bool IsNonNegativeIntegerConstant(ExpressionSyntax operand, ConversionContext context) =>
        context.SemanticHelper.TryGetConstantValue(operand, out var value)
        && value is sbyte and >= 0 or short and >= 0 or int and >= 0 or long and >= 0;

    /// <summary>Whether JavaScript's own string of this type differs from .NET's: booleans, anything
    /// nullable, and a bare <c>object</c> (which may hold either).</summary>
    private static bool NeedsFormatting(ITypeSymbol type) =>
        type.SpecialType is SpecialType.System_Boolean or SpecialType.System_Object
        || type.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T
        || (type.IsReferenceType && type.NullableAnnotation == NullableAnnotation.Annotated);
}
