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

    /// <summary>Each value <paramref name="component"/> carries: the twin's name for it, and its C# type.</summary>
    public static IReadOnlyList<(string Key, ITypeSymbol? Type)> Of(INamedTypeSymbol component, Compilation compilation)
    {
        var carried = new List<(string Key, ITypeSymbol? Type)>();
        foreach (var attribute in compilation.Assembly.GetAttributes())
        {
            if (attribute.AttributeClass?.ToDisplayString() != AttributeName) continue;
            if (attribute.ConstructorArguments is not
                [{ Value: string componentName }, { Value: string declaringName }, { Value: string member }, { Value: int kind }])
                continue;
            if (!SymbolEqualityComparer.Default.Equals(
                    compilation.GetTypeByMetadataName(componentName), component.OriginalDefinition))
                continue;

            carried.Add((member.ToCamelCase(), TypeOf(compilation.GetTypeByMetadataName(declaringName), member, kind)));
        }

        return carried;
    }

    /// <summary>The C# type of the member an entry names, by the kind it was declared with.</summary>
    private static ITypeSymbol? TypeOf(INamedTypeSymbol? declaring, string member, int kind) => kind switch
    {
        // HydratedMemberKind.Field
        0 => declaring?.GetMembers(member).OfType<IFieldSymbol>().FirstOrDefault()?.Type,
        // HydratedMemberKind.Property
        1 => declaring?.GetMembers(member).OfType<IPropertySymbol>().FirstOrDefault()?.Type,
        // HydratedMemberKind.CapturedParameter: a parameter of the primary constructor.
        2 => declaring?.InstanceConstructors
            .SelectMany(constructor => constructor.Parameters)
            .FirstOrDefault(parameter => parameter.Name == member)?.Type,
        _ => null,
    };
}
