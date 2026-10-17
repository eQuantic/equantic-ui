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
        && !type.Locations.Any(location => location.IsInSource) && !type.IsFramework();

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

    /// <summary>
    /// Whether the SDK ships <paramref name="type"/>'s twin: a type of one of its own assemblies, the
    /// eQuantic.UI family (the vocabulary, the component and chart libraries, the engines, the icon
    /// packs), or one the runtime says it provides (<c>[RuntimeProvided]</c>, or a namespace the runtime
    /// carries). Asked of the ASSEMBLY, never of a namespace that starts like the SDK's: eQuantic's other
    /// packages, the eQuantic.Core family an app may reference, ship no twin, and a namespace prefix took
    /// them for the framework, so their statics, calls and constructions were written as names. The call
    /// fence, the read fence, a construction and a method group all ask this one predicate.
    /// </summary>
    internal static bool IsFramework(this ITypeSymbol type) =>
        type.ContainingAssembly?.Name is { } assembly
            && (assembly == SdkAssembly || assembly.StartsWith(SdkAssembly + ".", StringComparison.Ordinal))
        || type is INamedTypeSymbol named && named.IsRuntimeProvided();

    /// <summary>The name the SDK's assemblies share, as the packages that carry them are named.</summary>
    private const string SdkAssembly = "eQuantic.UI";
}
