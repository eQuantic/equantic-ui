using System.Globalization;
using Microsoft.CodeAnalysis;
using eQuantic.UI.Compiler.CodeGen.Ir;

namespace eQuantic.UI.Compiler.CodeGen.Strategies.Types;

/// <summary>
/// An enum's shape, as the runtime's enum functions read it (<c>utils/enums.ts</c>): its declared
/// names, what the browser holds for each member (its camelCase name, or its number for a [Flags]
/// enum), the members' values, and whether it is a flags enum. Written inline where a call needs it,
/// since an enum has no object of its own in the browser: <c>Enum.Parse&lt;Status&gt;(text)</c> named
/// an object <c>Status</c> no module declares (#480).
/// </summary>
internal static class EnumShape
{
    /// <summary>The shape's JavaScript object literal.</summary>
    internal static string Of(INamedTypeSymbol enumType)
    {
        var members = enumType.GetMembers().OfType<IFieldSymbol>().Where(field => field.HasConstantValue).ToList();
        var flags = enumType.IsFlagsEnum();
        var values = members.Select(field => System.Convert.ToDecimal(field.ConstantValue, CultureInfo.InvariantCulture)
            .ToString(CultureInfo.InvariantCulture)).ToList();
        var names = string.Join(", ", members.Select(field => JsStringLiteral.Quote(field.Name)));
        var keys = flags
            ? string.Join(", ", values)
            : string.Join(", ", members.Select(field => JsStringLiteral.Quote(field.Name.ToCamelCase())));
        return $"{{ names: [{names}], keys: [{keys}], values: [{string.Join(", ", values)}], flags: {(flags ? "true" : "false")} }}";
    }

    /// <summary>
    /// The text .NET writes for an enum value, where the inline name table cannot answer it: a
    /// [Flags] enum's, whose value is its number and whose text names its set flags (it indexed the
    /// table with the number and answered null), and a nullable enum's, which is the empty string
    /// for null (it was written by <c>String</c>, the camelCase name) (#452).
    /// </summary>
    internal static string Text(INamedTypeSymbol enumType, string held, ConversionContext context)
    {
        context.UsedHelpers.Add(Eq.Import);
        return $"{Eq.EnumText}({held}, {Of(enumType)})";
    }
}
