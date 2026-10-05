using System.Globalization;
using Microsoft.CodeAnalysis;
using eQuantic.UI.Compiler.CodeGen.Ir;

namespace eQuantic.UI.Compiler.CodeGen.Strategies.Types;

/// <summary>
/// An enum's member table, and the ONE place eqc reads an enum's members from: each member's declared
/// name, the key the browser holds for it (its twin name, or its value for a [Flags] enum, which is a
/// number there), and its value. An enum has no object of its own in the browser, so every lowering
/// that needs its members reads them from here: the runtime's shape, which the enum functions read
/// (<c>utils/enums.ts</c>), the key a value is held as and the value a key stands for, which a cast,
/// an operator and the order of the values read. Four writers enumerated the members on their own, and
/// a rule about how a member crosses had four places to be remembered in.
/// <para>
/// The shape is written ONCE per module, as a constant named after the enum (<c>$Status</c>), which
/// every call reads (#547): it was an object literal at every call, built every time the expression ran.
/// An expression converted outside any module writes it where it reads it.
/// </para>
/// <para>
/// A value is held at its underlying type's WIDTH (#551, #555): a number for a type of 32 bits or
/// fewer, an unsigned one never negative, and a BigInt for a <c>long</c> or a <c>ulong</c>, as the SDK
/// holds every 64-bit integer (<c>utils/long.ts</c>). A value no member names, which C# allows
/// (<c>(Status)7</c>, a combination of a non-flags enum's values), is held as that value.
/// </para>
/// </summary>
internal static class EnumShape
{
    /// <summary>One member: its declared name, the key the browser holds for it, and its value.</summary>
    internal readonly record struct Member(string Name, string Key, decimal Value);

    /// <summary>The members, in declaration order.</summary>
    internal static IReadOnlyList<Member> Members(INamedTypeSymbol enumType) =>
        enumType.GetMembers().OfType<IFieldSymbol>()
            .Where(field => field.HasConstantValue)
            .Select(field => new Member(field.Name, field.Name.ToCamelCase(),
                System.Convert.ToDecimal(field.ConstantValue, CultureInfo.InvariantCulture)))
            .ToList();

    /// <summary>The underlying type's width and sign, which every value of the enum is held at.</summary>
    internal static (int Bits, bool Unsigned) Width(INamedTypeSymbol enumType) =>
        IntegerWidth.Of(enumType.EnumUnderlyingType) ?? (32, false);

    /// <summary>Whether the underlying type is a <c>long</c> or a <c>ulong</c>, whose values the browser
    /// holds as BigInts.</summary>
    internal static bool IsWide(INamedTypeSymbol enumType) => Width(enumType).Bits == 64;

    /// <summary>The TypeScript a [Flags] enum's value is annotated with: the number its bits are, or a
    /// <c>bigint</c> for a 64-bit one (#551).</summary>
    internal static string FlagsTsType(ITypeSymbol enumType) =>
        enumType is INamedTypeSymbol named && IsWide(named) ? "bigint" : "number";

    /// <summary>A value of the underlying type as JavaScript writes it: <c>5</c>, or <c>5n</c> for a
    /// 64-bit enum, whose member past 2^53 was written as a number that is not its value.</summary>
    internal static string ValueLiteral(INamedTypeSymbol enumType, decimal value)
    {
        var digits = value.ToString(CultureInfo.InvariantCulture);
        return IsWide(enumType) ? digits + "n" : digits;
    }

    /// <summary>
    /// What the browser holds for a CONSTANT value of the enum: the key of the first member declared
    /// with it, for an enum that is not a flags one, or the value itself.
    /// </summary>
    internal static string HeldLiteral(INamedTypeSymbol enumType, object? constant)
    {
        var value = System.Convert.ToDecimal(constant, CultureInfo.InvariantCulture);
        if (!enumType.IsFlagsEnum()
            && Members(enumType).Where(member => member.Value == value).Select(member => member.Key).FirstOrDefault() is { } key)
            return JsStringLiteral.Quote(key);
        return ValueLiteral(enumType, value);
    }

    /// <summary>
    /// The shape's JavaScript object literal: the names, what the browser holds for each member (a
    /// flags enum writes none, since it holds the values), the values, whether it is a flags enum, the
    /// hex digits the <c>X</c> format writes, twice the underlying type's size in bytes, and, for an
    /// unsigned underlying type alone, that it is one: the width and the sign bound a number
    /// <c>Parse</c> reads and place a uint's high bit, and sixteen digits say the values are BigInts.
    /// </summary>
    internal static string Of(INamedTypeSymbol enumType)
    {
        var members = Members(enumType);
        var flags = enumType.IsFlagsEnum();
        var names = string.Join(", ", members.Select(member => JsStringLiteral.Quote(member.Name)));
        var keys = flags ? "" : $"keys: [{string.Join(", ", members.Select(member => JsStringLiteral.Quote(member.Key)))}], ";
        var values = string.Join(", ", members.Select(member => ValueLiteral(enumType, member.Value)));
        var unsigned = Width(enumType).Unsigned ? ", unsigned: true" : "";
        return $"{{ names: [{names}], {keys}values: [{values}], flags: {(flags ? "true" : "false")}, digits: {Width(enumType).Bits / 4}{unsigned} }}";
    }

    /// <summary>
    /// The shape as a call reads it: the constant its module declares once (<c>$Status</c>), or, for
    /// an expression converted outside any module, the literal itself.
    /// </summary>
    internal static string Table(INamedTypeSymbol enumType, ConversionContext context) => Table(enumType, context.Module);

    /// <summary>The shape as a call reads it, for a writer that holds the module rather than a
    /// conversion (a hydration spec).</summary>
    internal static string Table(INamedTypeSymbol enumType, ModuleConstants? module) =>
        module?.NameOf(enumType, () => Of(enumType)) ?? Of(enumType);

    /// <summary>
    /// The value an enum value is, at its underlying type's width: a key's member's value, or the
    /// value no member names. A flags enum holds its value already.
    /// </summary>
    internal static JsExpr ValueOf(INamedTypeSymbol enumType, JsExpr held, ConversionContext context)
    {
        if (enumType.IsFlagsEnum()) return held;
        context.UsedHelpers.Add(Eq.Import);
        return JsExpr.Call(JsExpr.Identifier(Eq.EnumValue), held, JsExpr.Literal(Table(enumType, context)));
    }

    /// <summary>
    /// What the browser holds for a value at the underlying type's width: the key of the first member
    /// with it, or the value itself where no member has it. A flags enum holds the value.
    /// </summary>
    internal static JsExpr Held(INamedTypeSymbol enumType, JsExpr value, ConversionContext context)
    {
        if (enumType.IsFlagsEnum()) return value;
        context.UsedHelpers.Add(Eq.Import);
        return JsExpr.Call(JsExpr.Identifier(Eq.EnumHold), value, JsExpr.Literal(Table(enumType, context)));
    }

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
        var table = Table(enumType, context);
        return format is null
            ? $"{Eq.EnumText}({held}, {table})"
            : $"{Eq.EnumText}({held}, {table}, {format})";
    }
}
