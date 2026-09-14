using Microsoft.CodeAnalysis;

namespace eQuantic.UI.Compiler.CodeGen.Extensions;

/// <summary>
/// The HOST-ONLY fence, as one question asked from every place a client can name a symbol.
///
/// <para>
/// eqc routes the whole <c>eQuantic.UI.Primitives</c> namespace to <c>@equantic/runtime</c> without
/// an attribute per type — that is what lets the shared vocabulary work — and the routing is a
/// NAMESPACE test. So a framework symbol the runtime deliberately ships no twin for is routed as
/// readily as the rest: the reference compiles, emits, and dies at hydration on "does not provide an
/// export named", while SSR keeps answering 200 with correct markup. <c>RouteValues</c> shipped that
/// way and took a page down.
/// </para>
///
/// <para>
/// TWO levels, because the fence is needed at both and one without the other is a hole. A TYPE
/// marked <c>[ServerOnly]</c> never crosses at all (<c>FaceResolution</c>, <c>ComponentBoundary</c>).
/// A METHOD marked <c>[ServerOnly]</c> on a type that DOES cross fences just itself
/// (<c>FaceName.Usable</c>, beside a <c>FaceName.IsWellFormed</c> the runtime does export) — and a
/// type-only check accepts the containing type as provided and waves the member through.
/// </para>
///
/// <para>
/// One function, asked from EVERY branch that can return a name — and counting them is the whole
/// difficulty. There are SIX: a qualified call, a static member READ, an unqualified call through
/// <c>using static</c>, a method GROUP passed as a delegate, a CONSTRUCTION, and an OPERATOR. Each
/// returns early on its own path, so each had to be told, and every version of this fence has
/// guarded some of them while reading like protection for all.
/// <c>ComponentBoundary.Contained</c> compiled while <c>ComponentBoundary.ClearContained()</c> did
/// not; <c>using static</c> compiled while a qualified call did not; <c>Matrix2D.Identity</c> was
/// stopped while <c>new Matrix2D(…)</c> two lines above it was not.
/// </para>
/// <para>
/// The OPERATOR branch is the one whose absence failed SILENTLY rather than loudly, and it is worth
/// stating separately for that reason. JavaScript cannot overload an operator, so an unfenced
/// framework <c>+</c> emits JavaScript's own: two objects concatenate into
/// <c>"[object Object][object Object]"</c> and <c>a * 2</c> is <c>NaN</c>, in a page that compiled,
/// rendered on the server and looked right. Every other branch at least took the page down.
/// </para>
/// <para>
/// A new branch that returns a name owes this call.
/// </para>
/// </summary>
internal static class HostOnlySymbolExtensions
{
    /// <summary>Carries <c>[ServerOnly]</c> itself — a type or a single member.</summary>
    internal static bool IsHostOnly(this ISymbol symbol) =>
        symbol.GetAttributes().Any(attribute => attribute.AttributeClass?.Name == "ServerOnlyAttribute");

    /// <summary>
    /// Reports and returns true when <paramref name="symbol"/> may not be named from client code.
    /// Says nothing about a symbol declared in THIS compilation: an app's own <c>[ServerOnly]</c>
    /// member is handled by the parser, which emits no module for it at all.
    /// </summary>
    internal static bool ReportIfHostOnly(this ISymbol symbol, SyntaxNode node, ConversionContext context)
    {
        var declaring = symbol.ContainingType;
        if (declaring is null) return false;
        if (declaring.Locations.Any(location => location.IsInSource)) return false;
        if (!symbol.IsHostOnly() && !declaring.IsHostOnly()) return false;
        return Report(declaring, symbol.Name, node, context);
    }

    /// <summary>
    /// The TYPE named on its own — a construction, where there is no member to ask about. Same
    /// rule, same message, through the same function: the branch that reported it inline for one
    /// release is how two pieces come to answer the same question each in its own words.
    /// </summary>
    internal static bool ReportIfHostOnlyType(this ITypeSymbol? type, SyntaxNode node, ConversionContext context)
    {
        if (type is not INamedTypeSymbol named) return false;
        if (named.Locations.Any(location => location.IsInSource)) return false;
        if (!named.IsHostOnly()) return false;
        return Report(named, node, context);
    }

    private static bool Report(INamedTypeSymbol declaring, SyntaxNode node, ConversionContext context) =>
        Report(declaring, member: null, node, context);

    private static bool Report(
        INamedTypeSymbol declaring, string? member, SyntaxNode node, ConversionContext context)
    {
        var named = member is null
            ? declaring.ToDisplayString()
            : $"{declaring.ToDisplayString()}.{member}";

        // Its OWN code, not EQ2004. The two look alike — neither symbol has a translation — and
        // they are not the same error: EQ2004 is an OMISSION, fixed by adding a strategy, while
        // this is a DECISION, and a reader who follows EQ2004's remedy here writes code nobody
        // wants. One code meaning two unrelated things is how EQ2101 went wrong, and the
        // diagnostics baseline asked the question directly when this gained a reporting site.
        context.Report(node, ConversionSeverity.Error, "EQ2010",
            $"'{named}' is HOST ONLY ([ServerOnly]) and the "
            + "runtime ships no twin for it, so a client component naming it would fail at hydration "
            + "rather than here. Call it from server code — a [ServerAction], a [ServerOnly] class, "
            + "or the realizer — never from a component's Build.");
        return true;
    }
}
