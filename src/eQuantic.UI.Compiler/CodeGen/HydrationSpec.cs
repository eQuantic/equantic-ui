using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using eQuantic.UI.Compiler.CodeGen.Strategies.Types;

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
/// <c>'single'</c>, <c>'dateTime'</c>…) for a compat scalar, <c>[spec]</c> for a list,
/// <c>{ dict: spec, key, byValue, sorted }</c> for a dictionary, and a bare class NAME for an
/// in-source record/struct, whose emitted twin carries its own <c>static $hydration</c>.
/// Null means IDENTITY: the JSON value is already what the runtime computes with, and no spec is
/// emitted at all — the common case stays clean. A dictionary is never that case: it crosses as a
/// JSON object and has to become the runtime's dictionary class.
/// </para>
/// </summary>
public static class HydrationSpec
{
    /// <summary>The JS spec literal for <paramref name="type"/>, or null when hydration is the
    /// identity. Record/struct names the spec references are added to <paramref name="referenced"/>
    /// so the caller can import their modules.</summary>
    public static string? Of(ITypeSymbol? type, ISet<string> referenced, ISet<string> runtime) =>
        Of(type, new References(referenced, runtime), new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default));

    /// <summary>Where the names a spec mentions come from: <c>InSource</c> are this compilation's
    /// own twins, sibling modules; <c>Runtime</c> are the vocabulary's, which only
    /// <c>@equantic/runtime</c> exports. A caller that imported a runtime twin from a sibling module
    /// emitted `import { Rect } from "./Rect"`, a module that exists nowhere.</summary>
    private readonly record struct References(ISet<string> InSource, ISet<string> Runtime);

    private static string? Of(ITypeSymbol? type, References referenced, HashSet<INamedTypeSymbol> visiting)
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
            // A float arrives as the shortest text that names the SINGLE, which JavaScript parses
            // as the nearest double — a different number until it is rounded back (SinglePrecision).
            case SpecialType.System_Single:
                return "'single'";
            // A string is IEnumerable<char> to the walk below, and already itself on the wire.
            case SpecialType.System_String:
                return null;
        }

        if (type is IArrayTypeSymbol array)
            return List(array.ElementType, referenced, visiting);

        if (type is not INamedTypeSymbol named) return null;

        if (Scalar(named) is { } scalar) return scalar;

        // A dictionary before the enumerable walk — it IS IEnumerable<KeyValuePair<,>>, but it
        // crosses as a JSON object, which always has to become the runtime's dictionary class.
        if (DictionaryTypes(named) is var (keyType, valueType))
            return DictionarySpec(named, keyType, valueType, referenced, visiting);

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
            referenced.InSource.Add(named.Name);
            return named.Name;
        }

        // A type the runtime ships no export for is fenced (EQ2010) wherever a component names it,
        // and a map that described it would put its name, or its members', in the emitted module.
        if (named.IsHostOnly()) return null;

        // A data type from a REFERENCED assembly has no twin to name — a page library's domain
        // record is the ordinary case — but the model still knows its members, so the boundary
        // stays typed STRUCTURALLY: `{ members: { downloads: 'long' } }` coerces the named members
        // onto a copy of the plain object, no prototype involved. Without this, a foreign record's long crossed as the
        // string EqJson wrote and met BigInt arithmetic in the browser — on a page the build had
        // accepted and the server had rendered perfectly.
        //
        // Outside System/Microsoft only: the BCL's data shapes are either scalars handled above or
        // types whose members are not payload.
        if (!named.Locations.Any(location => location.IsInSource)
            && !IsPlatformNamespace(named)
            && visiting.Add(named))
        {
            // A recursion STACK, not a memo: the mark exists so a self-referential foreign type
            // cannot recurse forever, and it comes off on the way out — left on, the SECOND field
            // of the same foreign record silently got no spec at all.
            try
            {
                return MembersSpec(named, referenced, visiting, twin: named.IsRuntimeProvided() ? named.Name : null);
            }
            finally
            {
                visiting.Remove(named);
            }
        }

        return null;
    }

    /// <summary>The structural member map for a type from a referenced assembly, or null when no
    /// member needs coercion. Names are camelCased exactly as EqJson writes them. A type the
    /// RUNTIME ships (a vocabulary value type such as <c>Rect</c>) names its twin as <c>of</c>, so
    /// the copy is built on that prototype: a structural copy alone arrived as a plain object, and
    /// a Rect in a payload lost its getters and methods, which is what the typed boundary replaced
    /// when its floats started to hydrate.</summary>
    private static string? MembersSpec(INamedTypeSymbol named, References referenced, HashSet<INamedTypeSymbol> visiting,
        string? twin)
    {
        var entries = DataMembers(named)
            .Select(member => (member.Name, Spec: Of(member.Type, referenced, visiting)))
            .Where(member => member.Spec is not null)
            .Select(member => $"{member.Name.ToCamelCase()}: {member.Spec}")
            .ToList();
        if (entries.Count == 0) return null;
        if (twin is null) return $"{{ members: {{ {string.Join(", ", entries)} }} }}";
        referenced.Runtime.Add(twin);
        return $"{{ of: {twin}, members: {{ {string.Join(", ", entries)} }} }}";
    }

    private static bool IsPlatformNamespace(INamedTypeSymbol named)
    {
        var space = named.ContainingNamespace?.ToDisplayString() ?? string.Empty;
        return space == "System" || space.StartsWith("System.", System.StringComparison.Ordinal)
            || space == "Microsoft" || space.StartsWith("Microsoft.", System.StringComparison.Ordinal);
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

    private static string? List(ITypeSymbol element, References referenced, HashSet<INamedTypeSymbol> visiting) =>
        Of(element, referenced, visiting) is { } inner ? $"[{inner}]" : null;

    /// <summary>The key and value types of a dictionary-shaped type — itself or any interface it
    /// implements constructed from <c>IDictionary&lt;,&gt;</c> / <c>IReadOnlyDictionary&lt;,&gt;</c>.</summary>
    private static (ITypeSymbol Key, ITypeSymbol Value)? DictionaryTypes(INamedTypeSymbol named) =>
        SelfAndInterfaces(named)
            .FirstOrDefault(i => i.Arity == 2 && IsSystemCollection(i)
                && i.OriginalDefinition.MetadataName is "IDictionary`2" or "IReadOnlyDictionary`2")
            is { } dictionary
                ? (dictionary.TypeArguments[0], dictionary.TypeArguments[1])
                : null;

    /// <summary>
    /// <c>{ dict: values, key: tag, byValue: true, sorted: true }</c>: how each value hydrates (null
    /// when it arrives as it is), how a property name becomes the key, and which class holds the
    /// entries — a sorted one's own, or the runtime's <c>Dictionary</c>, finding its keys by value
    /// where the key type's default comparer does (<see cref="DictionaryStrategy.EqualsByValue"/>).
    /// </summary>
    private static string DictionarySpec(INamedTypeSymbol dictionary, ITypeSymbol key, ITypeSymbol value,
        References referenced, HashSet<INamedTypeSymbol> visiting)
    {
        var parts = new List<string> { $"dict: {Of(value, referenced, visiting) ?? "null"}" };
        if (KeyTag(key) is { } tag) parts.Add($"key: {tag}");
        if (dictionary.DictionaryFactory() is Eq.SortedDictionary or Eq.SortedList) parts.Add("sorted: true");
        else if (DictionaryStrategy.EqualsByValue(key)) parts.Add("byValue: true");
        return $"{{ {string.Join(", ", parts)} }}";
    }

    /// <summary>
    /// How the property name System.Text.Json writes for a key of this type becomes the key (a
    /// <c>HydrationKey</c> of <c>utils/hydrate.ts</c>): a number, a bool, or a compat scalar by its tag.
    /// Null where the name IS the key: a string, a char, a <c>Guid</c>, an enum's camelCase name.
    /// </summary>
    private static string? KeyTag(ITypeSymbol key)
    {
        var type = key.UnwrapNullable() ?? key;
        switch (type.SpecialType)
        {
            case SpecialType.System_Boolean:
                return "'bool'";
            case SpecialType.System_Decimal:
                return "'decimal'";
            case SpecialType.System_Int64 or SpecialType.System_UInt64:
                return "'long'";
            case SpecialType.System_Single:
                return "'single'";
            case SpecialType.System_SByte or SpecialType.System_Byte or SpecialType.System_Int16
                or SpecialType.System_UInt16 or SpecialType.System_Int32 or SpecialType.System_UInt32
                or SpecialType.System_Double:
                return "'number'";
        }
        return type is INamedTypeSymbol named ? Scalar(named) : null;
    }

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
        var throwaway = new References(new HashSet<string>(), new HashSet<string>());
        return DataMembers(named).Any(member => Of(member.Type, throwaway, visiting) is not null);
    }

    /// <summary>The member map for a record/struct twin — <c>{ id: 'long', price: Money }</c> with
    /// the twin's camelCased member names — or null when no member needs hydration.</summary>
    public static string? Members(INamedTypeSymbol type, ISet<string> referenced, ISet<string> runtime)
    {
        var entries = DataMembers(type)
            .Select(m => (m.Name, Spec: Of(m.Type, referenced, runtime)))
            .Where(m => m.Spec is not null)
            .Select(m => $"{m.Name.ToCamelCase()}: {m.Spec}")
            .ToList();
        return entries.Count > 0 ? $"{{ {string.Join(", ", entries)} }}" : null;
    }
}
