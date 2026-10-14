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
/// text, a value tuple is <c>(1, a)</c> and an anonymous object <c>{ A = 1 }</c> (#762), and a
/// number and a date are written in the culture in force, as .NET writes them (#454): a
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

        // A value TUPLE is an array here and an ANONYMOUS object a plain object, whose own strings are
        // JavaScript's, `1,2` and `[object Object]`. Each reads as the text .NET writes, from what only
        // its static type knows: a tuple's element types, and an anonymous type's C# member names in
        // their declared order (#762). A null one, an empty Nullable tuple, writes nothing.
        if (real is INamedTypeSymbol { IsTupleType: true } tuple)
            return TupleText(tuple, converted, Eq.TupleText, context);
        if (real is INamedTypeSymbol { IsAnonymousType: true } anonymous)
            return AnonymousText(anonymous, converted, Eq.RecordText, context);

        // A value the browser holds as DATA (`[TwinIsData]`, `Color`) is a plain object, whose own
        // string is `[object Object]`. It reads as the record text .NET writes, from the members
        // .NET prints, and a null one as nothing. A member whose own text its type decides says so,
        // as RecordText writes it: a `Curve`'s points are floats, and JavaScript's digits for 0.2f
        // are the double's, 0.20000000298023224 (#518).
        if (real is INamedTypeSymbol data && data.TwinIsData())
            return RecordText(data, converted, Eq.RecordText, context);

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
    /// The record text of a value the browser holds as data, written by <paramref name="helper"/> over
    /// the members .NET prints: <see cref="Eq.RecordText"/> for the text itself, and
    /// <see cref="Eq.RecordTextGroup"/> for the delegate a <c>ToString</c> method group is, which writes
    /// the same text of the value it was made with (#518).
    /// </summary>
    internal static JsExpr RecordText(INamedTypeSymbol data, JsExpr value, string helper, ConversionContext context) =>
        RecordText(helper, value, data.Name,
            data.PrintedMembers().Select(member => (member.Name, (ITypeSymbol?)TwinData.TypeOf(member))), context);

    /// <summary>
    /// An anonymous object's text, as the <c>ToString</c> C# writes for an anonymous type writes it: a
    /// record's text with no name, <c>{ R = 255, Name = a }</c> and <c>{ }</c>, its members under their C#
    /// names in the order they are declared, each as a concatenation writes it. <paramref name="helper"/>
    /// is <see cref="Eq.RecordText"/> for the text itself and <see cref="Eq.RecordTextGroup"/> for the
    /// delegate a <c>ToString</c> method group is.
    /// </summary>
    internal static JsExpr AnonymousText(INamedTypeSymbol anonymous, JsExpr value, string helper, ConversionContext context) =>
        RecordText(helper, value, "",
            anonymous.GetMembers().OfType<IPropertySymbol>().Select(member => (member.Name, (ITypeSymbol?)member.Type)), context);

    /// <summary>
    /// A value tuple's text, as <c>ValueTuple.ToString</c> writes it (<c>(1, a)</c>), through the runtime's
    /// tuple text, which writes each element as a concatenation writes it: from the element itself where
    /// its type says nothing more, and from what the compiler hands where it does (<see cref="Printed"/>),
    /// up to the last element that says anything. A tuple of eight elements or more is one array here,
    /// and <c>TupleElements</c> lists every one of them, as .NET writes them flat from its <c>Rest</c>.
    /// <paramref name="helper"/> is <see cref="Eq.TupleText"/> for the text itself and
    /// <see cref="Eq.TupleTextGroup"/> for the delegate a <c>ToString</c> method group is (#762).
    /// </summary>
    internal static JsExpr TupleText(INamedTypeSymbol tuple, JsExpr value, string helper, ConversionContext context)
    {
        context.UsedHelpers.Add(Eq.Import);
        var elements = tuple.TupleElements.Select(element => Printed(element.Type, context)).ToList();
        var said = elements.FindLastIndex(element => element is not null) + 1;
        return said == 0
            ? JsExpr.Call(JsExpr.Identifier(helper), value)
            : JsExpr.Call(JsExpr.Identifier(helper), value,
                JsExpr.Array(elements.Take(said).Select(element => element ?? JsExpr.Literal("null")).ToList()));
    }

    /// <summary>
    /// A record's text, as its PrintMembers writes it (<c>Name { A = 1, B = x }</c>), through the
    /// runtime's record text, which reads each member under its twin's name: a record the browser holds
    /// as a class (its <c>toString</c>) and one it holds as data alike, the delegate of a data twin's
    /// <c>ToString</c> method group, which <paramref name="helper"/> names, and an anonymous object's text,
    /// whose <paramref name="name"/> is empty. Each member is written as a concatenation would write it,
    /// which the runtime's formatter answers from the value alone where its type says nothing more, and
    /// from what the compiler hands where it does (<see cref="Printed"/>). A member whose type is not known
    /// is written from its value. One call shape for all of them, <c>helper(value, 'Name', [members])</c>.
    /// </summary>
    internal static JsExpr RecordText(string helper, JsExpr value, string name,
        IEnumerable<(string Name, ITypeSymbol? Type)> members, ConversionContext context)
    {
        context.UsedHelpers.Add(Eq.Import);
        JsExpr Member((string Name, ITypeSymbol? Type) member)
        {
            var label = JsExpr.Literal(JsStringLiteral.Quote(member.Name));
            return Printed(member.Type, context) is { } how ? JsExpr.Array([label, how]) : label;
        }
        return JsExpr.Call(JsExpr.Identifier(helper), value, JsExpr.Literal(JsStringLiteral.Quote(name)),
            JsExpr.Array(members.Select(Member).ToList()));
    }

    /// <summary>
    /// What the runtime is told of a record's member or a tuple's element whose type says what its value
    /// cannot (the runtime's <c>Printed</c>): a function of the value where the compiler writes the text
    /// of the type, an enum's member name, a data twin's record text, a tuple's and an anonymous object's
    /// own text; a number's kind where it writes its own digits (a float) or no sign on a zero (an
    /// integer); and null where the value says all there is.
    /// </summary>
    private static JsExpr? Printed(ITypeSymbol? type, ConversionContext context)
    {
        var real = type.UnwrapNullable() ?? type;
        if (real is INamedTypeSymbol named
            && (named.TypeKind == TypeKind.Enum || named.TwinIsData() || named.IsTupleType || named.IsAnonymousType))
            return JsExpr.Arrow(context.TypeAnnotations ? "$v: any" : "$v", Of(type!, JsExpr.Identifier("$v"), context));
        return real is not null && WritesInTheCulture(real) && FormatKind.OfText(real) is { } kind
            ? JsExpr.Literal($"'{kind}'")
            : null;
    }

    /// <summary>
    /// Whether a value of the type reaches a formatter as its TEXT: a value tuple, an anonymous object and
    /// a value the browser holds as data (<c>[TwinIsData]</c>, <c>Color</c>, <c>Curve</c>), held as an array
    /// and as plain objects, whose text only the static type can write, and which .NET never formats, none
    /// being <c>IFormattable</c>: a format written for one is ignored, and an alignment pads its
    /// <c>ToString</c>. An interpolation hole with a format or an alignment and an argument of
    /// <c>string.Format</c> hand the formatter that text, which it pads; handed the value, it wrote
    /// JavaScript's <c>1,2</c> and <c>[object Object]</c> (#762).
    /// </summary>
    internal static bool ReachesTheFormatterAsText(ITypeSymbol? type) =>
        type.UnwrapNullable() is INamedTypeSymbol { IsTupleType: true } or INamedTypeSymbol { IsAnonymousType: true }
        || type.UnwrapNullable().TwinIsData();

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
