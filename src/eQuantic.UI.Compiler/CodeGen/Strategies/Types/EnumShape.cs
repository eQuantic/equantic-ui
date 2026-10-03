using System.Globalization;
using Microsoft.CodeAnalysis;
using eQuantic.UI.Compiler.CodeGen.Ir;

namespace eQuantic.UI.Compiler.CodeGen.Strategies.Types;

/// <summary>
/// An enum's member table, and the ONE place eqc reads an enum's members from: each member's declared
/// name, the key the browser holds for it (its twin name, or its value for a [Flags] enum, which is a
/// number there), and its value. An enum has no object of its own in the browser, so every lowering
/// that needs its members writes them inline from here: the runtime's shape, which the enum functions
/// read (<c>utils/enums.ts</c>), a cast's or an arithmetic's key↔value maps, and the order the values
/// sort in. Four writers enumerated the members on their own, and a rule about how a member crosses
/// had four places to be remembered in.
/// <para>
/// A value no member names, which C# allows (<c>(Status)7</c>, a combination of a non-flags enum's
/// values), is held as its number, as the runtime's own functions hold it: the key↔value maps fall
/// back to the value itself, where they answered undefined.
/// </para>
/// </summary>
internal static class EnumShape
{
    /// <summary>One member: its declared name, the key the browser holds for it, and its value.</summary>
    internal readonly record struct Member(string Name, string Key, decimal Value)
    {
        /// <summary>The value as JavaScript writes it.</summary>
        internal string Number => Value.ToString(CultureInfo.InvariantCulture);
    }

    /// <summary>The members, in declaration order.</summary>
    internal static IReadOnlyList<Member> Members(INamedTypeSymbol enumType) =>
        enumType.GetMembers().OfType<IFieldSymbol>()
            .Where(field => field.HasConstantValue)
            .Select(field => new Member(field.Name, field.Name.ToCamelCase(),
                System.Convert.ToDecimal(field.ConstantValue, CultureInfo.InvariantCulture)))
            .ToList();

    /// <summary>
    /// The shape's JavaScript object literal: the names, what the browser holds for each member (a
    /// flags enum writes none, since it holds the values), the values, whether it is a flags enum, and
    /// the hex digits the <c>X</c> format writes, twice the underlying type's size in bytes.
    /// </summary>
    internal static string Of(INamedTypeSymbol enumType)
    {
        var members = Members(enumType);
        var flags = enumType.IsFlagsEnum();
        var names = string.Join(", ", members.Select(member => JsStringLiteral.Quote(member.Name)));
        var keys = flags ? "" : $"keys: [{string.Join(", ", members.Select(member => JsStringLiteral.Quote(member.Key)))}], ";
        var values = string.Join(", ", members.Select(member => member.Number));
        return $"{{ names: [{names}], {keys}values: [{values}], flags: {(flags ? "true" : "false")}, digits: {Digits(enumType)} }}";
    }

    /// <summary>Each member's key to its value: <c>{ 'low': 0, 'medium': 5 }</c>, what a cast to a
    /// number and the order of the values read.</summary>
    internal static string KeyToValue(INamedTypeSymbol enumType) =>
        "{ " + string.Join(", ", Members(enumType).Select(member => $"'{member.Key}': {member.Number}")) + " }";

    /// <summary>Each value to its member's key: <c>{ 0: 'low', 5: 'medium' }</c>, what a cast from a
    /// number reads. A negative value is quoted, since no literal key can carry its sign.</summary>
    internal static string ValueToKey(INamedTypeSymbol enumType) =>
        "{ " + string.Join(", ", Members(enumType).Select(member =>
            $"{(member.Value < 0 ? $"'{member.Number}'" : member.Number)}: '{member.Key}'")) + " }";

    /// <summary>
    /// The number an enum value is: a key's member's value, or the number a value no member names is
    /// held as. A flags enum holds its number already.
    /// </summary>
    internal static JsExpr ValueOf(INamedTypeSymbol enumType, JsExpr held, ConversionContext context) =>
        enumType.IsFlagsEnum()
            ? held
            : JsExpr.Template($"(({KeyToValue(enumType)})[{{0}}] ?? {{0}})", [held], context.TypeAnnotations);

    /// <summary>
    /// What the browser holds for a number: its member's key, or the number itself where no member has
    /// it. A flags enum holds the number.
    /// </summary>
    internal static JsExpr Held(INamedTypeSymbol enumType, JsExpr number, ConversionContext context) =>
        enumType.IsFlagsEnum()
            ? number
            : JsExpr.Template($"(({ValueToKey(enumType)})[{{0}}] ?? {{0}})", [number], context.TypeAnnotations);

    /// <summary>The key of the first member with this value, or null where no member has it.</summary>
    internal static string? KeyOf(INamedTypeSymbol enumType, decimal value) =>
        Members(enumType).Where(member => member.Value == value).Select(member => member.Key).FirstOrDefault();

    /// <summary>
    /// The text .NET writes for an enum value, where the inline name table cannot answer it: a
    /// [Flags] enum's, whose value is its number and whose text names its set flags (it indexed the
    /// table with the number and answered null), a nullable enum's, which is the empty string for null
    /// (it was written by <c>String</c>, the camelCase name) (#452), and one a format asks for
    /// (<c>D</c>, <c>X</c>, <c>F</c>), given as the JavaScript that holds it.
    /// </summary>
    internal static string Text(INamedTypeSymbol enumType, string held, ConversionContext context, string? format = null)
    {
        context.UsedHelpers.Add(Eq.Import);
        return format is null
            ? $"{Eq.EnumText}({held}, {Of(enumType)})"
            : $"{Eq.EnumText}({held}, {Of(enumType)}, {format})";
    }

    /// <summary>The hex digits of the underlying type: two per byte.</summary>
    private static int Digits(INamedTypeSymbol enumType) => enumType.EnumUnderlyingType?.SpecialType switch
    {
        SpecialType.System_Byte or SpecialType.System_SByte => 2,
        SpecialType.System_Int16 or SpecialType.System_UInt16 => 4,
        SpecialType.System_Int64 or SpecialType.System_UInt64 => 16,
        _ => 8,
    };
}
