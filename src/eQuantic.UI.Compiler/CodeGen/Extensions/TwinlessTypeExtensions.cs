using Microsoft.CodeAnalysis;

namespace eQuantic.UI.Compiler.CodeGen.Extensions;

/// <summary>
/// The types the browser has no name for: declared in no source the build compiles (a referenced
/// project's types are, and become modules, #624) and outside the framework, whose twins the runtime
/// ships. The platform's types are among them, and what of them the browser answers is what a strategy
/// translates. A PACKAGE's types are the rest: no strategy translates any of their members, and a class
/// of one named in a module is a ReferenceError at the page's first render (#822). The call fence, the
/// read fence, a construction and a method group all ask these, so their refusals agree.
/// </summary>
internal static class TwinlessTypeExtensions
{
    /// <summary>Whether the browser has neither a module nor a twin of <paramref name="type"/>. A type the
    /// model could not bind is no type at all, and is EQ2006's where the model is to be trusted.</summary>
    internal static bool IsTwinless(this ITypeSymbol type) =>
        type.TypeKind is not (TypeKind.Error or TypeKind.TypeParameter or TypeKind.Dynamic)
        && !type.Locations.Any(location => location.IsInSource) && !IsFramework(type);

    /// <summary>Whether <paramref name="type"/> is a package's: twinless, and no type of the platform's,
    /// whose members the strategies translate.</summary>
    internal static bool IsPackages(this ITypeSymbol type) => type.IsTwinless() && !BoundaryShape.IsPlatform(type);

    /// <summary>Why a refusal of a package's type has no translation: where the type comes from.</summary>
    internal static string PackageReason(this ITypeSymbol type) =>
        $"'{type.ToDisplayString()}' comes from '{type.ContainingAssembly?.Name}', an assembly the build has no "
        + "source for, and the runtime has no twin of it";

    /// <summary>What a refusal of a package's type offers instead.</summary>
    internal const string PackageRemedy =
        "Use it in a [ServerAction] and hand the browser what it answers, or reference the project it is written "
        + "in, whose types the build compiles.";

    /// <summary>The framework's namespaces, whose twins the runtime ships: the call fence's rule.</summary>
    private static bool IsFramework(ITypeSymbol type)
    {
        var space = type.ContainingNamespace?.ToDisplayString() ?? string.Empty;
        return space == "eQuantic" || space.StartsWith("eQuantic.", StringComparison.Ordinal);
    }
}
