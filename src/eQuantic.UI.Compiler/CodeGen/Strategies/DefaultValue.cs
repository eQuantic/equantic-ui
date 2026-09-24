using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

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
    public static string Of(ITypeSymbol? type, ConversionContext context)
    {
        var value = Of(type, named => (named.Locations.Any(location => location.IsInSource)
            ? context.UsedAppTypes
            : context.UsedRuntimeTypes).Add(named.Name));
        if (value.Contains("$eq.")) context.UsedHelpers.Add(Eq.Import);
        return value;
    }

    /// <summary>The default, with no context to tell about the helper import — the emitter's field
    /// path already scans what it emits for <c>$eq.</c> and adds it.</summary>
    public static string Of(ITypeSymbol? type) => Of(type, named: null);

    /// <param name="type">The type whose default to write.</param>
    /// <param name="named">Told of every struct the value constructs, for its import.</param>
    private static string Of(ITypeSymbol? type, Action<INamedTypeSymbol>? named)
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
                return $"{Eq.Long}(0)";
            case SpecialType.System_Decimal:
                return $"{Eq.Dec}(0)";
            case SpecialType.System_Char:
                return "'\\0'";
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

        // A STRUCT's default is its zero instance, and C# never has a null one. The twin can build
        // it when its bare constructor zeroes every component: a struct the compiler EMITS (one of
        // the app's with a twin to build, or one from a namespace it transpiles whole), whose
        // parameters default to their own types' zeros by this same rule, or a vocabulary struct
        // whose hand-written twin says it does ([ZeroConstructs]). `new CodeGrid()` held a null
        // Point on the web before this.
        if (type is INamedTypeSymbol { TypeKind: TypeKind.Struct } structType && ZeroConstructs(structType))
        {
            named?.Invoke(structType);
            return ConstructsBeyondZero(structType) ? ZeroOf(structType, named) : $"new {structType.Name}()";
        }

        // A tuple is an ARRAY on this side, and its zero is an array of its elements' zeros.
        if (type is INamedTypeSymbol { IsTupleType: true } tuple)
            return "[" + string.Join(", ", tuple.TupleElements.Select(element => Of(element.Type, named))) + "]";

        // A nullable value type defaults to the null one; every reference type does too. A struct
        // whose twin cannot build its zero (a hand-written vocabulary twin not marked
        // [ZeroConstructs]) gets its twin's own default instead, which is what `undefined` asks
        // for: the twin's constructor defaults (`style: BoxStyle = new BoxStyle()`) and its
        // `!== undefined` checks apply for undefined and never for null, and C# has no null struct.
        // ParameterDefaultLiteral fills an omitted `= default` struct argument by the same rule.
        return type is { IsValueType: true } && !type.IsNullableValue() ? "undefined" : "null";
    }

    /// <summary>
    /// Whether a struct's twin constructor gives a member more than its zero: a field's or a
    /// property's initializer, a positional parameter's default, or an explicit parameterless
    /// constructor's body. <c>default(T)</c> runs none of them, and the twin's bare <c>new T()</c>
    /// runs all of them, so such a struct's zero is built with every member's own zero passed in
    /// instead: <c>default(Counter)</c> held the <c>Step = 2</c> C# never gives it.
    /// </summary>
    private static bool ConstructsBeyondZero(INamedTypeSymbol type) =>
        type.DeclaringSyntaxReferences.Select(reference => reference.GetSyntax()).OfType<TypeDeclarationSyntax>()
            .Any(declaration =>
                declaration.ParameterList?.Parameters.Any(parameter => parameter.Default is not null) == true
                || declaration.Members.Any(member => member switch
                {
                    FieldDeclarationSyntax field => !field.Modifiers.Any(SyntaxKind.StaticKeyword)
                        && !field.Modifiers.Any(SyntaxKind.ConstKeyword)
                        && field.Declaration.Variables.Any(variable => variable.Initializer is not null),
                    PropertyDeclarationSyntax property => !property.Modifiers.Any(SyntaxKind.StaticKeyword)
                        && property.Initializer is not null,
                    ConstructorDeclarationSyntax constructor => !constructor.Modifiers.Any(SyntaxKind.StaticKeyword)
                        && constructor.ParameterList.Parameters.Count == 0,
                    _ => false,
                }));

    /// <summary>The zero of such a struct: every member's own zero, passed to the twin's
    /// constructor in the order it takes them, which is the order its value members are listed in.</summary>
    private static string ZeroOf(INamedTypeSymbol type, Action<INamedTypeSymbol>? named)
    {
        var declaration = type.DeclaringSyntaxReferences.Select(reference => reference.GetSyntax())
            .OfType<TypeDeclarationSyntax>().First();
        var zeros = declaration.ValueMembers().Select(member => Of(MemberType(type, member.Display), named));
        return $"new {type.Name}({string.Join(", ", zeros)})";
    }

    /// <summary>A value member's type: a field's, or a property's, which a positional parameter
    /// declares too.</summary>
    private static ITypeSymbol? MemberType(INamedTypeSymbol type, string name) =>
        type.GetMembers(name).FirstOrDefault() switch
        {
            IFieldSymbol field => field.Type,
            IPropertySymbol property => property.Type,
            _ => null,
        };

    /// <summary>Whether the twin of <paramref name="type"/> builds its zero instance from a bare
    /// constructor — see <see cref="Of(ITypeSymbol?)"/>.</summary>
    private static bool ZeroConstructs(INamedTypeSymbol type)
    {
        if (type.IsGenericType || type.SpecialType != SpecialType.None) return false;
        // Nullable<T> is a struct too, and its default is null — handled above, never here.
        if (type.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T) return false;
        if (type.GetAttributes().Any(a => a.AttributeClass?.Name == "ZeroConstructsAttribute")) return true;
        // In source, only when a twin is emitted at all: a struct the emitter refuses (an empty one)
        // has no class, and `new Empty()` would name one nothing wrote.
        if (type.Locations.Any(location => location.IsInSource)) return RecordTypeEmitter.EmitsTwin(type);
        var ns = type.ContainingNamespace?.ToDisplayString() ?? "";
        return Services.RuntimeProvidedTypeScanner.IsTranspiledNamespace(ns);
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
