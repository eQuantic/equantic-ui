using Microsoft.CodeAnalysis;

namespace eQuantic.UI.Compiler.CodeGen.Strategies;

/// <summary>
/// <c>default(T)</c> as JavaScript. C#'s default is decided by the TYPE, and for a value type it
/// is never null: an <c>int</c> defaults to 0, a <c>bool</c> to false, a <c>long</c> to 0n, an
/// enum to its zero-valued member. The LINQ <c>…OrDefault</c> family returns exactly this when the
/// sequence has nothing to give, so <c>new int[0].SingleOrDefault()</c> is 0 in .NET — emitting
/// <c>null</c> there is a wrong answer that no exception announces.
/// </summary>
public static class DefaultValue
{
    /// <summary>The default of <paramref name="type"/>, or <c>null</c> where the type is a
    /// reference type, unknown, or a struct with no faithful zero on this side.</summary>
    public static string Of(ITypeSymbol? type, ConversionContext context)
    {
        switch (type?.SpecialType)
        {
            case SpecialType.System_Boolean:
                return "false";
            case SpecialType.System_SByte or SpecialType.System_Byte
                or SpecialType.System_Int16 or SpecialType.System_UInt16
                or SpecialType.System_Int32 or SpecialType.System_UInt32
                or SpecialType.System_Single or SpecialType.System_Double:
                return "0";
            case SpecialType.System_Int64 or SpecialType.System_UInt64:
                context.UsedHelpers.Add(Eq.Import);
                return $"{Eq.Long}(0)";
            case SpecialType.System_Decimal:
                context.UsedHelpers.Add(Eq.Import);
                return $"{Eq.Dec}(0)";
            case SpecialType.System_Char:
                return "'\\0'";
            case SpecialType.System_String or SpecialType.System_Object:
                return "null";
        }

        // An enum is its member NAME at runtime, so the default is the member whose value is 0.
        // .NET still yields the numeric 0 when the enum declares no such member.
        if (type is { TypeKind: TypeKind.Enum })
        {
            var zero = type.GetMembers().OfType<IFieldSymbol>()
                .FirstOrDefault(field => field.HasConstantValue && IsZero(field.ConstantValue));
            return zero is null ? "0" : $"'{zero.Name.ToCamelCase()}'";
        }

        // A nullable value type defaults to the null one; every reference type does too. Another
        // struct has no zeroed instance on this side — null is the honest answer, and the sites
        // that need better say so explicitly.
        return "null";
    }

    /// <summary>The default of the ELEMENT of a sequence-typed expression.</summary>
    public static string OfElement(ITypeSymbol? sequence, ConversionContext context) =>
        Of(ElementType(sequence), context);

    private static ITypeSymbol? ElementType(ITypeSymbol? sequence) => sequence switch
    {
        IArrayTypeSymbol array => array.ElementType,
        INamedTypeSymbol named => named.AllInterfaces
            .Concat(named.OriginalDefinition.MetadataName == "IEnumerable`1" ? new[] { named } : [])
            .FirstOrDefault(i => i.OriginalDefinition.MetadataName == "IEnumerable`1")
            ?.TypeArguments.FirstOrDefault(),
        _ => null,
    };

    private static bool IsZero(object? constant)
    {
        try { return constant is not null && System.Convert.ToInt64(constant) == 0; }
        catch { return false; }
    }
}
