using Microsoft.CodeAnalysis;
using eQuantic.UI.Compiler.CodeGen.Strategies;

namespace eQuantic.UI.Compiler.CodeGen;

/// <summary>
/// The text .NET writes for a value whose type never says what its text is: <c>Object.ToString</c>
/// and <c>ValueType.ToString</c> both write the type's full name, its namespace and the types it is
/// nested in joined by <c>+</c> (<c>My.App.Outer+In</c>). A twin is a JavaScript object, whose own
/// text is <c>[object Object]</c>, and a plain struct's twin printed a record's text, which only a
/// record writes (#570). So a twin whose type declares no <c>ToString</c>, and derives none from a
/// base the browser carries, writes its full name.
/// </summary>
internal static class DefaultText
{
    /// <summary>
    /// The full name a value of <paramref name="type"/> writes as its text, or null where the type
    /// writes something else: a record, whose text is synthesized; a type that declares
    /// <c>ToString</c> or derives it from a base that does; an exception, whose text is its message's;
    /// a class over a base the app does not declare, whose twin the browser does not build; and a
    /// generic type, whose name carries type arguments the browser does not hold.
    /// </summary>
    public static string? Of(INamedTypeSymbol type)
    {
        if (type.IsRecord || ExceptionTypes.Is(type)) return null;
        for (var at = type; at is not null; at = at.ContainingType)
            if (at.IsGenericType) return null;
        for (var at = type; at is not null && at.SpecialType is not (SpecialType.System_Object or SpecialType.System_ValueType);
             at = at.BaseType)
        {
            if (!at.Locations.Any(location => location.IsInSource)) return null;
            if (DeclaresToString(at)) return null;
        }
        return FullName(type);
    }

    private static bool DeclaresToString(INamedTypeSymbol type) =>
        type.GetMembers(nameof(object.ToString)).OfType<IMethodSymbol>()
            .Any(method => !method.IsStatic && method.Parameters.Length == 0);

    /// <summary><c>Type.ToString()</c> of a type that is not generic.</summary>
    private static string FullName(INamedTypeSymbol type)
    {
        var name = type.MetadataName;
        for (var owner = type.ContainingType; owner is not null; owner = owner.ContainingType)
            name = owner.MetadataName + "+" + name;
        return type.ContainingNamespace is { IsGlobalNamespace: false } space
            ? space.ToDisplayString() + "." + name
            : name;
    }
}
