using Microsoft.CodeAnalysis;

namespace eQuantic.UI.Compiler.CodeGen;

/// <summary>
/// The hydration manifest as eqc reads it: the <c>[assembly: HydratedMember(...)]</c> entries the source
/// generator wrote for one component, which reach eqc's compilation among the generated sources it already
/// reads.
/// <para>
/// The server writes a component's state from the same entries, so the twin adopts exactly what the
/// server sends, each value under the name the twin declares (<see cref="StringExtensions.ToCamelCase"/>,
/// the rule the two share) and coerced by its C# type. A type is matched by the metadata name the
/// manifest records, resolved by the compilation itself (<c>GetTypeByMetadataName</c>), so no second
/// spelling of that name lives here.
/// </para>
/// </summary>
internal static class HydrationManifest
{
    private const string AttributeName = "eQuantic.UI.Primitives.HydratedMemberAttribute";

    /// <summary>
    /// Each value <paramref name="component"/> carries: the twin's name for it, its C# type, and the
    /// projection it crosses as when it is a server value (null when it crosses whole).
    /// </summary>
    public static IReadOnlyList<(string Key, ITypeSymbol? Type, string? Projection)> Of(INamedTypeSymbol component, Compilation compilation)
    {
        var carried = new List<(string Key, ITypeSymbol? Type, string? Projection)>();
        foreach (var attribute in compilation.Assembly.GetAttributes())
        {
            if (attribute.AttributeClass?.ToDisplayString() != AttributeName) continue;
            if (attribute.ConstructorArguments is not
                [{ Value: string componentName }, { Value: string declaringName }, { Value: string member }, { Value: int kind }])
                continue;
            if (!SymbolEqualityComparer.Default.Equals(
                    compilation.GetTypeByMetadataName(componentName), component.OriginalDefinition))
                continue;

            var projection = attribute.NamedArguments
                .Where(argument => argument.Key == "Projection")
                .Select(argument => argument.Value.Value as string)
                .FirstOrDefault();
            // HydratedMemberKind.BackingField crosses into the slot the twin keeps the property's store in.
            var key = kind == 3 ? eQuantic.UI.TwinName.BackingSlot(member) : member.ToCamelCase();
            carried.Add((key, TypeOf(OnChain(component, compilation.GetTypeByMetadataName(declaringName)), member, kind), projection));
        }

        return carried;
    }

    /// <summary>
    /// The type the manifest names, as the component's chain constructs it: an inherited member of
    /// <c>LoadingPage&lt;long&gt;</c> is a <c>List&lt;long&gt;</c>, where the definition the manifest's name resolves
    /// to declares a <c>List&lt;T&gt;</c>, which no spec can say how to coerce.
    /// </summary>
    private static INamedTypeSymbol? OnChain(INamedTypeSymbol component, INamedTypeSymbol? definition)
    {
        if (definition is null) return null;
        for (var type = component; type is not null; type = type.BaseType)
            if (SymbolEqualityComparer.Default.Equals(type.OriginalDefinition, definition.OriginalDefinition))
                return type;
        return definition;
    }

    /// <summary>The C# type of the member an entry names, by the kind it was declared with.</summary>
    private static ITypeSymbol? TypeOf(INamedTypeSymbol? declaring, string member, int kind) => kind switch
    {
        // HydratedMemberKind.Field
        0 => declaring?.GetMembers(member).OfType<IFieldSymbol>().FirstOrDefault()?.Type,
        // HydratedMemberKind.Property, and HydratedMemberKind.BackingField, whose store is of the property's type
        1 or 3 => declaring?.GetMembers(member).OfType<IPropertySymbol>().FirstOrDefault()?.Type,
        // HydratedMemberKind.CapturedParameter: a parameter of the primary constructor.
        2 => declaring?.InstanceConstructors
            .SelectMany(constructor => constructor.Parameters)
            .FirstOrDefault(parameter => parameter.Name == member)?.Type,
        _ => null,
    };
}
