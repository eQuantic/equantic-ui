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

    /// <summary>
    /// True when the type is a NODE — anything that ends up in the built tree, which is everything
    /// deriving from <c>VisualNode</c>: the abstract vocabulary (<c>Box</c>, <c>Row</c>, <c>Text</c>)
    /// and every component, since <c>UiComponent : VisualNode</c>.
    /// <para>
    /// Broader than <see cref="IsUiComponent"/> deliberately. That one asks "is this a component
    /// class", which a <c>Column</c> is not; origin stamping has to cover the vocabulary too, or a
    /// click on a layout container finds nothing to select.
    /// </para>
    /// </summary>
    public static bool IsVisualNode(this ITypeSymbol? type)
    {
        for (var t = type; t != null; t = t.BaseType)
        {
            if (t.Name == "VisualNode") return true;
        }
        return false;
    }

    /// <summary>
    /// True when the type derives (transitively) from <c>ComponentState</c> — a <c>StatefulComponent</c>'s
    /// state class. A state class is owned by its page: the page's module emits it complete (via
    /// <c>ParseStateClass</c>) and <c>createState()</c> news it up from that same module, so it must never
    /// also be emitted as a standalone component module (which produced a broken duplicate carrying only
    /// <c>build()</c>). This is a strict subset of <see cref="IsUiComponent"/>.
    /// </summary>
    public static bool IsComponentState(this ITypeSymbol? type)
    {
        for (var t = type; t != null; t = t.BaseType)
        {
            if (t.Name == "ComponentState") return true;
        }
        return false;
    }

    /// <summary>
    /// True when the type is an enum annotated with <c>[Flags]</c>. Such enums are designed to be
    /// OR-combined (<c>Read | Write</c>) — a value the member-name string representation cannot express —
    /// so the transpiler represents <c>[Flags]</c> enums NUMERICALLY (members emit their underlying value),
    /// while non-flags enums keep the member-name string.
    /// </summary>
    public static bool IsFlagsEnum(this ITypeSymbol? type) =>
        type is { TypeKind: TypeKind.Enum }
        && type.GetAttributes().Any(a => a.AttributeClass?.Name == "FlagsAttribute");

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

    /// <summary>
    /// True when <paramref name="type"/> is a generic dictionary (<c>Dictionary</c>, <c>IDictionary</c>
    /// or <c>IReadOnlyDictionary</c>) whose KEY is a structural value type (record/struct/value tuple).
    /// A plain JS object can't key on those — it coerces the key to a string via <c>toString</c>,
    /// collapsing distinct values — so the transpiler routes these to the runtime
    /// <c>$eq.collections.valueMap</c> (structural-equality keys). String/number/enum-keyed dictionaries
    /// return <c>false</c> and keep the plain-object representation.
    /// </summary>
    public static bool IsValueKeyedDictionary(this ITypeSymbol? type)
    {
        if (type is not INamedTypeSymbol named) return false;
        var def = named.OriginalDefinition;
        // Match on name + namespace + arity (not a display-string prefix) so nested helper types like
        // `Dictionary<,>.KeyCollection` — whose display string also starts with "…Dictionary<" — don't
        // get mistaken for the dictionary itself.
        if (def?.ContainingNamespace?.ToDisplayString() != "System.Collections.Generic") return false;
        var isDictionary = def.Name is "Dictionary" or "IDictionary" or "IReadOnlyDictionary";
        return isDictionary
            && named.TypeArguments.Length == 2
            && named.TypeArguments[0].IsStructuralValueType();
    }

    /// <summary>
    /// True when <paramref name="type"/> is a key-sorted dictionary — <c>SortedDictionary&lt;K, V&gt;</c>
    /// or the generic <c>SortedList&lt;K, V&gt;</c>. These keep their keys ordered, so they route to the
    /// runtime <c>$eq.collections.sortedDictionary</c>/<c>sortedList</c> (sorted <c>Keys</c>/<c>Values</c>/
    /// iteration) rather than the plain-object dictionary form. <see cref="SortedDictionaryFactory"/>
    /// picks the matching runtime factory.
    /// </summary>
    public static bool IsSortedDictionary(this ITypeSymbol? type)
    {
        if (type is not INamedTypeSymbol named) return false;
        var def = named.OriginalDefinition;
        if (def?.ContainingNamespace?.ToDisplayString() != "System.Collections.Generic") return false;
        return def.Name is "SortedDictionary" or "SortedList" && named.TypeArguments.Length == 2;
    }

    /// <summary>The runtime factory (<c>$eq.collections.sortedDictionary</c>/<c>sortedList</c>) for a
    /// sorted dictionary type, or <c>null</c> when it is not one.</summary>
    public static string? SortedDictionaryFactory(this ITypeSymbol? type) =>
        type is INamedTypeSymbol { OriginalDefinition.Name: "SortedList" } ? Eq.SortedList
        : type.IsSortedDictionary() ? Eq.SortedDictionary
        : null;

    /// <summary>
    /// Whether a receiver's STATIC type leaves its runtime shape open. An array, a List or a string
    /// is an array or a string, and JS members apply directly; but `ICollection`, `IEnumerable` and
    /// the set interfaces are all satisfied by a <c>HashSet</c>, which lowers to a JS Set — and a Set
    /// has neither <c>includes</c> nor <c>length</c>. A member written for the wrong shape returns
    /// <c>undefined</c> rather than failing, which is how a checkbox stops responding in silence.
    /// </summary>
    public static bool HasOpenCollectionShape(this ITypeSymbol? type)
    {
        if (type is IArrayTypeSymbol or null) return false;
        if (type.SpecialType == SpecialType.System_String) return false;
        var def = type.OriginalDefinition?.ToString() ?? "";
        return def.StartsWith("System.Collections.Generic.ICollection")
            || def.StartsWith("System.Collections.Generic.IReadOnlyCollection")
            || def.StartsWith("System.Collections.Generic.IEnumerable")
            || def.StartsWith("System.Collections.Generic.ISet")
            || def.StartsWith("System.Collections.Generic.IReadOnlySet")
            || def.StartsWith("System.Collections.Generic.HashSet");
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

    /// <summary>
    /// Whether the type is a Dictionary shape (Dictionary/IDictionary/IReadOnlyDictionary), and
    /// whether its KEY is numeric. Transpiled dictionaries are plain JS objects — not iterable —
    /// so every construct that ENUMERATES one (foreach, a List copy) must go through
    /// <c>$eq.entries(obj, numericKeys)</c>, which yields destructurable [key, value] pairs that
    /// also answer .key/.value, with numeric keys restored (Object.entries strings them, and a
    /// stringified key silently turns later arithmetic into concatenation).
    /// </summary>
    public static bool IsDictionaryLike(this ITypeSymbol? type, out string keyForm)
    {
        // What `$eq.entries` restores each stringified object key as — the KEY TYPE the compiler
        // saw: `'long'` a BigInt, `'decimal'` a runtime Decimal, `true` a plain number, `false`
        // the string it already is. An object key is always a string at runtime; the C# key the
        // loop binds is the exact type, or `key + 1` concatenates (or throws, for a BigInt).
        keyForm = "false";
        if (type is not INamedTypeSymbol named) return false;
        var definition = named.OriginalDefinition.ToDisplayString();
        var isDictionary = definition is "System.Collections.Generic.Dictionary<TKey, TValue>"
            or "System.Collections.Generic.IDictionary<TKey, TValue>"
            or "System.Collections.Generic.IReadOnlyDictionary<TKey, TValue>";
        if (!isDictionary || named.TypeArguments.Length != 2) return false;
        var key = named.TypeArguments[0].SpecialType;
        keyForm = key switch
        {
            SpecialType.System_Int64 or SpecialType.System_UInt64 => "'long'",
            SpecialType.System_Decimal => "'decimal'",
            SpecialType.System_Int32 or SpecialType.System_Single or SpecialType.System_Double
                or SpecialType.System_Int16 or SpecialType.System_Byte => "true",
            _ => "false",
        };
        // ONLY primitive-keyed dictionaries lower to plain objects. A record/struct key lowers to
        // $eq.collections.valueMap, which is ITERABLE with .key/.value pairs already — wrapping it
        // in Object.entries would enumerate the map's internals, not its entries (the conformance
        // suite caught exactly that).
        return keyForm != "false" || key == SpecialType.System_String
            || named.TypeArguments[0] is INamedTypeSymbol { TypeKind: TypeKind.Enum };
    }

    /// <summary>
    /// The declaring static class of a C# 14 extension-BLOCK member (<c>extension(T receiver) { … }</c>):
    /// the member's containing type is Roslyn's unnamed extension grouping
    /// (<see cref="INamedTypeSymbol.IsExtension"/>), and ITS parent is the class the emitter lowers
    /// the member onto as a static. Null for every other kind of member — including classic
    /// <c>this</c>-parameter extensions, which keep their own reduced-form path.
    /// </summary>
    public static INamedTypeSymbol? ExtensionBlockHome(this ISymbol? symbol) =>
        symbol is { ContainingType: { IsExtension: true, ContainingType: { } home } } ? home : null;

    /// <summary>
    /// Registers a type name the conversion INTRODUCED into the output (the source never names the
    /// extension home — the call is written on the receiver), in the bucket its namespace decides,
    /// so the import scanner can see it. Same routing the static-call path uses.
    /// </summary>
    public static void RegisterIntroduced(this INamedTypeSymbol home, ConversionContext context)
    {
        var ns = home.ContainingNamespace?.ToDisplayString() ?? string.Empty;
        if (Services.RuntimeProvidedTypeScanner.IsRuntimeProvidedNamespace(ns))
            context.UsedRuntimeTypes.Add(home.Name);
        else
            context.UsedAppTypes.Add(home.Name);
    }
}
