using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace eQuantic.UI.Compiler.CodeGen;

/// <summary>
/// The hydration spec for a type — the compile-time half of the TYPED BOUNDARY. The wire protocol
/// (EqJson) sends what JavaScript cannot represent natively as strings: a <c>long</c> as
/// "9007199254740993", a <c>decimal</c> as "0.1", the date/time family as ISO text. The compiler
/// KNOWS the C# type of every state field and every Server Action's return, so it writes that
/// knowledge down as a small JS literal — this class computes it — and the runtime's
/// <c>$eq.hydrate</c> coerces the value ONCE at the boundary, instead of every use site coercing
/// defensively.
/// <para>
/// The spec language mirrors <c>utils/hydrate.ts</c>: a tag (<c>'long'</c>, <c>'decimal'</c>,
/// <c>'dateTime'</c>…) for a compat scalar, <c>[spec]</c> for a list, <c>{ dict: spec }</c> for a
/// dictionary's values (the twin is a plain object — its keys are strings), and a bare class NAME
/// for an in-source record/struct, whose emitted twin carries its own <c>static $hydration</c>.
/// Null means IDENTITY: the JSON value is already what the runtime computes with, and no spec is
/// emitted at all — the common case stays clean.
/// </para>
/// </summary>
public static class HydrationSpec
{
    /// <summary>The JS spec literal for <paramref name="type"/>, or null when hydration is the
    /// identity. Record/struct names the spec references are added to <paramref name="referenced"/>
    /// so the caller can import their modules.</summary>
    public static string? Of(ITypeSymbol? type, ISet<string> referenced) =>
        Of(type, referenced, new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default));

    private static string? Of(ITypeSymbol? type, ISet<string> referenced, HashSet<INamedTypeSymbol> visiting)
    {
        type = type.UnwrapNullable();
        switch (type?.SpecialType)
        {
            case null:
                return null;
            case SpecialType.System_Decimal:
                return "'decimal'";
            case SpecialType.System_Int64 or SpecialType.System_UInt64:
                return "'long'";
            // A string is IEnumerable<char> to the walk below, and already itself on the wire.
            case SpecialType.System_String:
                return null;
        }

        if (type is IArrayTypeSymbol array)
            return List(array.ElementType, referenced, visiting);

        if (type is not INamedTypeSymbol named) return null;

        if (Scalar(named) is { } scalar) return scalar;

        // A dictionary before the enumerable walk — it IS IEnumerable<KeyValuePair<,>>, but its
        // twin is a plain object: keys stay strings, values hydrate.
        if (DictionaryValueType(named) is { } valueType)
            return Of(valueType, referenced, visiting) is { } value ? $"{{ dict: {value} }}" : null;

        if (ElementType(named) is { } element)
            return List(element, referenced, visiting);

        // A TUPLE crosses as an ARRAY, positionally — it has no twin to name, and naming it
        // `ValueTuple` (its symbol name) emitted a reference to a class that exists nowhere.
        if (named.IsTupleType)
        {
            var parts = named.TupleElements.Select(e => Of(e.Type, referenced, visiting)).ToList();
            return parts.Any(part => part is not null)
                ? $"{{ tuple: [{string.Join(", ", parts.Select(part => part ?? "null"))}] }}"
                : null;
        }

        // An IN-SOURCE record or struct has an emitted twin (a class, a prototype, methods); it
        // appears in the spec by NAME when any member transitively needs hydration — the twin's
        // own `static $hydration` says which.
        if (IsEmittedValueType(named) && HasHydratableMember(named, visiting))
        {
            referenced.Add(named.Name);
            return named.Name;
        }

        return null;
    }

    /// <summary>The date/time compat scalars, by their one full name each.</summary>
    private static string? Scalar(INamedTypeSymbol named) => named.ToDisplayString() switch
    {
        "System.DateTime" => "'dateTime'",
        "System.TimeSpan" => "'timeSpan'",
        "System.DateOnly" => "'dateOnly'",
        "System.TimeOnly" => "'timeOnly'",
        "System.DateTimeOffset" => "'dateTimeOffset'",
        _ => null,
    };

    private static string? List(ITypeSymbol element, ISet<string> referenced, HashSet<INamedTypeSymbol> visiting) =>
        Of(element, referenced, visiting) is { } inner ? $"[{inner}]" : null;

    /// <summary>The value type of a dictionary-shaped type — itself or any interface it implements
    /// constructed from <c>IDictionary&lt;,&gt;</c> / <c>IReadOnlyDictionary&lt;,&gt;</c>.</summary>
    private static ITypeSymbol? DictionaryValueType(INamedTypeSymbol named) =>
        SelfAndInterfaces(named)
            .FirstOrDefault(i => i.Arity == 2 && IsSystemCollection(i)
                && i.OriginalDefinition.MetadataName is "IDictionary`2" or "IReadOnlyDictionary`2")
            ?.TypeArguments[1];

    /// <summary>The element type of an enumerable — itself or any interface it implements
    /// constructed from <c>IEnumerable&lt;T&gt;</c>.</summary>
    private static ITypeSymbol? ElementType(INamedTypeSymbol named) =>
        SelfAndInterfaces(named)
            .FirstOrDefault(i => i.Arity == 1 && IsSystemCollection(i)
                && i.OriginalDefinition.MetadataName == "IEnumerable`1")
            ?.TypeArguments[0];

    private static IEnumerable<INamedTypeSymbol> SelfAndInterfaces(INamedTypeSymbol named) =>
        new[] { named }.Concat(named.AllInterfaces);

    private static bool IsSystemCollection(INamedTypeSymbol named) =>
        named.OriginalDefinition.ContainingNamespace?.ToDisplayString() == "System.Collections.Generic";

    /// <summary>Whether this type's twin is an emitted class with value semantics — the set
    /// <c>RecordTypeEmitter</c> handles: an in-source record, or an in-source struct.</summary>
    private static bool IsEmittedValueType(INamedTypeSymbol named) =>
        named.Locations.Any(location => location.IsInSource)
        && (named.IsRecord || named.TypeKind == TypeKind.Struct);

    /// <summary>Whether any public data member (transitively) has a spec — a cycle answers no for
    /// its own path, so a self-referential record still specs on its OTHER members.</summary>
    private static bool HasHydratableMember(INamedTypeSymbol named, HashSet<INamedTypeSymbol> visiting) =>
        visiting.Add(named) && HasHydratableMemberOf(named, visiting);

    /// <summary>The members a twin carries as DATA — public instance settable properties
    /// (positional record parameters included) and fields. A get-only computed property is a
    /// method on the twin, never a payload slot.</summary>
    private static IEnumerable<(string Name, ITypeSymbol Type)> DataMembers(INamedTypeSymbol named) =>
        named.GetMembers().Where(m => m is { DeclaredAccessibility: Accessibility.Public, IsStatic: false })
            .Select(m => m switch
            {
                IPropertySymbol { IsIndexer: false, SetMethod: not null } property => (property.Name, property.Type),
                IFieldSymbol { IsImplicitlyDeclared: false } field => (field.Name, field.Type),
                _ => default((string, ITypeSymbol)?),
            })
            .OfType<(string, ITypeSymbol)>();

    /// <summary>Whether any data member (transitively) has a spec — see the visiting guard above.</summary>
    private static bool HasHydratableMemberOf(INamedTypeSymbol named, HashSet<INamedTypeSymbol> visiting)
    {
        var throwaway = new HashSet<string>();
        return DataMembers(named).Any(member => Of(member.Type, throwaway, visiting) is not null);
    }

    /// <summary>The member map for a record/struct twin — <c>{ id: 'long', price: Money }</c> with
    /// the twin's camelCased member names — or null when no member needs hydration.</summary>
    public static string? Members(INamedTypeSymbol type, ISet<string> referenced)
    {
        var entries = DataMembers(type)
            .Select(m => (m.Name, Spec: Of(m.Type, referenced)))
            .Where(m => m.Spec is not null)
            .Select(m => $"{m.Name.ToCamelCase()}: {m.Spec}")
            .ToList();
        return entries.Count > 0 ? $"{{ {string.Join(", ", entries)} }}" : null;
    }
}
