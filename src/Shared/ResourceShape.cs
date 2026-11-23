using Microsoft.CodeAnalysis;

namespace eQuantic.UI;

/// <summary>
/// The shape of a resx catalog's typed accessors, as Visual Studio's <c>ResXFileCodeGenerator</c>
/// writes them: a class with a static <c>ResourceManager</c> property of type
/// <c>System.Resources.ResourceManager</c> and a static <c>Culture</c> property of type
/// <c>CultureInfo</c>, whose other static string properties are the accessors, one per key.
///
/// <para>
/// Two assemblies that cannot reference each other have to answer this identically. eqc rewrites an
/// accessor's read into a lookup in the culture the page is in, and the source generator's analyzer
/// warns when one is read once, into a static (EQ3107). A catalog one of them recognised and the other
/// did not would be translated and never warned about, or warned about and never translated. So the
/// shape is one file, linked into both.
/// </para>
/// <para>
/// The generator's class itself is NOT static (it carries a private constructor), so the shape reads
/// the members, not the class modifier, and types compare by name and namespace, never by display
/// string, which a nullable annotation would silently unmatch.
/// </para>
/// </summary>
internal static class ResourceShape
{
    /// <summary>Whether <paramref name="type"/> is a resx catalog's accessor class.</summary>
    public static bool IsResourceClass(INamedTypeSymbol? type)
    {
        if (type is not { TypeKind: TypeKind.Class }) return false;
        var resourceManager = false;
        var culture = false;
        foreach (var member in type.GetMembers())
        {
            if (member is not IPropertySymbol { IsStatic: true } property) continue;
            if (property.Name == "ResourceManager" && IsType(property.Type, "System.Resources", "ResourceManager"))
                resourceManager = true;
            else if (property.Name == "Culture" && IsType(property.Type, "System.Globalization", "CultureInfo"))
                culture = true;
            if (resourceManager && culture) return true;
        }

        return false;
    }

    /// <summary>A static string property of a resource class: the accessor of one key. The
    /// <c>ResourceManager</c> and <c>Culture</c> properties exclude themselves by their type.</summary>
    public static bool IsResourceAccessor(IPropertySymbol? property) =>
        property is { IsStatic: true, Type.SpecialType: SpecialType.System_String }
        && IsResourceClass(property.ContainingType);

    private static bool IsType(ITypeSymbol type, string ns, string name) =>
        type.Name == name && type.ContainingNamespace?.ToDisplayString() == ns;
}
