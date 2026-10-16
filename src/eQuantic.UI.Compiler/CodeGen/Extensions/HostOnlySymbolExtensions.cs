using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

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
/// difficulty. There are SEVEN: a qualified call, a static member READ, an unqualified call through
/// <c>using static</c>, a method GROUP passed as a delegate, a CONSTRUCTION, an OPERATOR, and an
/// EXTENSION reached on its receiver, its call or its group, which names its home
/// (<c>InvocationStrategy.ExtensionHome</c>). Each returns early on its own path, so each had to be
/// told, and every version of this fence has guarded some of them while reading like protection
/// for all. <c>ComponentBoundary.Contained</c> compiled while <c>ComponentBoundary.ClearContained()</c>
/// did not; <c>using static</c> compiled while a qualified call did not; <c>Matrix2D.Identity</c> was
/// stopped while <c>new Matrix2D(…)</c> two lines above it was not; and <c>curve.Ease(t)</c> compiled
/// while <c>CurveEvaluator.Ease(curve, t)</c> did not, because the receiver crosses (#518).
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
/// <para>
/// The same question covers what .NET keeps on the host (<see cref="ServerPlatform"/>): a member of
/// <c>System.IO</c>, of the networking, threading and interop namespaces, is refused at the site that
/// writes it, with the code that names its API (EQ2102–EQ2107, EQ2112). So what is checked is what the
/// module holds, stateless and stateful components and every class a page reaches alike, and a
/// <c>[ServerAction]</c>'s body or a <c>[ServerOnly]</c> member, which no module holds, is never
/// asked (#816).
/// </para>
/// <para>
/// A TYPE of source answers by one rule wherever it is declared, the app or a project it references:
/// the build writes no module for it when it, or a type it is declared in, never crosses
/// (<see cref="ReportIfKeptOut"/>), asked where client code writes its name. The app's own top-level
/// <c>[ServerOnly]</c> class was the case left out: the fence said nothing of a type this compilation
/// declares, so <c>Vault.Key()</c> was written as a call on a class nothing wrote (#823).
/// </para>
/// <para>
/// A <c>[ServerOnly]</c> method reaches no twin, whatever type declares it, and a call can reach one
/// without naming it: through the interface member it implements, or the method it overrides, which
/// carry no attribute. <c>((IShape)new Circle()).Area()</c> built, and the browser threw a TypeError
/// where .NET answered. Such a call is refused where it is written, naming the method
/// (<see cref="ServerOnlyContracts"/>), and a call that can reach no such method is not.
/// </para>
/// </summary>
internal static class HostOnlySymbolExtensions
{
    /// <summary>
    /// Carries <c>[ServerOnly]</c> itself — a type, a single member, or something it OVERRIDES.
    /// <para>
    /// The override walk is not a convenience. A call resolves to the symbol on the RECEIVER's
    /// type, so `node.Accept(…)` on a `Text` binds to `Text.Accept` and never sees the attribute on
    /// `VisualNode.Accept` — the fence would have to be repeated on all 39 implementations, and the
    /// fortieth would forget. One declaration, and the overrides inherit it.
    /// </para>
    /// </summary>
    internal static bool IsHostOnly(this ISymbol symbol)
    {
        for (var current = symbol; current is not null; current = Overridden(current))
            if (current.GetAttributes().Any(a => a.AttributeClass?.Name == "ServerOnlyAttribute"))
                return true;
        return false;

        // ORIGINAL DEFINITION first. At a call site a generic method is the CONSTRUCTED symbol
        // (`Accept<int, int>`), and a constructed method overrides nothing — `OverriddenMethod` is
        // null there, so the walk stopped at the first step and the fence passed. Measured: with a
        // `VisualNode` receiver it reported, with a `Text` receiver it did not, and the only
        // difference is which symbol the call binds to.
        static ISymbol? Overridden(ISymbol symbol) => symbol.OriginalDefinition switch
        {
            IMethodSymbol method => method.OverriddenMethod,
            IPropertySymbol property => property.OverriddenProperty,
            IEventSymbol @event => @event.OverriddenEvent,
            _ => null,
        };
    }

    /// <summary>
    /// Reports and returns true when <paramref name="symbol"/> may not be named from client code: it
    /// is marked <c>[ServerOnly]</c> itself, a call through it may reach a <c>[ServerOnly]</c> method
    /// (<see cref="ServerOnlyContracts"/>), it belongs to an API only a server has
    /// (<see cref="ServerPlatform"/>), or the metadata type it is declared on is marked
    /// <c>[ServerOnly]</c>. The type of a symbol declared in source is the kept-out rule's
    /// (<see cref="ReportIfKeptOut"/>), asked where the code names it, so one site says it once.
    /// <para>
    /// <paramref name="through"/> is the type the member was reached THROUGH, where the call site
    /// has a receiver to ask about. A host-only type fences the members a component could only
    /// reach by naming it — <c>FaceResolution.Unresolved</c> — and that is the whole of what the
    /// type-level test meant while every host-only type was a sealed utility. A host-only BASE is
    /// the case it did not foresee: <c>SingleChildNode</c> is fenced because the runtime exports no
    /// twin for the TYPE, while the <c>Child</c> it declares is inherited by twenty nodes that are
    /// client-visible, each with a twin carrying that child. Fencing the type there fenced
    /// <c>pressable.Child</c> too, which compiled on the release before and names nothing the
    /// runtime lacks — measured, not reasoned: the probe reported
    /// <c>'…SingleChildNode.Child' is HOST ONLY</c> and told the reader to move an ordinary read
    /// into a <c>[ServerAction]</c>.
    /// </para>
    /// <para>
    /// So the type-level test asks where the member was reached from. A member carrying the
    /// attribute ITSELF is unaffected and still reports through any receiver — that is
    /// <c>VisualNode.Accept</c> on a <c>Text</c>, and it is checked FIRST here for that reason.
    /// </para>
    /// </summary>
    internal static bool ReportIfHostOnly(this ISymbol symbol, SyntaxNode node, ConversionContext context,
        ITypeSymbol? through = null)
    {
        var declaring = symbol.ContainingType;
        if (declaring is null) return false;
        // The MEMBER first, whoever declares it. A member marked [ServerOnly] is refused where
        // client code names it, this compilation's own included: a twin leaves such a method out,
        // so a call to it from the component's own Build was written as a call to a member the twin
        // does not have, with no diagnostic (found in review of #827).
        if (symbol.IsHostOnly()) return Report(declaring, symbol.Name, node, context);
        // A contract such a member fulfils: the call binds to a member that carries no attribute, and
        // may reach, in the browser, the one the twin leaves out (#816).
        if (symbol.ReportIfFulfilledOnTheServer(node, context, through)) return true;
        // What .NET keeps on the host, whatever the call goes through (#816).
        if (declaring.ReportIfServerPlatform(node, context)) return true;
        // A type of source, this compilation's or a referenced project's, is refused where its name
        // is written (ReportIfKeptOut), never here as well: a page that named a referenced project's
        // [ServerOnly] class was told twice, once at the class and once at its member (#823).
        if (declaring.Locations.Any(location => location.IsInSource)) return false;
        if (!declaring.IsHostOnly()) return false;
        if (ReachedThroughAClientVisibleType(through)) return false;
        return Report(declaring, symbol.Name, node, context);
    }

    /// <summary>
    /// The member was read off something whose own type is not fenced — so the component named a
    /// node, not the shape behind it. A null receiver type (a static access, or a model that cannot
    /// answer) keeps the fence exactly as it was.
    /// <para>
    /// It does NOT also test that the receiver differs from the fenced type, which the first version
    /// did: a receiver that IS the fenced type is host-only, so the second clause already decided
    /// every case the first could have. Measured by removing it — both tests stayed green, which is
    /// the definition of a branch that never decides.
    /// </para>
    /// </summary>
    private static bool ReachedThroughAClientVisibleType(ITypeSymbol? through) =>
        through is not null && !through.IsHostOnly();

    /// <summary>
    /// Reports and returns true when a call bound to <paramref name="symbol"/>, an interface member or a
    /// method that can be overridden, may reach a <c>[ServerOnly]</c> method of a type that crosses,
    /// which that type's twin leaves out (<see cref="ServerOnlyContracts"/>). The call is made on
    /// <paramref name="through"/> where it has a receiver, and on the type its code is declared in where
    /// it is bare. A call of <c>base</c> dispatches to nothing: it runs the base's own method, which the
    /// base's twin holds.
    /// </summary>
    private static bool ReportIfFulfilledOnTheServer(this ISymbol symbol, SyntaxNode node, ConversionContext context,
        ITypeSymbol? through)
    {
        if (symbol is not IMethodSymbol contract || CallsTheBase(node)) return false;
        if ((context.ProjectCompilation ?? context.SemanticModel?.Compilation) is not { } build) return false;
        var methods = ServerOnlyContracts.FulfilledOnTheServer(contract, () => through ?? TypeOfThis(node, context), build);
        if (methods.Count == 0) return false;
        // At the call as written: a `?.` call reaches here rewritten, at no place of the file.
        context.Report(context.SemanticHelper.Original(node), ConversionSeverity.Error, "EQ2010",
            FulfilledOnTheServerMessage($"{contract.ContainingType.ToDisplayString()}.{contract.Name}", methods));
        return true;
    }

    /// <summary>Whether the call, or the method group, is made on <c>base</c>.</summary>
    private static bool CallsTheBase(SyntaxNode node) => node switch
    {
        InvocationExpressionSyntax { Expression: MemberAccessExpressionSyntax { Expression: BaseExpressionSyntax } } => true,
        MemberAccessExpressionSyntax { Expression: BaseExpressionSyntax } => true,
        _ => false,
    };

    /// <summary>The type a bare call, or a bare method group, is made on: the one its code is declared
    /// in, which <c>this</c> is. Null for any other node, and where the model does not hold its file.</summary>
    private static ITypeSymbol? TypeOfThis(SyntaxNode node, ConversionContext context)
    {
        if (node is not (InvocationExpressionSyntax { Expression: SimpleNameSyntax } or SimpleNameSyntax)) return null;
        var declaration = context.SemanticHelper.Original(node).Ancestors().OfType<TypeDeclarationSyntax>().FirstOrDefault();
        return declaration is not null && context.SemanticModel is { } model && model.SyntaxTree == declaration.SyntaxTree
            ? model.GetDeclaredSymbol(declaration) as ITypeSymbol
            : null;
    }

    /// <summary>What EQ2010 says of a call through a contract a <c>[ServerOnly]</c> method fulfils: the
    /// method, and why a call through the contract cannot reach it.</summary>
    private static string FulfilledOnTheServerMessage(string contract, IReadOnlyList<string> methods)
    {
        var named = string.Join(" and ", methods.Select(method => $"'{method}'"));
        return $"'{contract}' may reach {named}, marked [ServerOnly], which no module holds: a call through "
            + "the contract would reach a member the browser does not have. Call it from server code, a "
            + $"[ServerAction] or a [ServerOnly] member, or take [ServerOnly] off {named} so it crosses.";
    }

    /// <summary>
    /// The TYPE named on its own — a construction, where there is no member to ask about. Same
    /// rule, same message, through the same function: the branch that reported it inline for one
    /// release is how two pieces come to answer the same question each in its own words. A type of
    /// source is the kept-out rule's, which the construction asks first (<see cref="ReportIfKeptOut"/>).
    /// </summary>
    internal static bool ReportIfHostOnlyType(this ITypeSymbol? type, SyntaxNode node, ConversionContext context)
    {
        if (type is not INamedTypeSymbol named) return false;
        if (named.ReportIfServerPlatform(node, context)) return true;
        if (named.Locations.Any(location => location.IsInSource)) return false;
        if (!named.IsHostOnly()) return false;
        return Report(named, node, context);
    }

    private static bool Report(INamedTypeSymbol declaring, SyntaxNode node, ConversionContext context) =>
        Report(declaring, member: null, node, context);

    /// <summary>
    /// A type of source the build writes no twin for, named from client code: it, or a class it
    /// derives from, is marked <c>[ServerOnly]</c> or is an attribute, or it is declared inside a type
    /// that is (<see cref="Services.PlainClassModule.NeverCrosses"/>). The reference named a twin
    /// nothing wrote, <c>Vault$Key</c> for a nested one (#584) and <c>Vault</c> for the app's own
    /// top-level class (#823), which the page met at load.
    /// <para>
    /// Whichever compilation declares it: a type of a project the app references is in source too
    /// (#624), and the type-level fence that refused it while it was read from its assembly said
    /// nothing once it had a source.
    /// </para>
    /// <para>
    /// A component crosses whatever it is marked, where it is declared at the top of its file: the
    /// parser writes a module for every one of those, as <c>[ServerOnly]</c>'s own doc says, so a
    /// page that places it names a twin that exists.
    /// </para>
    /// </summary>
    internal static bool ReportIfKeptOut(this INamedTypeSymbol type, SyntaxNode node, ConversionContext context)
    {
        if (!type.Locations.Any(location => location.IsInSource)) return false;
        if (!type.IsKeptOut()) return false;
        context.Report(node, ConversionSeverity.Error, "EQ2010", KeptOutMessage(type.ToDisplayString()));
        context.RefusedTwins.Add(type.TwinReference());
        return true;
    }

    /// <summary>
    /// Whether the build writes no module for <paramref name="type"/>, a type of source: it, or a class
    /// it derives from, is marked <c>[ServerOnly]</c> or is an attribute, or it is declared inside a type
    /// that is (<see cref="Services.PlainClassModule.NeverCrosses"/>). A component declared at the top of
    /// its file crosses whatever it is marked. The rule a fence asks where client code names the type,
    /// and the one that says which types' <c>[ServerOnly]</c> methods a call can reach
    /// (<see cref="ServerOnlyContracts"/>).
    /// </summary>
    internal static bool IsKeptOut(this INamedTypeSymbol type) =>
        !(type.ContainingType is null && type.IsUiComponent()) && Services.PlainClassModule.NeverCrosses(type);

    /// <summary>What EQ2010 says of a type of source the build writes no twin for (#584, #823): where
    /// a fence names it, and where a module's import net does.</summary>
    internal static string KeptOutMessage(string named) =>
        $"'{named}' never reaches the browser: it, or a type it is declared in, is [ServerOnly] "
        + "or an attribute, which the build writes no module for. "
        + "Use it from server code — a [ServerAction] or a [ServerOnly] class — never from a component's Build.";

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
        context.Report(node, ConversionSeverity.Error, "EQ2010", Message(named));
        return true;
    }

    /// <summary>
    /// The one wording. A THIRD caller reports this — the emitter, for a host-only type named in a
    /// SIGNATURE rather than in an expression — and it has no ConversionContext to go through, so
    /// the message is what the three share rather than the function. The alternative was a second
    /// text, which is how two pieces come to answer one question each in its own words.
    /// </summary>
    internal static string Message(string named) =>
        $"'{named}' is HOST ONLY ([ServerOnly]) and the "
        + "runtime ships no twin for it, so a client component naming it would fail at hydration "
        + "rather than here. Call it from server code — a [ServerAction], a [ServerOnly] class, "
        + "or the realizer — never from a component's Build.";
}
