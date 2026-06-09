using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
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

    /// <summary>The framework base types a UI component / state class derives from. Kept tiny and
    /// principled (the actual abstract bases the framework defines) — intermediate user/library bases
    /// are reached by walking the chain, not by naming them.</summary>
    private static readonly string[] ComponentBaseNames =
        { "StatefulComponent", "StatelessComponent", "HtmlElement", "ComponentState" };

    /// <summary>
    /// True when the type derives (transitively) from a framework component/state base. Walking the base
    /// chain recognises a component that extends another user or library component without enumerating
    /// every intermediate base — replacing brittle direct-base-name matching.
    /// </summary>
    public static bool IsUiComponent(this ITypeSymbol? type)
    {
        for (var t = type; t != null; t = t.BaseType)
        {
            if (System.Array.IndexOf(ComponentBaseNames, t.Name) >= 0) return true;
        }
        return false;
    }

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

    /// <summary>
    /// Zero-based index of a value-tuple element accessed by name — either a positional <c>ItemN</c>
    /// or a declared element name (<c>X</c> in <c>(int X, int Y)</c>). Returns -1 when the type is not a
    /// tuple or the name doesn't match an element (so the caller can fall back to default member access).
    /// </summary>
    public static int TupleElementIndex(this ITypeSymbol? type, string name)
    {
        if (type is not INamedTypeSymbol { IsTupleType: true } tuple) return -1;

        // Positional accessor: Item1, Item2, … (always available, even on named tuples).
        var m = Regex.Match(name, @"^Item(\d+)$");
        if (m.Success && int.TryParse(m.Groups[1].Value, out var n)
            && n >= 1 && n <= tuple.TupleElements.Length)
        {
            return n - 1;
        }

        // Declared element name.
        for (var i = 0; i < tuple.TupleElements.Length; i++)
        {
            if (tuple.TupleElements[i].Name == name) return i;
        }
        return -1;
    }

    /// <summary>
    /// Ordered camelCase element names of a type's <c>Deconstruct(out …)</c> method — the order
    /// <c>var (a, b) = value</c> binds to. Used to deconstruct a record/struct (a plain object) by
    /// position. Returns <c>null</c> when the type has no usable <c>Deconstruct</c>.
    /// </summary>
    public static IReadOnlyList<string>? DeconstructElementNames(this ITypeSymbol? type)
    {
        var deconstruct = type?.GetMembers("Deconstruct")
            .OfType<IMethodSymbol>()
            .FirstOrDefault(m => m.Parameters.Length > 0 && m.Parameters.All(p => p.RefKind == RefKind.Out));
        return deconstruct?.Parameters.Select(p => p.Name.ToCamelCase()).ToList();
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
