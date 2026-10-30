using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using eQuantic.UI.Compiler.CodeGen.Extensions;
using eQuantic.UI.Compiler.CodeGen.Ir;

namespace eQuantic.UI.Compiler.CodeGen;

/// <summary>
/// What <c>System.Text.Json</c> writes for a type under <c>EqJson.Options</c>, for a twin to write the same: its public
/// instance properties with a public getter, and what <c>[JsonInclude]</c> adds, less what <c>[JsonIgnore]</c> takes
/// out. <c>JSON.stringify</c> writes every own key of an object, and the runtime cannot tell a field from a property
/// by an instance's keys, so fields, private state and an explicit implementation's store reached the JSON a server
/// action received, where the server's serializer never writes them (#745). Only the static type knows.
/// </summary>
internal static class JsonContract
{
    /// <summary>
    /// The JavaScript array of what a twin of <paramref name="type"/> writes: each member's twin name, or a pair of its
    /// JSON name and its twin name where <c>[JsonPropertyName]</c> sets one. Null where the twin holds nothing else, so
    /// its own keys are the contract, and for a type of the SDK's or one a framework class derives, which never crosses
    /// to a server action, so its twin carries no list.
    /// </summary>
    internal static string? Members(INamedTypeSymbol type)
    {
        var chain = new List<INamedTypeSymbol>();
        for (var at = type; at is { SpecialType: not (SpecialType.System_Object or SpecialType.System_ValueType) }; at = at.BaseType)
        {
            if (!at.Locations.Any(location => location.IsInSource) || at.IsFramework()) return null;
            chain.Insert(0, at);
        }
        if (!chain.Any(HoldsWhatItDoesNotWrite)) return null;

        var written = new List<string>();
        var seen = new HashSet<string>();
        for (var at = chain.Count - 1; at >= 0; at--)
        {
            foreach (var member in chain[at].GetMembers())
            {
                if (!IsWritten(member) || !seen.Add(member.Name)) continue;
                var twin = JsStringLiteral.Quote(member.Name.ToCamelCase());
                written.Add(JsonName(member) is { } name ? $"[{JsStringLiteral.Quote(name)}, {twin}]" : twin);
            }
        }
        return $"[{string.Join(", ", written)}]";
    }

    /// <summary>Whether a type's twin holds a key the serializer does not write.</summary>
    private static bool HoldsWhatItDoesNotWrite(INamedTypeSymbol type) =>
        type.GetMembers().Any(member => member switch
        {
            IFieldSymbol { IsStatic: false, IsImplicitlyDeclared: false, IsConst: false } => true,
            IPropertySymbol { IsStatic: false, IsImplicitlyDeclared: false } property => !IsWritten(property),
            _ => false,
        }) || (!type.IsRecord && type.InstanceConstructors.Any(constructor => constructor.Parameters.Length > 0
            && constructor.DeclaringSyntaxReferences.Any(reference => reference.GetSyntax() is TypeDeclarationSyntax)));

    private static bool IsWritten(ISymbol member)
    {
        if (member.IsStatic || IsIgnored(member)) return false;
        return member switch
        {
            IPropertySymbol { IsIndexer: false, GetMethod: not null, ExplicitInterfaceImplementations.Length: 0 } property =>
                property.DeclaredAccessibility == Accessibility.Public && property.GetMethod.DeclaredAccessibility == Accessibility.Public
                    ? property.Name != "EqualityContract"
                    : HasAttribute(property, "JsonIncludeAttribute"),
            IFieldSymbol { IsImplicitlyDeclared: false } field => HasAttribute(field, "JsonIncludeAttribute"),
            _ => false,
        };
    }

    /// <summary>Whether <c>[JsonIgnore]</c> takes a member out always, its default condition, where the others
    /// (<c>WhenWritingNull</c>, <c>WhenWritingDefault</c>, <c>Never</c>) decide by the value.</summary>
    private static bool IsIgnored(ISymbol member) =>
        member.GetAttributes().FirstOrDefault(attribute => attribute.AttributeClass?.Name == "JsonIgnoreAttribute") is { } ignore
        && ignore.NamedArguments.FirstOrDefault(argument => argument.Key == "Condition").Value.Value is null or 1;

    /// <summary>The name <c>[JsonPropertyName]</c> gives a member, or null for its own.</summary>
    private static string? JsonName(ISymbol member) =>
        member.GetAttributes().FirstOrDefault(attribute => attribute.AttributeClass?.Name == "JsonPropertyNameAttribute")
            ?.ConstructorArguments.FirstOrDefault().Value as string;

    private static bool HasAttribute(ISymbol member, string name) =>
        member.GetAttributes().Any(attribute => attribute.AttributeClass?.Name == name);
}
