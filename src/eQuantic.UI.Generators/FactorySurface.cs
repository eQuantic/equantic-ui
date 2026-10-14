using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace eQuantic.UI.Generators;

/// <summary>
/// Which of the app's components the declarative surface reaches, and which constructor each factory
/// mirrors.
/// <para>
/// Two generators need the answer. One writes the surface; the hydration manifest's follows a value a
/// page hands to a component through a factory call. Generators do not see each other's output, so
/// where the manifest is written that call binds to nothing, and the parameter the value lands in is
/// found here, by the rule that wrote the factory, rather than by a second copy of it.
/// </para>
/// </summary>
internal static class FactorySurface
{
    private const string FactoryAttribute = "UiFactoryAttribute";
    private const string PageAttribute = "PageAttribute";

    /// <summary>
    /// Whether the surface writes a factory for <paramref name="symbol"/>: a concrete, non-generic
    /// component in a namespace that the generated file reaches and the browser receives. A PAGE is
    /// reached by its route, never composed by hand, so a factory for one would be an offer to do the
    /// wrong thing.
    /// </summary>
    public static bool HasFactory(INamedTypeSymbol symbol) =>
        !symbol.IsAbstract
        && !symbol.IsGenericType
        && Crosses(symbol)
        && !symbol.ContainingNamespace.IsGlobalNamespace
        && IsComponent(symbol)
        && !IsPage(symbol);

    /// <summary>
    /// Whether the component and every type it is declared in are public and none is server-only. A
    /// public component inside a private or an internal owner got a public factory naming a type the
    /// generated file cannot reach, CS0122 or CS0050 in a file the developer never wrote (#703), and
    /// one inside a server-only owner has no module in the browser to build (#584).
    /// </summary>
    private static bool Crosses(INamedTypeSymbol symbol)
    {
        for (var type = symbol; type is not null; type = type.ContainingType)
        {
            if (type.DeclaredAccessibility != Accessibility.Public) return false;
            if (ServerOnlyAlongChain(type)) return false;
        }
        return true;
    }

    /// <summary>
    /// Whether <paramref name="type"/>, or a type it derives from, is marked <c>[ServerOnly]</c>: the
    /// attribute is inherited, and eqc writes no module for a type under it, as it writes none for one
    /// marked itself (found in Copilot's review of #931). By the name as written too, where the attribute
    /// binds to an error type, as eqc reads it.
    /// </summary>
    private static bool ServerOnlyAlongChain(INamedTypeSymbol type)
    {
        for (var at = type; at is not null; at = at.BaseType)
            if (at.GetAttributes().Any(a => a.AttributeClass?.Name is "ServerOnly" or "ServerOnlyAttribute")) return true;
        return false;
    }

    public static bool IsComponent(INamedTypeSymbol symbol)
    {
        for (var b = symbol.BaseType; b is not null; b = b.BaseType)
            if (b.Name is "StatelessComponent" or "StatefulComponent" or "UiComponent")
                return true;
        return false;
    }

    public static bool IsPage(INamedTypeSymbol symbol) =>
        symbol.GetAttributes().Any(a => a.AttributeClass?.Name == PageAttribute);

    /// <summary>
    /// The constructor the factory mirrors: the WIDEST public one, the rule the emitter applies when it
    /// collapses overloads, unless exactly one is elected with <c>[UiFactory]</c>. <paramref name="elected"/>
    /// counts the elected ones, since more than one is an error the surface reports (EQ3101).
    /// </summary>
    public static IMethodSymbol? Elect(INamedTypeSymbol symbol, out int elected)
    {
        var constructors = symbol.InstanceConstructors
            .Where(c => c.DeclaredAccessibility == Accessibility.Public && !c.IsStatic)
            .ToList();
        var marked = constructors
            .Where(c => c.GetAttributes().Any(a => a.AttributeClass?.Name == FactoryAttribute))
            .ToList();
        elected = marked.Count;
        if (constructors.Count == 0) return null;
        return marked.Count == 1 ? marked[0] : constructors.OrderByDescending(c => c.Parameters.Length).First();
    }

    /// <summary>
    /// The factory's own parameters, in order: the constructor's, minus the dependencies the factory
    /// fills from the scope instead of asking its caller for.
    /// </summary>
    public static IReadOnlyList<IParameterSymbol> Parameters(IMethodSymbol constructor) =>
        constructor.Parameters.Where(p => !CapabilityRule.IsDependency(p.Type)).ToList();

    /// <summary>
    /// The component a factory call named <paramref name="name"/> builds, as the surface resolves it:
    /// among the components it writes a factory for, the first by full name, which is the one that wins
    /// a name the surface reports as shared (EQ3102).
    /// </summary>
    public static INamedTypeSymbol? Resolve(Compilation compilation, string name) =>
        compilation.GetSymbolsWithName(name, SymbolFilter.Type)
            .OfType<INamedTypeSymbol>()
            .Where(type => type.Name == name && HasFactory(type))
            .OrderBy(type => type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat), System.StringComparer.Ordinal)
            .FirstOrDefault();
}
