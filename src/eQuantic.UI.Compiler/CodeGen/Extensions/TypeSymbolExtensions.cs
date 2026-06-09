using System;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace eQuantic.UI.Compiler.CodeGen;

/// <summary>
/// Semantic checks over Roslyn type symbols, expressed as extension methods so call sites read
/// fluently (<c>type.IsStructuralValueType()</c>, <c>type.IsNamed("System.DateTime")</c>) instead of
/// routing through a static helper class. Lives in the <c>CodeGen</c> namespace so strategies nested
/// under it see these without an extra <c>using</c>.
/// </summary>
public static class TypeSymbolExtensions
{
    private static readonly SpecialType[] IntegralTypes =
    {
        SpecialType.System_SByte, SpecialType.System_Byte,
        SpecialType.System_Int16, SpecialType.System_UInt16,
        SpecialType.System_Int32, SpecialType.System_UInt32,
        SpecialType.System_Int64, SpecialType.System_UInt64,
    };

    private static readonly SpecialType[] PrimitiveNumericTypes =
    {
        SpecialType.System_SByte, SpecialType.System_Byte,
        SpecialType.System_Int16, SpecialType.System_UInt16,
        SpecialType.System_Int32, SpecialType.System_UInt32,
        SpecialType.System_Single, SpecialType.System_Double,
        // Int64/UInt64 (long) and Decimal are intentionally excluded — handled by their own branches.
    };

    /// <summary>Returns the underlying <c>T</c> of a <c>Nullable&lt;T&gt;</c>, or the type itself.</summary>
    public static ITypeSymbol? UnwrapNullable(this ITypeSymbol? type)
    {
        if (type is INamedTypeSymbol named
            && named.OriginalDefinition?.SpecialType == SpecialType.System_Nullable_T
            && named.TypeArguments.Length == 1)
        {
            return named.TypeArguments[0];
        }
        return type;
    }

    /// <summary>Full-name match (e.g. <c>"System.DateTime"</c>), transparently unwrapping <c>Nullable&lt;T&gt;</c>.</summary>
    public static bool IsNamed(this ITypeSymbol? type, string fullName) =>
        type.UnwrapNullable()?.ToDisplayString() == fullName;

    /// <summary>An integral type (signed/unsigned 8–64 bit), unwrapping <c>Nullable&lt;T&gt;</c>.</summary>
    public static bool IsIntegral(this ITypeSymbol? type)
    {
        var t = type.UnwrapNullable();
        return t != null && Array.IndexOf(IntegralTypes, t.SpecialType) >= 0;
    }

    /// <summary><c>decimal</c> (or <c>decimal?</c>).</summary>
    public static bool IsDecimal(this ITypeSymbol? type) =>
        type.UnwrapNullable()?.SpecialType == SpecialType.System_Decimal;

    /// <summary><c>long</c>/<c>ulong</c> (or their nullable forms).</summary>
    public static bool IsLong(this ITypeSymbol? type) =>
        type.UnwrapNullable()?.SpecialType is SpecialType.System_Int64 or SpecialType.System_UInt64;

    /// <summary>
    /// <c>Nullable&lt;T&gt;</c> over a primitive numeric T (the kinds whose lifted operators route
    /// through <c>$eq.nullable.*</c>; excludes long/ulong and decimal, handled by their own branches).
    /// </summary>
    public static bool IsNullablePrimitiveNumeric(this ITypeSymbol? type)
    {
        if (type is INamedTypeSymbol named
            && named.OriginalDefinition?.SpecialType == SpecialType.System_Nullable_T
            && named.TypeArguments.Length == 1)
        {
            return Array.IndexOf(PrimitiveNumericTypes, named.TypeArguments[0].SpecialType) >= 0;
        }
        return false;
    }

    /// <summary>
    /// .NET value-shaped data the transpiler models as plain objects/arrays and that compares by
    /// VALUE: records (class or struct), user structs, and value tuples. Excludes <c>Nullable&lt;T&gt;</c>
    /// (handled separately), primitives/string/decimal (their <see cref="SpecialType"/> is set), and the
    /// compat structs that carry their own equality (DateTime, TimeSpan, DateOnly, TimeOnly,
    /// DateTimeOffset, Guid).
    /// </summary>
    public static bool IsStructuralValueType(this ITypeSymbol? type)
    {
        if (type == null) return false;
        if (type is INamedTypeSymbol n && n.OriginalDefinition?.SpecialType == SpecialType.System_Nullable_T)
            return false;
        if (type.SpecialType != SpecialType.None) return false; // int/string/decimal/bool/…
        if (type.IsTupleType) return true;
        if (type.IsRecord) return true;
        if (type.TypeKind == TypeKind.Struct)
        {
            return type.ToDisplayString() switch
            {
                "System.DateTime" or "System.TimeSpan" or "System.DateOnly" or "System.TimeOnly"
                    or "System.DateTimeOffset" or "System.Guid" => false,
                _ => true,
            };
        }
        return false;
    }

    /// <summary>The element type of an array or <c>IEnumerable&lt;T&gt;</c>, or <c>null</c>.</summary>
    public static ITypeSymbol? GetEnumerableElementType(this ITypeSymbol? collectionType)
    {
        if (collectionType is IArrayTypeSymbol array) return array.ElementType;
        if (collectionType is INamedTypeSymbol named)
        {
            if (named.TypeArguments.Length == 1) return named.TypeArguments[0];
            var enumerable = named.AllInterfaces
                .FirstOrDefault(i => i.Name == "IEnumerable" && i.TypeArguments.Length == 1);
            if (enumerable != null) return enumerable.TypeArguments[0];
        }
        return null;
    }
}
