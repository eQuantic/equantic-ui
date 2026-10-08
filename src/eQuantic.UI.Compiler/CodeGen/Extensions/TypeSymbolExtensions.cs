using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;

using eQuantic.UI.Compiler.CodeGen.Extensions;
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

    /// <summary>
    /// WHICH framework base the type reaches by walking its chain, or <see cref="ComponentBaseKind.None"/>.
    ///
    /// <para>
    /// The four names are the abstract bases the framework defines, and they are matched here and
    /// nowhere else. An intermediate base the app or a library wrote is reached BY THE WALK — naming
    /// one would be the brittleness this replaces.
    /// </para>
    ///
    /// <para>
    /// The BOOLEAN questions below answer from this rather than walking again, so there is one
    /// traversal and one list. That matters beyond tidiness: the parser used to ask a chain walk
    /// whether a class was a component and then decide WHAT to parse from the written base name, and
    /// the two answers disagreed for every component over an app-owned base.
    /// </para>
    /// </summary>
    public static ComponentBaseKind ResolveComponentBase(this ITypeSymbol? type)
    {
        for (var t = type; t != null; t = t.BaseType)
        {
            switch (t.Name)
            {
                case "StatefulComponent": return ComponentBaseKind.StatefulComponent;
                case "StatelessComponent": return ComponentBaseKind.StatelessComponent;
                case "HtmlElement": return ComponentBaseKind.HtmlElement;
                case "ComponentState": return ComponentBaseKind.ComponentState;
            }
        }
        return ComponentBaseKind.None;
    }

    /// <summary>
    /// Whether the vocabulary's assembly marks <paramref name="type"/> <c>[TwinIsTranspiled]</c>: its
    /// twin is transpiled from its C# with the runtime, as an app's type's is, though its namespace is
    /// the vocabulary's, whose other twins are hand-written (#592). Asked of the symbol, since an app
    /// reaches it as metadata.
    /// </summary>
    internal static bool TwinIsTranspiled(this ITypeSymbol type) =>
        type.GetAttributes().Any(attribute => attribute.AttributeClass is { Name: "TwinIsTranspiledAttribute" } marker
            && marker.ContainingNamespace?.ToDisplayString() == "eQuantic.UI.Primitives");

    /// <summary>
    /// The name a type's twin is written under: its own name after the chain of the types that contain
    /// it, joined by <c>$</c> (<c>Cart$Item</c>, <c>A$B$C</c>, #584), which no C# type can take, so a
    /// nested type's twin never meets a top-level type's. Generic arguments are erased
    /// (<c>Box&lt;T&gt;.Node</c> is <c>Box$Node</c>). Its module is named the same, so a reference
    /// imports it as it imports any type's. A top-level type's is its own name.
    /// </summary>
    internal static string TwinTypeName(this INamedTypeSymbol type) =>
        eQuantic.UI.TwinName.OfNested(type.ContainingType?.TwinTypeName(), type.Name);

    /// <summary>The twin name of a class, record or struct declared inside another type whose twin eqc
    /// writes (in the source, or in a namespace it transpiles whole), which differs from the name it is
    /// written with (<see cref="TwinTypeName(INamedTypeSymbol)"/>); null for any other type, whose name a
    /// reference keeps.</summary>
    internal static string? NestedTwinName(this INamedTypeSymbol type) =>
        type is { ContainingType: not null, TypeKind: TypeKind.Class or TypeKind.Struct }
        && (type.Locations.Any(location => location.IsInSource)
            || Services.RuntimeProvidedTypeScanner.IsTranspiledNamespace(type.ContainingNamespace?.ToDisplayString() ?? string.Empty))
            ? type.TwinTypeName()
            : null;

    /// <summary>
    /// The name a REFERENCE to a type writes, and imports: a nested type's twin where eqc writes it
    /// (<see cref="NestedTwinName"/>), and the type's own name otherwise, which is what a top-level type's
    /// twin and a hand-written twin of the runtime are called (a nested vocabulary type keeps its simple
    /// name there). Registering one name and calling another imported nothing the call named.
    /// </summary>
    internal static string TwinReference(this INamedTypeSymbol type) => type.NestedTwinName() ?? type.Name;

    /// <summary>
    /// True when the type derives (transitively) from a framework component/state base. Walking the base
    /// chain recognises a component that extends another user or library component without enumerating
    /// every intermediate base — replacing brittle direct-base-name matching.
    /// </summary>
    public static bool IsUiComponent(this ITypeSymbol? type) =>
        type.ResolveComponentBase() != ComponentBaseKind.None;

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
    public static bool IsComponentState(this ITypeSymbol? type) =>
        type.ResolveComponentBase() == ComponentBaseKind.ComponentState;

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

    /// <summary>Full-name match (e.g. <c>"System.DateTime"</c>), transparently unwrapping <c>Nullable&lt;T&gt;</c>.
    /// A reference type's nullable annotation is not part of its name either: the display string
    /// carries it, so a <c>CultureInfo?</c> parameter never matched <c>"System.Globalization.CultureInfo"</c>
    /// (#528).</summary>
    public static bool IsNamed(this ITypeSymbol? type, string fullName) =>
        type.UnwrapNullable()?.WithNullableAnnotation(NullableAnnotation.NotAnnotated).ToDisplayString() == fullName;

    /// <summary>An integral type (signed/unsigned 8–64 bit), unwrapping <c>Nullable&lt;T&gt;</c>.</summary>
    public static bool IsIntegral(this ITypeSymbol? type)
    {
        var t = type.UnwrapNullable();
        return t != null && Array.IndexOf(IntegralTypes, t.SpecialType) >= 0;
    }

    /// <summary>Whether this IS a <c>Nullable&lt;T&gt;</c> — the question every lifted operator
    /// asks, and the one <see cref="IsNamed"/> deliberately does not (it unwraps, so a
    /// <c>DateOnly?</c> answers yes to "is this a DateOnly").</summary>
    public static bool IsNullableValue(this ITypeSymbol? type) =>
        type is INamedTypeSymbol { OriginalDefinition.SpecialType: SpecialType.System_Nullable_T };

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
    /// True when <paramref name="type"/> is one of the dictionaries of System.Collections.Generic:
    /// <c>Dictionary</c>, <c>IDictionary</c>, <c>IReadOnlyDictionary</c>, <c>SortedDictionary</c> or
    /// <c>SortedList</c>. Each is a runtime class on this side, the runtime's <c>Dictionary</c> or its
    /// <c>SortedMap</c>, and <see cref="DictionaryFactory"/> names the factory that constructs it. The
    /// list is <see cref="BoundaryShape"/>'s, which the source generator reads too.
    /// </summary>
    internal static bool IsDictionary(this ITypeSymbol? type) => BoundaryShape.DictionaryName(type) is not null;

    /// <summary>The runtime factory a dictionary of this type is constructed by: a sorted one's own,
    /// or the runtime's <c>Dictionary</c>; null when the type is not a dictionary.</summary>
    internal static string? DictionaryFactory(this ITypeSymbol? type) => BoundaryShape.DictionaryName(type) switch
    {
        null => null,
        "SortedDictionary" => Eq.SortedDictionary,
        "SortedList" => Eq.SortedList,
        _ => Eq.Dictionary,
    };

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
    /// The declaring static class of a C# 14 extension-BLOCK member (<c>extension(T receiver) { … }</c>):
    /// the member's containing type is Roslyn's unnamed extension grouping
    /// (<see cref="INamedTypeSymbol.IsExtension"/>), and ITS parent is the class the emitter lowers
    /// the member onto as a static. Null for every other kind of member — including classic
    /// <c>this</c>-parameter extensions, which keep their own reduced-form path.
    /// </summary>
    public static INamedTypeSymbol? ExtensionBlockHome(this ISymbol? symbol) =>
        symbol is { ContainingType: { IsExtension: true, ContainingType: { } home } } ? home : null;

    /// <summary>
    /// The name a twin is written by where the code names it, said once for the import too (#584): every
    /// site that writes a type's twin into the output names it through this, so a twin reached only by
    /// inference (an operator on what a call returned, the zero of a type an alias names, a nested
    /// component's construction or type test) is imported as one the syntax names is.
    /// </summary>
    public static string IntroduceTwin(this INamedTypeSymbol type, ConversionContext context)
    {
        // A nested type whose owner never crosses has no twin: EQ2010 where the code names it, however
        // it is reached (an alias, using static, an operator), found by Copilot's third review of #654.
        // A twin it reported is not imported, so the module's import net, which catches a path that
        // reaches no fence, does not say it twice.
        if (context.Converting is not { } at || !type.ReportIfKeptOut(at, context)) type.RegisterIntroduced(context);
        return type.TwinReference();
    }

    /// <summary>
    /// Registers a type name the conversion INTRODUCED into the output (the source never names the
    /// extension home — the call is written on the receiver), in the bucket its namespace decides,
    /// so the import scanner can see it. Same routing the static-call path uses.
    /// </summary>
    public static void RegisterIntroduced(this INamedTypeSymbol home, ConversionContext context)
    {
        if (home.IsRuntimeProvided())
            context.UsedRuntimeTypes.Add(home.TwinReference());
        else
            context.UsedAppTypes.Add(home.TwinReference());
    }

    /// <summary>
    /// Whether the RUNTIME supplies this type — the one rule, for every reader that has to agree
    /// about it.
    /// <para>
    /// Two things answer yes. A runtime-provided NAMESPACE says so implicitly (the shared
    /// vocabulary and component libraries), and <c>[RuntimeProvided]</c> says so explicitly, which
    /// its own doc exists for: it "extends it to runtime-backed types living elsewhere — e.g. the
    /// web adapter <c>VisualNodeComponent</c>".
    /// </para>
    /// <para>
    /// This answers WHERE AN IMPORT COMES FROM, and it is deliberately not the question the
    /// extension-home lowering asks. That one is "does the runtime export a home under this name",
    /// which only the attribute can answer: the namespace is too broad, because
    /// <c>eQuantic.UI.Primitives</c> also holds types the runtime exports no twin for
    /// (<c>CurveEvaluator</c>). Collapsing the two into this predicate sends that type's extension
    /// home again — measured, and caught by the case written for it.
    /// </para>
    /// <para>
    /// What the attribute half fixes HERE is the other direction: a home the attribute marks,
    /// living outside those namespaces (its doc exists for exactly that — the web adapter
    /// <c>VisualNodeComponent</c>), used to be bucketed as an app type. The call was emitted as
    /// <c>Home.method(…)</c> and the parser skips runtime-provided classes, so no app module was
    /// written either, and the call named nothing at all.
    /// </para>
    /// </summary>
    public static bool IsRuntimeProvided(this INamedTypeSymbol type) =>
        Services.RuntimeProvidedTypeScanner.IsRuntimeProvidedNamespace(
            type.ContainingNamespace?.ToDisplayString() ?? string.Empty)
        // By its name as written too, where the attribute binds to an error type (a compilation that
        // does not reference it), as PlainClassModule reads [ServerOnly].
        || type.GetAttributes().Any(a => a.AttributeClass?.Name is "RuntimeProvidedAttribute" or "RuntimeProvided")
        // A nested type is the runtime's where its owner is, by the rule the parser reads
        // (PlainClassModule.OwnersCross), and the runtime carries it under its owner's twin name
        // (`CodeBlock$CodeMetrics`). Where the owner said so by the attribute, the reference imported
        // a sibling module nothing writes (found by Copilot's third review of #654).
        || type.ContainingType?.IsRuntimeProvided() == true;

    /// <summary>
    /// A vocabulary value type whose browser twin is its data alone, as <c>[TwinIsData]</c> declares
    /// (<c>Color</c>): a plain object of its positional members, with its statics and each of its
    /// instance methods, value first, on a companion of its name. Asked of the SYMBOL, so an app's own
    /// type of the same name is the app's, and only of a type the runtime ships.
    /// </summary>
    internal static bool TwinIsData(this ITypeSymbol? type) =>
        type is INamedTypeSymbol named
        && named.IsRuntimeProvided()
        && named.GetAttributes().Any(attribute => attribute.AttributeClass is
            { Name: "TwinIsDataAttribute", ContainingNamespace: { } space }
            && space.ToDisplayString() == "eQuantic.UI.Primitives");

    /// <summary>
    /// The type arguments of <paramref name="type"/> whose parameter carries <c>[HydratesTypeArgument]</c>,
    /// in declaration order: the ones whose hydration spec the twin receives after its constructor's
    /// arguments, the C# type being erased in JavaScript (<c>ServerTopic&lt;T&gt;</c>, #291). Asked of
    /// the SYMBOL, so a parameter of the same name elsewhere means nothing.
    /// </summary>
    internal static IReadOnlyList<ITypeSymbol> HydratedTypeArguments(this ITypeSymbol? type)
    {
        if (type is not INamedTypeSymbol { IsGenericType: true } named) return [];
        var parameters = named.OriginalDefinition.TypeParameters;
        var hydrated = new List<ITypeSymbol>();
        for (var i = 0; i < parameters.Length; i++)
        {
            if (parameters[i].GetAttributes().Any(attribute => attribute.AttributeClass is
                    { Name: "HydratesTypeArgumentAttribute", ContainingNamespace: { } space }
                    && space.ToDisplayString() == "eQuantic.UI.Primitives"))
                hydrated.Add(named.TypeArguments[i]);
        }
        return hydrated;
    }

    /// <summary>
    /// Whether <paramref name="type"/> names a type parameter anywhere in it (<c>T</c>, <c>List&lt;T&gt;</c>,
    /// <c>T[]</c>): a type the browser cannot know, since a type argument is erased in JavaScript.
    /// </summary>
    internal static bool MentionsTypeParameter(this ITypeSymbol type) => type switch
    {
        ITypeParameterSymbol => true,
        IArrayTypeSymbol array => array.ElementType.MentionsTypeParameter(),
        INamedTypeSymbol named => named.TypeArguments.Any(argument => argument.MentionsTypeParameter())
                                  || named.ContainingType?.MentionsTypeParameter() == true,
        _ => false,
    };

    /// <summary>
    /// The members .NET writes in a record's text: the public instance fields and readable properties,
    /// as <c>PrintMembers</c> lists them, a positional record's own first and in its parameters'
    /// order. A type from metadata does not record where a field sits among the properties, so the
    /// rest follow in metadata order, and the conformance suite compares each marked type's text with
    /// .NET's.
    /// </summary>
    internal static IReadOnlyList<ISymbol> PrintedMembers(this INamedTypeSymbol type)
    {
        var printed = type.GetMembers()
            .Where(member => member is IFieldSymbol { IsStatic: false, DeclaredAccessibility: Accessibility.Public }
                or IPropertySymbol { IsStatic: false, DeclaredAccessibility: Accessibility.Public, IsIndexer: false, GetMethod: not null })
            .ToList();
        var positional = PositionalNames(type);
        return printed.OrderBy(member => positional.IndexOf(member.Name) is var at && at >= 0 ? at : positional.Count).ToList();
    }

    /// <summary>
    /// The members a value of a data twin STORES, which its plain object holds: a positional record's
    /// parameters and the public instance fields. A computed property is not one of them, so it is
    /// never written into the data.
    /// </summary>
    internal static IReadOnlyList<ISymbol> DataMembers(this INamedTypeSymbol type)
    {
        var positional = PositionalNames(type);
        return type.PrintedMembers()
            .Where(member => member is IFieldSymbol || positional.Contains(member.Name))
            .ToList();
    }

    /// <summary>The parameters of a record's widest constructor that is not its copy constructor, by name.</summary>
    private static List<string> PositionalNames(INamedTypeSymbol type) =>
        type.InstanceConstructors
            .Where(constructor => !(constructor.Parameters.Length == 1
                && SymbolEqualityComparer.Default.Equals(constructor.Parameters[0].Type, type)))
            .OrderByDescending(constructor => constructor.Parameters.Length)
            .FirstOrDefault()?.Parameters.Select(parameter => parameter.Name).ToList() ?? new List<string>();

    /// <summary>A vocabulary type the runtime ships NO export for: declared <c>[ServerOnly]</c>
    /// outside this compilation. Its name must reach no emitted module, not even a hydration map,
    /// and naming it in a component's shape is EQ2010. One definition, read by the import scanner
    /// and by the hydration spec alike.</summary>
    public static bool IsHostOnly(this INamedTypeSymbol type) =>
        type.GetAttributes().Any(a => a.AttributeClass?.Name == "ServerOnlyAttribute")
        && !type.Locations.Any(location => location.IsInSource);
}
