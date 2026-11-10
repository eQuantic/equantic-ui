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
/// <para>
/// A value is held at its underlying type's WIDTH (#551): a number for a type of 32 bits or fewer, and
/// a BigInt for a <c>long</c> or a <c>ulong</c>, as the SDK holds every 64-bit integer
/// (<c>utils/long.ts</c>). A member past 2^53 was written as a number that is not its value.
/// </para>
/// </summary>
internal static class EnumShape
{
    /// <summary>One member: its declared name, the key the browser holds for it, and its value.</summary>
    internal readonly record struct Member(string Name, string Key, decimal Value);

    /// <summary>The underlying type's width and sign, which every value of the enum is held at.</summary>
    internal static (int Bits, bool Unsigned) Width(INamedTypeSymbol enumType) =>
        IntegerWidth.Of(enumType.EnumUnderlyingType) ?? (32, false);

    /// <summary>Whether the underlying type is a <c>long</c> or a <c>ulong</c>, whose values the browser
    /// holds as BigInts.</summary>
    internal static bool IsWide(INamedTypeSymbol enumType) => Width(enumType).Bits == 64;

    /// <summary>A value of the enum as JavaScript writes it: <c>5</c>, or <c>5n</c> for a 64-bit enum.</summary>
    internal static string ValueLiteral(INamedTypeSymbol enumType, decimal value)
    {
        var digits = value.ToString(CultureInfo.InvariantCulture);
        return IsWide(enumType) ? digits + "n" : digits;
    }

    /// <summary>
    /// What the browser holds for a CONSTANT value of the enum: the key of the first member declared
    /// with it, for an enum that is not a flags one, or the value itself, at the enum's width.
    /// </summary>
    internal static string HeldLiteral(INamedTypeSymbol enumType, decimal value) =>
        !enumType.IsFlagsEnum() && KeyOf(enumType, value) is { } key
            ? JsStringLiteral.Quote(key)
            : ValueLiteral(enumType, value);

    /// <summary>The TypeScript a value of a [Flags] enum is annotated with: the number its bits are, or a
    /// <c>bigint</c> for a 64-bit one.</summary>
    internal static string FlagsTsType(INamedTypeSymbol enumType) => IsWide(enumType) ? "bigint" : "number";

    /// <summary>The members, in declaration order, each carrying the key of the first member with its value (#927).</summary>
    internal static IReadOnlyList<Member> Members(INamedTypeSymbol enumType)
    {
        var fields = enumType.GetMembers().OfType<IFieldSymbol>()
            .Where(field => field.HasConstantValue)
            .Select(field => (field.Name, Value: System.Convert.ToDecimal(field.ConstantValue, CultureInfo.InvariantCulture)))
            .ToList();
        var canonicalKey = new Dictionary<decimal, string>();
        foreach (var (name, value) in fields)
            canonicalKey.TryAdd(value, name.ToCamelCase());
        return fields.Select(field => new Member(field.Name, canonicalKey[field.Value], field.Value)).ToList();
    }

    /// <summary>
    /// The shape's JavaScript object literal: the names, what the browser holds for each member (a
    /// flags enum writes none, since it holds the values), the values, whether it is a flags enum, the
    /// hex digits the <c>X</c> format writes, twice the underlying type's size in bytes, and, for an
    /// unsigned underlying type alone, that it is one: the width and the sign bound a number
    /// <c>Parse</c> reads and place a uint's high bit.
    /// </summary>
    internal static string Of(INamedTypeSymbol enumType)
    {
        var members = Members(enumType);
        var flags = enumType.IsFlagsEnum();
        var names = string.Join(", ", members.Select(member => JsStringLiteral.Quote(member.Name)));
        var keys = flags ? "" : $"keys: [{string.Join(", ", members.Select(member => JsStringLiteral.Quote(member.Key)))}], ";
        var values = string.Join(", ", members.Select(member => ValueLiteral(enumType, member.Value)));
        var unsigned = Width(enumType).Unsigned ? ", unsigned: true" : "";
        return $"{{ names: [{names}], {keys}values: [{values}], flags: {(flags ? "true" : "false")}, digits: {Digits(enumType)}{unsigned} }}";
    }

    /// <summary>Each member's key to its value: <c>{ 'low': 0, 'medium': 5 }</c>, what a cast to a
    /// number and the order of the values read, a 64-bit enum's values as BigInts.</summary>
    internal static string KeyToValue(INamedTypeSymbol enumType) =>
        "{ " + string.Join(", ", Members(enumType).DistinctBy(member => member.Value)
            .Select(member => $"'{member.Key}': {ValueLiteral(enumType, member.Value)}")) + " }";

    /// <summary>
    /// Each value to its member's key: <c>{ 0: 'low', 5: 'medium' }</c>, what a cast from a number reads.
    /// A negative value is quoted, since no literal key can carry its sign, and so is every value of a
    /// 64-bit enum, which a number key would round: a BigInt indexes the object by its digits.
    /// </summary>
    internal static string ValueToKey(INamedTypeSymbol enumType) =>
        "{ " + string.Join(", ", Members(enumType).DistinctBy(member => member.Value).Select(member =>
        {
            var digits = member.Value.ToString(CultureInfo.InvariantCulture);
            return $"{(member.Value < 0 || IsWide(enumType) ? $"'{digits}'" : digits)}: '{member.Key}'";
        })) + " }";

    /// <summary>
    /// The number an enum value is: a key's member's value, or the number a value no member names is
    /// held as. A flags enum holds its number already.
    /// </summary>
    internal static JsExpr ValueOf(INamedTypeSymbol enumType, JsExpr held, ConversionContext context) =>
        enumType.IsFlagsEnum()
            ? held
            : JsExpr.Template($"(({KeyToValue(enumType)})[{{0}}] ?? {{0}})", [held]);

    /// <summary>
    /// What the browser holds for a number: its member's key, or the number itself where no member has
    /// it. A flags enum holds the number.
    /// </summary>
    internal static JsExpr Held(INamedTypeSymbol enumType, JsExpr number, ConversionContext context) =>
        enumType.IsFlagsEnum()
            ? number
            : JsExpr.Template($"(({ValueToKey(enumType)})[{{0}}] ?? {{0}})", [number]);

    /// <summary>The key of the first member with this value, or null where no member has it.</summary>
    internal static string? KeyOf(INamedTypeSymbol enumType, decimal value) =>
        Members(enumType).Where(member => member.Value == value).Select(member => member.Key).FirstOrDefault();

    /// <summary>The declared name of the first member with this value, or null where no member has it.</summary>
    internal static string? NameOf(INamedTypeSymbol enumType, decimal value) =>
        Members(enumType).Where(member => member.Value == value).Select(member => member.Name).FirstOrDefault();

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

    /// <summary>The hex digits of the underlying type: two per byte, which a 64-bit enum's shape says
    /// with its 16 that its values are BigInts.</summary>
    private static int Digits(INamedTypeSymbol enumType) => Width(enumType).Bits / 4;
}
