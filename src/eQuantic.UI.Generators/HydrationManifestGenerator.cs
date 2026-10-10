using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace eQuantic.UI.Generators;

/// <summary>
/// Writes the hydration manifest: one <c>[assembly: HydratedMember(...)]</c> per value a component
/// carries from its server render to the browser.
/// <para>
/// The server used to decide this for itself, by reflection, at run time. It named a value after the
/// field the C# compiler synthesized and kept whatever was not an interface, while eqc named the twin's
/// members by its own rule, so an auto-property crossed as <c>Downloads</c> into a twin that declares
/// <c>downloads</c> and never landed, and a captured primary-constructor parameter crossed as
/// <c>&lt;identity&gt;P</c>. The build knows every member's symbol, so the description is written here,
/// once, and the server and eqc both read it.
/// </para>
/// <para>
/// A type is described when its state can cross: when it prefetches, whatever it derives from (a
/// write-once component or an escape-hatch page alike). Every member it and its app-declared bases hold
/// crosses, except a delegate (the browser builds its own handlers) and a dependency the browser
/// resolves for itself (<see cref="CapabilityRule"/>, the rule the factory surface and eqc already
/// share). A type is named by its metadata name, so a private nested component is described too.
/// </para>
/// <para>
/// A PAGE is described as well when the server's container hands it a value the browser cannot build
/// (<see cref="ServerValueAnalysis"/>): that value crosses as its projection, what the browser-side code
/// reads of it, whether the page prefetches or not, and a use the projection cannot follow fails the
/// build with EQ2114. A page is a type with <c>[Page]</c>, or one an app routes with <c>MapPage&lt;T&gt;</c>.
/// </para>
/// </summary>
[Generator]
public sealed class HydrationManifestGenerator : IIncrementalGenerator
{
    private const string PrefetchInterface = "eQuantic.UI.Primitives.IServerPrefetch";
    private const string RouteExtensions = "eQuantic.UI.Server.UIExtensions";
    private const string WebComponent = "eQuantic.UI.Web.IComponent";
    private const string Attribute = "global::eQuantic.UI.Primitives.HydratedMember";
    private const string Kind = "global::eQuantic.UI.Primitives.HydratedMemberKind";

    internal static readonly DiagnosticDescriptor ServerValueEscapes = new(
        "EQ2114", "A server value is used in a way the browser cannot follow",
        "'{0}' uses '{1}', which only the server has, at '{2}': {3}. The browser builds the page without "
        + "it, and the build sends only what it can see the browser read of it. Decide on the server, in "
        + "PrefetchAsync or a [ServerOnly] member, and keep the result in a field; or call a [ServerAction] "
        + "when the browser's state is an input.",
        "eQuantic.UI", DiagnosticSeverity.Error, isEnabledByDefault: true);

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var components = context.SyntaxProvider.CreateSyntaxProvider(
                predicate: static (node, _) => node is ClassDeclarationSyntax { BaseList: not null },
                transform: static (ctx, token) => Describe(ctx, token))
            .Where(static component => component is not null)
            .Collect();

        // A page routed with `app.MapPage<T>(…)` carries no attribute, and the container builds it all
        // the same.
        var routed = context.SyntaxProvider.CreateSyntaxProvider(
                predicate: static (node, _) => node is GenericNameSyntax
                {
                    Identifier.ValueText: "MapPage", TypeArgumentList.Arguments.Count: 1,
                },
                transform: static (ctx, token) => Routed(ctx, token))
            .Where(static page => page is not null)
            .Collect();

        context.RegisterSourceOutput(components.Combine(routed),
            static (spc, pair) => Emit(spc, pair.Left, pair.Right));
    }

    /// <summary>One crossing value: the component, the type declaring the member, the member, its kind, its projection.</summary>
    private sealed class Entry
    {
        public Entry(string component, string declaringType, string member, string kind, string? projection = null)
        {
            Component = component; DeclaringType = declaringType; Member = member; Kind = kind; Projection = projection;
        }

        public string Component { get; }
        public string DeclaringType { get; }
        public string Member { get; }
        public string Kind { get; }
        /// <summary>Null when the value crosses whole.</summary>
        public string? Projection { get; }
    }

    /// <summary>
    /// What one class carries: as any component, and as a page. Whether it IS a page is known only once
    /// every <c>MapPage&lt;T&gt;</c> of the app has been read, so both answers are kept until then.
    /// </summary>
    private sealed class Described
    {
        public Described(string component, bool isPage, IReadOnlyList<Entry> entries,
            IReadOnlyList<Entry> projected, IReadOnlyCollection<(string Declaring, string Member)> server,
            IReadOnlyList<BoundaryStop> stops)
        {
            Component = component; IsPage = isPage; Entries = entries;
            Projected = projected; Server = server; Stops = stops;
        }

        public string Component { get; }
        public bool IsPage { get; }
        /// <summary>What crosses whole, when it prefetches.</summary>
        public IReadOnlyList<Entry> Entries { get; }
        /// <summary>As a page: each server value the browser reads, with its projection.</summary>
        public IReadOnlyList<Entry> Projected { get; }
        /// <summary>As a page: the members holding a server value, which never cross whole.</summary>
        public IReadOnlyCollection<(string Declaring, string Member)> Server { get; }
        /// <summary>As a page: the uses the projection cannot follow.</summary>
        public IReadOnlyList<BoundaryStop> Stops { get; }
    }

    private static string? Routed(GeneratorSyntaxContext ctx, System.Threading.CancellationToken token) =>
        ctx.SemanticModel.GetSymbolInfo(ctx.Node, token).Symbol is IMethodSymbol { TypeArguments.Length: 1 } route
        && route.ContainingType?.ToDisplayString() == RouteExtensions
        && route.TypeArguments[0] is INamedTypeSymbol page
            ? MetadataName(page)
            : null;

    private static void Emit(SourceProductionContext spc, ImmutableArray<Described?> all, ImmutableArray<string?> routed)
    {
        var pages = new HashSet<string>(routed.Where(name => name is not null)!, System.StringComparer.Ordinal);
        var reported = new HashSet<string>(System.StringComparer.Ordinal);
        var described = new List<Entry>();
        foreach (var component in all.Where(c => c is not null).Select(c => c!))
        {
            if (!component.IsPage && !pages.Contains(component.Component))
            {
                described.AddRange(component.Entries);
                continue;
            }

            described.AddRange(component.Entries.Where(e => !component.Server.Contains((e.DeclaringType, e.Member))));
            described.AddRange(component.Projected);
            // A partial class is met once per declaration, and each met the same stops.
            foreach (var stop in component.Stops)
                if (reported.Add($"{stop.Location.SourceTree?.FilePath}:{stop.Location.SourceSpan}:{stop.Reason}"))
                    spc.ReportDiagnostic(Diagnostic.Create(ServerValueEscapes, stop.Location,
                        stop.Page, stop.Value, stop.Expression, stop.Reason));
        }

        // A partial class is met once per declaration, so the same entries arrive more than once.
        var entries = described
            .GroupBy(e => (e.Component, e.DeclaringType, e.Member, e.Kind))
            .Select(g => g.First())
            .OrderBy(e => e.Component, System.StringComparer.Ordinal)
            .ThenBy(e => e.DeclaringType, System.StringComparer.Ordinal)
            .ThenBy(e => e.Member, System.StringComparer.Ordinal)
            .ToList();
        if (entries.Count == 0) return;

        var source = new StringBuilder();
        source.AppendLine("// <auto-generated/>");
        source.AppendLine("// What each component carries from its server render to the browser. The server writes a page's");
        source.AppendLine("// state from these entries alone, and eqc writes each twin's adoption from them.");
        foreach (var entry in entries)
            source.AppendLine(
                $"[assembly: {Attribute}(\"{entry.Component}\", \"{entry.DeclaringType}\", "
                + $"\"{entry.Member}\", {Kind}.{entry.Kind}"
                + (entry.Projection is null ? "" : $", Projection = \"{entry.Projection}\"")
                + ")]");

        spc.AddSource("HydrationManifest.g.cs", source.ToString());
    }

    private static Described? Describe(GeneratorSyntaxContext ctx, System.Threading.CancellationToken token)
    {
        if (ctx.SemanticModel.GetDeclaredSymbol(ctx.Node, token) is not INamedTypeSymbol symbol) return null;
        if (symbol.IsAbstract || symbol.IsStatic) return null;

        var prefetches = Prefetches(symbol);
        // Read only where it can matter: a type that can be a page, whose constructor takes a value the
        // browser cannot build. A service that takes one is no page, and reading it would be work thrown away.
        var servers = CanBePage(symbol) && symbol.InstanceConstructors
            .Any(c => !c.IsImplicitlyDeclared && c.Parameters.Any(p => ServerValueAnalysis.IsServerValue(p.Type)))
            ? ServerValueAnalysis.Of(symbol, ctx.SemanticModel.Compilation, token)
            : null;
        if (!prefetches && servers is null) return null;

        var component = MetadataName(symbol);
        var entries = prefetches ? Crossing(symbol, component, ctx.SemanticModel.Compilation, token) : new List<Entry>();
        var projected = new List<Entry>();
        var server = new HashSet<(string Declaring, string Member)>();
        if (servers is not null)
        {
            foreach (var member in servers.Storage)
                server.Add((MetadataName(member is IParameterSymbol parameter ? parameter.ContainingType : member.ContainingType), member.Name));
            foreach (var pair in servers.Projections)
                projected.Add(new Entry(component,
                    MetadataName(pair.Key is IParameterSymbol parameter ? parameter.ContainingType : pair.Key.ContainingType),
                    pair.Key.Name, KindOf(pair.Key), pair.Value.ToString()));
        }

        return new Described(component, FactorySurface.IsPage(symbol), entries, projected, server,
            servers?.Stops ?? (IReadOnlyList<BoundaryStop>)new List<BoundaryStop>());
    }

    /// <summary>What <c>MapPage&lt;T&gt;</c> accepts, and what carries <c>[Page]</c>: a component of either kind.</summary>
    private static bool CanBePage(INamedTypeSymbol symbol) =>
        FactorySurface.IsPage(symbol)
        || FactorySurface.IsComponent(symbol)
        || symbol.AllInterfaces.Any(i => i.ToDisplayString() == WebComponent);

    private static string KindOf(ISymbol storage) => storage switch
    {
        IParameterSymbol => "CapturedParameter",
        IPropertySymbol property when KeepsItsOwnStore(property) => "BackingField",
        IPropertySymbol => "Property",
        _ => "Field",
    };

    /// <summary>
    /// Whether a property keeps its store through C#'s <c>field</c>: it has one the compiler declares, as
    /// an auto-property does, and accessors of its own that read or write it.
    /// </summary>
    private static bool KeepsItsOwnStore(IPropertySymbol property) =>
        property.ContainingType.GetMembers().OfType<IFieldSymbol>()
            .Any(field => field.IsImplicitlyDeclared && SymbolEqualityComparer.Default.Equals(field.AssociatedSymbol, property))
        && property.DeclaringSyntaxReferences
            .Select(reference => reference.GetSyntax())
            .OfType<PropertyDeclarationSyntax>()
            .Any(declaration => declaration.ExpressionBody is not null
                || declaration.AccessorList?.Accessors.Any(a => a.Body is not null || a.ExpressionBody is not null) == true);

    /// <summary>Every member a prefetching component holds, which crosses whole.</summary>
    private static List<Entry> Crossing(
        INamedTypeSymbol symbol, string component, Compilation compilation, System.Threading.CancellationToken token)
    {
        var entries = new List<Entry>();
        // The app's own types are described; a framework base's fields belong to the framework, and a
        // referenced assembly's private members are not even visible to this compilation.
        for (var type = symbol; type is not null && type.IsDeclaredIn(compilation); type = type.BaseType)
        {
            var declaring = MetadataName(type);
            foreach (var member in type.GetMembers())
            {
                token.ThrowIfCancellationRequested();
                if (member is not IFieldSymbol { IsStatic: false, IsConst: false } field) continue;
                if (Excluded(field.Type)) continue;

                // An auto-property's storage is a field the compiler declares for it: the property is
                // what crosses, read through its getter. One whose accessors guard that store through
                // `field` crosses as the store, since its setter need not give back what its getter read.
                if (field.IsImplicitlyDeclared)
                {
                    if (field.AssociatedSymbol is IPropertySymbol { IsStatic: false } property)
                        entries.Add(new Entry(component, declaring, property.Name, KindOf(property)));
                    continue;
                }

                entries.Add(new Entry(component, declaring, field.Name, "Field"));
            }

            foreach (var parameter in CapturedParameters(type, compilation, token))
                if (!Excluded(parameter.Type))
                    entries.Add(new Entry(component, declaring, parameter.Name, "CapturedParameter"));
        }

        return entries;
    }

    /// <summary>A delegate never crosses, and neither does a dependency the browser resolves itself.</summary>
    private static bool Excluded(ITypeSymbol type) =>
        type.TypeKind == TypeKind.Delegate || CapabilityRule.IsDependency(type);

    /// <summary>
    /// The primary-constructor parameters the class captures, which is what makes the C# compiler give
    /// one a field: a reference from a member's body. A reference from a field or property initializer,
    /// or from the base type's arguments, runs during construction and is not a capture.
    /// </summary>
    private static IEnumerable<IParameterSymbol> CapturedParameters(
        INamedTypeSymbol type, Compilation compilation, System.Threading.CancellationToken token)
    {
        var declarations = type.SourceIn(compilation)
            .Select(reference => reference.GetSyntax(token))
            .OfType<TypeDeclarationSyntax>()
            .ToList();
        var primary = declarations.FirstOrDefault(d => d.ParameterList is not null);
        if (primary is null) return Enumerable.Empty<IParameterSymbol>();

        var model = compilation.GetSemanticModel(primary.SyntaxTree);
        var parameters = primary.ParameterList!.Parameters
            .Select(p => model.GetDeclaredSymbol(p, token))
            .OfType<IParameterSymbol>()
            .ToList();
        if (parameters.Count == 0) return Enumerable.Empty<IParameterSymbol>();

        var captured = new HashSet<IParameterSymbol>(SymbolEqualityComparer.Default);
        foreach (var declaration in declarations)
        {
            var declarationModel = compilation.GetSemanticModel(declaration.SyntaxTree);
            foreach (var identifier in declaration.DescendantNodes().OfType<IdentifierNameSyntax>())
            {
                if (!parameters.Any(p => p.Name == identifier.Identifier.ValueText)) continue;
                if (declarationModel.GetSymbolInfo(identifier, token).Symbol is not IParameterSymbol bound) continue;
                if (!parameters.Contains(bound, SymbolEqualityComparer.Default)) continue;
                if (RunsDuringConstruction(identifier)) continue;
                captured.Add(bound);
            }
        }

        return parameters.Where(p => captured.Contains(p));
    }

    /// <summary>Whether a reference sits where the constructor evaluates it: an initializer, or the base's arguments.</summary>
    private static bool RunsDuringConstruction(SyntaxNode reference)
    {
        foreach (var ancestor in reference.Ancestors())
        {
            switch (ancestor)
            {
                case BaseListSyntax:
                case FieldDeclarationSyntax:
                    return true;
                case PropertyDeclarationSyntax property:
                    return property.Initializer is { } initializer && initializer.Span.Contains(reference.Span);
                case MemberDeclarationSyntax:
                    return false;
            }
        }
        return false;
    }

    private static bool Prefetches(INamedTypeSymbol symbol) =>
        symbol.AllInterfaces.Any(i => i.ToDisplayString() == PrefetchInterface);

    /// <summary>
    /// The name the runtime gives the type's definition (<see cref="System.Type.FullName"/>): its
    /// namespace, its containing types joined by <c>+</c>, and the arity a generic one carries
    /// (<c>Shop.Grid`1</c>).
    /// </summary>
    private static string MetadataName(INamedTypeSymbol type)
    {
        var name = type.MetadataName;
        for (var outer = type.ContainingType; outer is not null; outer = outer.ContainingType)
            name = outer.MetadataName + "+" + name;
        var space = type.ContainingNamespace;
        return space is null || space.IsGlobalNamespace ? name : space.ToDisplayString() + "." + name;
    }
}
