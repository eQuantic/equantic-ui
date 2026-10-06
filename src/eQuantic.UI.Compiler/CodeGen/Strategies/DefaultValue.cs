using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using eQuantic.UI.Compiler.CodeGen.Ir;

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
    /// reference type or unknown. Every struct the value names is imported: the default is text no
    /// syntax spells (<c>new T[n]</c> and <c>default(T)</c> never write <c>new Point()</c>), so the
    /// module's own scan cannot see it, and an unimported twin fails the module when it loads.</summary>
    public static string Of(ITypeSymbol? type, ConversionContext context) => Of(type, context, typeParameter: null);

    /// <summary>
    /// The default of <paramref name="type"/> where a type parameter's zero is given: inside a generic
    /// struct's <c>$zero</c>, which takes the zero of each of its type arguments from its caller,
    /// a member of type <c>T</c> is that zero, and a <c>Pair&lt;T&gt;</c> passes it on. The open
    /// declaration cannot know it: <c>default(Pair&lt;int&gt;).First</c> was null where C# has 0
    /// (found by Copilot's review of #608).
    /// </summary>
    internal static string Of(ITypeSymbol? type, ConversionContext context, Func<ITypeParameterSymbol, string?>? typeParameter)
    {
        var value = Of(type, named => (IsRuntimeProvided(named) ? context.UsedRuntimeTypes : context.UsedAppTypes)
            .Add(named.Name), typeParameter);
        if (value.Contains("$eq.")) context.UsedHelpers.Add(Eq.Import);
        return value;
    }

    /// <summary>Whether a struct's twin comes from the runtime, by the rule the import scan uses
    /// (<see cref="Services.RuntimeProvidedTypeScanner"/>): a runtime-provided namespace, or a type
    /// marked <c>[RuntimeProvided]</c>. Only the rest is one of the app's own modules. Being in
    /// source does not decide it: a source-tree build compiles the library's own structs from source,
    /// and they still come from <c>@equantic/runtime</c> (found in review, #405).</summary>
    private static bool IsRuntimeProvided(INamedTypeSymbol type) =>
        Services.RuntimeProvidedTypeScanner.IsRuntimeProvidedNamespace(type.ContainingNamespace?.ToDisplayString() ?? "")
        || type.GetAttributes().Any(attribute => attribute.AttributeClass?.Name == "RuntimeProvidedAttribute")
        || !type.Locations.Any(location => location.IsInSource);

    /// <param name="type">The type whose default to write.</param>
    /// <param name="named">Told of every struct the value constructs, for its import.</param>
    /// <param name="typeParameter">The zero a type parameter has where its caller gives one, or null.</param>
    private static string Of(ITypeSymbol? type, Action<INamedTypeSymbol>? named,
        Func<ITypeParameterSymbol, string?>? typeParameter = null)
    {
        if (type is ITypeParameterSymbol parameter && typeParameter?.Invoke(parameter) is { } given) return given;
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
                return $"{Eq.Long}(0)";
            case SpecialType.System_Decimal:
                return $"{Eq.Dec}(0)";
            case SpecialType.System_Char:
                return JsStringLiteral.Quote("\0");
            case SpecialType.System_String or SpecialType.System_Object:
                return "null";
            case SpecialType.System_DateTime:
                return $"{Eq.DateTime}.minValue()";
        }

        // The time and identity structs the runtime twins: each one's zero is what its MinValue, Zero
        // or Empty crosses as. `new DateTime[1]` held null on the web, where C# holds 0001-01-01.
        switch (type?.ToDisplayString())
        {
            case "System.TimeSpan":
                return $"{Eq.TimeSpan}.zero";
            case "System.DateOnly":
                return "$eq.time.dateOnly.minValue()";
            case "System.TimeOnly":
                return "$eq.time.timeOnly.minValue()";
            case "System.DateTimeOffset":
                return $"{Eq.DateTimeOffset}.minValue()";
            case "System.Guid":
                return "'00000000-0000-0000-0000-000000000000'";
        }

        // A KeyValuePair is the pair a dictionary yields, so its zero is the pair of the two zeros (#433).
        if (type is INamedTypeSymbol { OriginalDefinition.MetadataName: "KeyValuePair`2", TypeArguments: [var key, var value] } pair
            && pair.ContainingNamespace?.ToDisplayString() == "System.Collections.Generic")
            return $"{Eq.Pair}({Of(key, named, typeParameter)}, {Of(value, named, typeParameter)})";

        // An enum is its member NAME at runtime, so the default is the member whose value is 0.
        // .NET still yields the numeric 0 when the enum declares no such member.
        if (type is { TypeKind: TypeKind.Enum })
        {
            // A [Flags] enum is a NUMBER on this side — the bits have to be combinable — so its
            // default is 0 whatever its zero member is called. An ordinary enum is its member
            // NAME, and the default is the member whose value is zero; .NET still yields the
            // numeric 0 when the enum declares no such member.
            if (type is INamedTypeSymbol enumType && enumType.IsFlagsEnum()) return "0";
            var zero = type.GetMembers().OfType<IFieldSymbol>()
                .FirstOrDefault(field => field.HasConstantValue && IsZero(field.ConstantValue));
            return zero is null ? "0" : $"'{zero.Name.ToCamelCase()}'";
        }

        // A value the browser holds as DATA (`[TwinIsData]`) is its members, so its zero is each
        // member's zero written out, `{ r: 0, g: 0, b: 0, a: 0 }` for a `Color`, with no twin to build.
        if (type is INamedTypeSymbol data && data.TwinIsData())
            return TwinData.Literal(data, _ => null, member => Of(member, named, typeParameter));

        // A STRUCT's default is its zero instance, and C# never has a null one: the `$zero()` every
        // struct twin the compiler EMITS carries (one of the app's, or one from a namespace it
        // transpiles whole, a generic one included), built without the constructor, or the bare
        // constructor of a vocabulary struct whose hand-written twin says it zeroes it
        // ([ZeroConstructs]). `new CodeGrid()` held a null Point on the web before this; and a bare
        // `new S()` for every struct whose constructor did nothing but zero it ran an all-optional
        // constructor for a zero, started the type's initialization, and from another assembly ran
        // its constructor's defaults, where nothing said which constructors a struct of metadata had.
        if (type is INamedTypeSymbol { TypeKind: TypeKind.Struct } structType && ZeroOf(structType, named, typeParameter) is { } structZero)
        {
            named?.Invoke(structType);
            return structZero;
        }

        // A tuple is an ARRAY on this side, and its zero is an array of its elements' zeros.
        if (type is INamedTypeSymbol { IsTupleType: true } tuple)
            return "[" + string.Join(", ", tuple.TupleElements.Select(element => Of(element.Type, named, typeParameter))) + "]";

        // A nullable value type defaults to the null one; every reference type does too. A struct
        // whose twin cannot build its zero (a hand-written vocabulary twin not marked
        // [ZeroConstructs]) gets its twin's own default instead, which is what `undefined` asks
        // for: the twin's constructor defaults (`style: BoxStyle = new BoxStyle()`) and its
        // `!== undefined` checks apply for undefined and never for null, and C# has no null struct.
        // ObjectCreationStrategy.DefaultLiteralFor fills an omitted `= default` struct argument by the
        // same rule.
        return type is { IsValueType: true } && !type.IsNullableValue() ? "undefined" : "null";
    }

    /// <summary>
    /// Whether the default of <paramref name="type"/> constructs a twin (<c>new Cell()</c>, a struct's
    /// <c>$zero()</c>, or a tuple or a pair holding one): code that runs, and names another module,
    /// where every other default is a value. Asked by the type initializer (TypeInitializer.Orders),
    /// which builds such a static on first use and never while its module is evaluated.
    /// </summary>
    internal static bool Constructs(ITypeSymbol? type)
    {
        var constructs = false;
        Of(type, _ => constructs = true);
        return constructs;
    }

    /// <summary>
    /// How the twin of <paramref name="type"/> gives its zero instance (see
    /// <see cref="Of(ITypeSymbol?, ConversionContext)"/>), or null where it has none to give: the
    /// <c>$zero()</c> of a twin the compiler writes, or the bare constructor of a hand-written one
    /// marked <c>[ZeroConstructs]</c>. A generic struct's <c>$zero</c> takes the zero of each of its
    /// type arguments, which only the closed type here knows: <c>Pair.$zero(0)</c> for a
    /// <c>Pair&lt;int&gt;</c>.
    /// </summary>
    private static string? ZeroOf(INamedTypeSymbol type, Action<INamedTypeSymbol>? named,
        Func<ITypeParameterSymbol, string?>? typeParameter)
    {
        if (type.SpecialType != SpecialType.None) return null;
        // Nullable<T> is a struct too, and its default is null — handled above, never here.
        if (type.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T) return null;
        if (type.GetAttributes().Any(a => a.AttributeClass?.Name == "ZeroConstructsAttribute")) return $"new {type.Name}()";
        // In source, only when a twin is emitted at all: a struct declared only by an empty partial
        // declaration has no class, and its zero would name one nothing wrote.
        var written = type.Locations.Any(location => location.IsInSource)
            ? RecordTypeEmitter.EmitsTwin(type)
            : Services.RuntimeProvidedTypeScanner.IsTranspiledNamespace(type.ContainingNamespace?.ToDisplayString() ?? "")
              || type.TwinIsTranspiled();
        if (!written) return null;
        var zeros = type.TypeArguments.Select(argument => Of(argument, named, typeParameter));
        return $"{type.TwinReference()}.$zero({string.Join(", ", zeros)})";
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
