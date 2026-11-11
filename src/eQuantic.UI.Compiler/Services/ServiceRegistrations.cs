using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace eQuantic.UI.Compiler.Services;

/// <summary>
/// What the app registered in its container, read from its source: the registrations the browser
/// repeats in its own container, so a component that takes an app's service through its constructor
/// gets one there as it does on the server and on Photon (#1335).
/// <para>
/// The developer writes the ordinary .NET registration, <c>builder.Services.AddSingleton&lt;IWalletLedger,
/// WalletLedger&gt;()</c>, once, in <c>Program.cs</c>. The server's container hands the service out from
/// it, and this reads the same line for the browser. Nothing crosses because it is registered: a
/// registration crosses where browser code RESOLVES its service (<see cref="CodeGen.BrowserServices"/>),
/// so an app's server-only services (a repository over a database, a mail sender) are never compiled for
/// the browser.
/// </para>
/// <para>
/// Only an app's OWN interfaces, declared in the app's source or a referenced project's. The SDK's
/// capabilities (<c>IClock</c>, <c>ICamera</c>) are the runtime's to provide in the browser, and an
/// interface from a package names nothing the browser could build.
/// </para>
/// </summary>
internal sealed class ServiceRegistrations
{
    /// <summary>A build that read no registration: a source compiled alone, the playground's.</summary>
    public static readonly ServiceRegistrations None = new(new Dictionary<string, List<ServiceRegistration>>(), _ => false);

    private const string ServiceCollectionExtensions = "Microsoft.Extensions.DependencyInjection.ServiceCollectionServiceExtensions";
    private const string DescriptorExtensions = "Microsoft.Extensions.DependencyInjection.Extensions.ServiceCollectionDescriptorExtensions";

    private readonly Dictionary<string, List<ServiceRegistration>> _byKey;
    private readonly Func<string, bool> _isSourceOfTheBuild;

    private ServiceRegistrations(Dictionary<string, List<ServiceRegistration>> byKey, Func<string, bool> isSourceOfTheBuild)
    {
        _byKey = byKey;
        _isSourceOfTheBuild = isSourceOfTheBuild;
    }

    /// <summary>
    /// The registrations written in <paramref name="compilations"/>' files that
    /// <paramref name="isSourceOfTheBuild"/> claims: the app's own and its referenced projects'. A
    /// library may register its services in an extension method the app calls (<c>AddWallet()</c>),
    /// and that is where the registration is written.
    /// </summary>
    public static ServiceRegistrations Read(IEnumerable<Compilation> compilations, Func<string, bool> isSourceOfTheBuild)
    {
        var byKey = new Dictionary<string, List<ServiceRegistration>>(StringComparer.Ordinal);
        var read = new HashSet<SyntaxTree>();
        foreach (var compilation in compilations)
        {
            foreach (var tree in compilation.SyntaxTrees)
            {
                if (!read.Add(tree) || string.IsNullOrEmpty(tree.FilePath) || !isSourceOfTheBuild(tree.FilePath)) continue;
                SemanticModel? model = null;
                foreach (var invocation in tree.GetRoot().DescendantNodes().OfType<InvocationExpressionSyntax>())
                {
                    // The name first, which costs nothing: a model is asked only about a call that
                    // could be a registration.
                    if (LifetimeOf(NameOf(invocation)) is null) continue;
                    model ??= compilation.GetSemanticModel(tree);
                    if (Of(invocation, model, isSourceOfTheBuild) is not { } registration) continue;
                    var key = registration.Service.Name;
                    if (!byKey.TryGetValue(key, out var registrations)) byKey[key] = registrations = [];
                    registrations.Add(registration);
                }
            }
        }
        return new ServiceRegistrations(byKey, isSourceOfTheBuild);
    }

    /// <summary>Whether the build read any registration at all, which is what lets a resolution
    /// with none be called a mistake: a build that read nothing knows nothing.</summary>
    public bool WasRead => !ReferenceEquals(this, None);

    /// <summary>Every registration of the service whose key is <paramref name="key"/>.</summary>
    public IReadOnlyList<ServiceRegistration> All(string key) =>
        _byKey.TryGetValue(key, out var registrations) ? registrations : [];

    /// <summary>
    /// The registration the container hands <paramref name="key"/> out by, or null when there is none
    /// or the build cannot tell which. A <c>TryAdd…</c> counts only where nothing else registers the
    /// service, which is what the container does with it. Two registrations of different classes are
    /// two answers: which one the container keeps depends on the order they run in, and code decides
    /// that, not the build.
    /// </summary>
    public ServiceRegistration? Find(string key)
    {
        var all = All(key);
        var added = all.Where(registration => !registration.OnlyIfAbsent).ToList();
        var deciding = added.Count > 0 ? added : all;
        var distinct = deciding
            .DistinctBy(registration => (registration.Service, registration.Implementation, registration.Lifetime),
                new RegistrationComparer())
            .ToList();
        return distinct.Count == 1 ? distinct[0] : null;
    }

    /// <summary>The constructor the container builds <paramref name="implementation"/> with: its widest
    /// public one. Both the construction and the names it brings read it here.</summary>
    public static IMethodSymbol? ConstructorOf(INamedTypeSymbol implementation) =>
        implementation.InstanceConstructors
            .Where(candidate => candidate.DeclaredAccessibility == Accessibility.Public)
            .OrderByDescending(candidate => candidate.Parameters.Length)
            .FirstOrDefault();

    /// <summary>Whether two public constructors share the most parameters, which .NET's container refuses
    /// to choose between.</summary>
    public static bool HasTiedConstructors(INamedTypeSymbol implementation)
    {
        var lengths = implementation.InstanceConstructors
            .Where(candidate => candidate.DeclaredAccessibility == Accessibility.Public)
            .Select(candidate => candidate.Parameters.Length)
            .ToList();
        return lengths.Count > 1 && lengths.Count(length => length == lengths.Max()) > 1;
    }

    /// <summary>Whether <paramref name="service"/> is one of the app's own interfaces: declared in
    /// source the build reads as the app's or a referenced project's.</summary>
    public bool IsAppsOwn(ITypeSymbol service) =>
        service.OriginalDefinition.DeclaringSyntaxReferences.Any(reference =>
            !string.IsNullOrEmpty(reference.SyntaxTree.FilePath) && _isSourceOfTheBuild(reference.SyntaxTree.FilePath));

    /// <summary>The method a call names, as written: <c>AddSingleton</c> in
    /// <c>services.AddSingleton&lt;I, C&gt;()</c>.</summary>
    private static string? NameOf(InvocationExpressionSyntax invocation) => invocation.Expression switch
    {
        MemberAccessExpressionSyntax access => access.Name.Identifier.ValueText,
        SimpleNameSyntax name => name.Identifier.ValueText,
        _ => null,
    };

    /// <summary>The registry's word for the lifetime a registration method names, or null for a
    /// method that registers nothing.</summary>
    private static string? LifetimeOf(string? method) => method switch
    {
        "AddSingleton" or "TryAddSingleton" => "singleton",
        "AddScoped" or "TryAddScoped" => "scoped",
        "AddTransient" or "TryAddTransient" => "transient",
        _ => null,
    };

    /// <summary>
    /// The registration a call writes, or null for a call that writes none the browser could repeat:
    /// one of another method, one that registers a class under itself, or one keyed by an interface
    /// that is not the app's own.
    /// </summary>
    private static ServiceRegistration? Of(InvocationExpressionSyntax invocation, SemanticModel model, Func<string, bool> isSourceOfTheBuild)
    {
        if (model.GetSymbolInfo(invocation).Symbol is not IMethodSymbol method) return null;
        var owner = (method.ReducedFrom ?? method).ContainingType?.ToDisplayString();
        if (owner is not (ServiceCollectionExtensions or DescriptorExtensions)) return null;
        var lifetime = LifetimeOf(method.Name)!;

        // What follows the collection: nothing, a factory, an instance, or the types as values.
        var parameters = method.ReducedFrom is null ? method.Parameters.Skip(1).ToList() : method.Parameters.ToList();
        var arguments = method.ReducedFrom is null
            ? invocation.ArgumentList.Arguments.Skip(1).ToList()
            : invocation.ArgumentList.Arguments.ToList();

        ITypeSymbol? service;
        ITypeSymbol? implementation;
        if (method.TypeArguments.Length > 0)
        {
            service = method.TypeArguments[0];
            // `AddSingleton<I, C>()` names the class; a factory or an instance in its place is code the
            // server runs, which the browser cannot.
            implementation = parameters.Count == 0
                ? method.TypeArguments[^1]
                : null;
        }
        else
        {
            // `AddSingleton(typeof(I), typeof(C))`: the same registration with the types as values.
            if (parameters.Count == 0 || !IsSystemType(parameters[0].Type)) return null;
            service = TypeOf(arguments.ElementAtOrDefault(0), model);
            implementation = parameters.Count == 2 && IsSystemType(parameters[1].Type)
                ? TypeOf(arguments.ElementAtOrDefault(1), model)
                : null;
            if (parameters.Count == 1) implementation = service;
        }

        // Keyed by an interface, or a class registered under itself (`AddSingleton<LedgerOptions>()`),
        // which the container builds as its own implementation.
        if (service is not INamedTypeSymbol { IsUnboundGenericType: false } named
            || !(CapabilityRule.IsDependency(named)
                 || (named.TypeKind == TypeKind.Class && SymbolEqualityComparer.Default.Equals(named, implementation)))
            || !named.OriginalDefinition.DeclaringSyntaxReferences.Any(reference =>
                !string.IsNullOrEmpty(reference.SyntaxTree.FilePath) && isSourceOfTheBuild(reference.SyntaxTree.FilePath)))
            return null;

        // A class the browser can build: declared in source the build reads, and not abstract.
        var buildable = implementation is INamedTypeSymbol { TypeKind: TypeKind.Class, IsAbstract: false, IsUnboundGenericType: false } type
            && type.OriginalDefinition.DeclaringSyntaxReferences.Any(reference =>
                !string.IsNullOrEmpty(reference.SyntaxTree.FilePath) && isSourceOfTheBuild(reference.SyntaxTree.FilePath))
            ? type
            : null;

        return new ServiceRegistration(named, buildable, lifetime, method.Name.StartsWith("Try", StringComparison.Ordinal),
            invocation.GetLocation());
    }

    private static bool IsSystemType(ITypeSymbol type) => type.ToDisplayString() == "System.Type";

    /// <summary>The type a <c>typeof(T)</c> argument names, or null for any other argument.</summary>
    private static ITypeSymbol? TypeOf(ArgumentSyntax? argument, SemanticModel model) =>
        argument?.Expression is TypeOfExpressionSyntax typeOf ? model.GetTypeInfo(typeOf.Type).Type : null;

    /// <summary>Two registrations are one answer when they name the same service, class and lifetime.</summary>
    private sealed class RegistrationComparer : IEqualityComparer<(INamedTypeSymbol Service, INamedTypeSymbol? Implementation, string Lifetime)>
    {
        public bool Equals((INamedTypeSymbol Service, INamedTypeSymbol? Implementation, string Lifetime) x,
            (INamedTypeSymbol Service, INamedTypeSymbol? Implementation, string Lifetime) y) =>
            SymbolEqualityComparer.Default.Equals(x.Service, y.Service)
            && SymbolEqualityComparer.Default.Equals(x.Implementation, y.Implementation)
            && x.Lifetime == y.Lifetime;

        public int GetHashCode((INamedTypeSymbol Service, INamedTypeSymbol? Implementation, string Lifetime) obj) =>
            HashCode.Combine(SymbolEqualityComparer.Default.GetHashCode(obj.Service),
                obj.Implementation is null ? 0 : SymbolEqualityComparer.Default.GetHashCode(obj.Implementation), obj.Lifetime);
    }
}
