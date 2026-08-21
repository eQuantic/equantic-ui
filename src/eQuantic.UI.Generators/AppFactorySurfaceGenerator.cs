using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace eQuantic.UI.Generators;

/// <summary>
/// Writes the app's own half of the declarative surface.
/// <para>
/// The SDK's components are reachable without <c>new</c> because a hand-written class gives each
/// one a factory named after it. An app's components had no such class, so a screen that composed
/// both read with a seam down the middle — <c>Text(…)</c> beside <c>new MyCard(…)</c> — and the
/// only fix was for every developer to hand-roll the same class and remember to keep it in step
/// with their components. That is a convention to learn; this is a build that already knows.
/// </para>
/// <para>
/// One factory per component, because the surface transpiles to a JavaScript twin and JS methods do
/// not overload. The constructor is the WIDEST — the same rule the emitter applies when it collapses
/// overloads, so the generated factory and the transpiled constructor never disagree — unless one is
/// elected with <c>[UiFactory]</c>.
/// </para>
/// </summary>
[Generator]
public sealed class AppFactorySurfaceGenerator : IIncrementalGenerator
{
    /// <summary>
    /// FullyQualifiedFormat drops the <c>?</c> on a nullable reference type, so a component
    /// declaring <c>string? eyebrow = null</c> came out of here as <c>string eyebrow = null</c> —
    /// CS8625 in every consumer with nullable enabled, in generated code they cannot edit, and a
    /// hard build failure for anyone who treats warnings as errors. The signature also lied about
    /// what the factory accepts.
    /// </summary>
    /// <summary>The type without its nullable annotation. A capability is resolved by its
    /// interface — <c>Resolve&lt;IClock&gt;()</c>, never <c>Resolve&lt;IClock?&gt;()</c>, which is a
    /// different call and says nothing the method's own return type does not already say.</summary>
    private static string Unannotated(string type) =>
        type.EndsWith("?", StringComparison.Ordinal) ? type.Substring(0, type.Length - 1) : type;

    private static readonly SymbolDisplayFormat FullyQualifiedWithNullability =
        SymbolDisplayFormat.FullyQualifiedFormat.WithMiscellaneousOptions(
            SymbolDisplayFormat.FullyQualifiedFormat.MiscellaneousOptions
            | SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier);

    private const string FactoryAttribute = "UiFactoryAttribute";
    private const string PageAttribute = "PageAttribute";

    internal static readonly DiagnosticDescriptor AmbiguousElection = new(
        "EQ3101", "A component elects more than one factory constructor",
        "'{0}' marks {1} constructors with [UiFactory]. A component gets ONE factory — the twin is "
        + "JavaScript, which has no overloads — so the election needs a single winner.",
        "eQuantic.UI", DiagnosticSeverity.Error, isEnabledByDefault: true);

    internal static readonly DiagnosticDescriptor NameCollision = new(
        "EQ3102", "Two components share a name",
        "'{0}' and '{1}' have the same name, so only one can own the factory called '{2}'. Rename "
        + "one, or keep them apart and build the other with `new`.",
        "eQuantic.UI", DiagnosticSeverity.Warning, isEnabledByDefault: true);

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var components = context.SyntaxProvider.CreateSyntaxProvider(
                predicate: static (node, _) => node is ClassDeclarationSyntax { BaseList: not null },
                transform: static (ctx, token) => Describe(ctx, token))
            .Where(static component => component is not null)
            .Collect();

        context.RegisterSourceOutput(components, static (spc, all) => Emit(spc, all));
    }

    private static void Emit(SourceProductionContext spc, ImmutableArray<Component?> all)
    {
        var found = all.Where(c => c is not null).Select(c => c!).ToList();
        foreach (var component in found.Where(c => c.ElectedCount > 1))
            spc.ReportDiagnostic(Diagnostic.Create(AmbiguousElection, component.Location,
                component.Name, component.ElectedCount));

        var usable = found.Where(c => c.ElectedCount <= 1).ToList();
        if (usable.Count == 0) return;

        // Two components with the same name would emit the same factory twice — which does not
        // compile, and which the developer would read as the generator being broken rather than as
        // a name they own. Report it and keep the first, so the rest of the surface still exists.
        var byName = new Dictionary<string, Component>();
        foreach (var component in usable.OrderBy(c => c.FullName, System.StringComparer.Ordinal))
        {
            if (byName.TryGetValue(component.Name, out var winner))
            {
                spc.ReportDiagnostic(Diagnostic.Create(NameCollision, component.Location,
                    winner.FullName, component.FullName, component.Name));
                continue;
            }
            byName[component.Name] = component;
        }

        // The surface lives in the namespace the app's components share — the shortest common one,
        // so a project that grew subfolders still gets ONE surface rather than several.
        var surfaceNamespace = CommonNamespace(byName.Values.Select(c => c.Namespace));
        if (surfaceNamespace.Length == 0) return;

        var source = new StringBuilder();
        source.AppendLine("// <auto-generated/>");
        source.AppendLine("#nullable enable");
        // The global using is what makes the factories reachable with no import in any file — the
        // same way the SDK puts its own surface in scope.
        source.AppendLine($"global using static {surfaceNamespace}.AppUI;");
        source.AppendLine();
        source.AppendLine($"namespace {surfaceNamespace};");
        source.AppendLine();
        source.AppendLine("/// <summary>The app's components, reachable without `new`. Generated.</summary>");
        source.AppendLine("public static class AppUI");
        source.AppendLine("{");
        foreach (var component in byName.Values.OrderBy(c => c.Name, System.StringComparer.Ordinal))
        {
            // A CAPABILITY never reaches the signature: a caller composing a component has no
            // container to reach into, and eqc already resolves it on the other side. The factory
            // asks the same scope the SSR pipeline arms, so both targets construct alike.
            var parameters = string.Join(", ", component.Parameters
                .Where(p => !p.IsDependency)
                .Select(p => p.Default is null ? $"{p.Type} {p.Name}" : $"{p.Type} {p.Name} = {p.Default}"));
            // A capability the component declared it CANNOT work without is required by name: a
            // target that does not have it gets a sentence saying which one and where, instead of a
            // null that travels into the component and fails somewhere inside it. One the component
            // declared nullable is handed over as it comes — that is the author saying they cope.
            var arguments = string.Join(", ", component.Parameters.Select(p => p.IsDependency
                ? (p.IsRequired
                    ? $"global::eQuantic.UI.Primitives.CapabilityScope.Require<{Unannotated(p.Type)}>(\"{component.Name}\")"
                    : $"global::eQuantic.UI.Primitives.CapabilityScope.Resolve<{Unannotated(p.Type)}>()")
                : p.Name));
            source.AppendLine($"    /// <summary>Builds a <see cref=\"{component.Name}\"/>.</summary>");
            // Qualified on the RIGHT of `new` only: the factory's own name shadows the type inside
            // this class, so `new Panel(…)` here would bind to the method and not compile.
            source.AppendLine(
                $"    public static {component.FullName} {component.Name}({parameters}) => "
                + $"new {component.FullName}({arguments});");
        }
        source.AppendLine("}");

        spc.AddSource("AppUI.g.cs", source.ToString());
    }

    /// <summary>The longest namespace prefix every component shares, on dot boundaries.</summary>
    private static string CommonNamespace(IEnumerable<string> namespaces)
    {
        string? common = null;
        foreach (var ns in namespaces)
        {
            if (common is null) { common = ns; continue; }
            var a = common.Split('.');
            var b = ns.Split('.');
            var shared = 0;
            while (shared < a.Length && shared < b.Length && a[shared] == b[shared]) shared++;
            common = string.Join(".", a.Take(shared));
        }
        return common ?? "";
    }

    private sealed class Component
    {
        public Component(string ns, string name, string fullName, int electedCount,
            List<(string Type, string Name, string? Default, bool IsDependency, bool IsRequired)> parameters, Location location)
        {
            Namespace = ns; Name = name; FullName = fullName;
            ElectedCount = electedCount; Parameters = parameters; Location = location;
        }

        public string Namespace { get; }
        public string Name { get; }
        /// <summary>Globally qualified, so the factory body is never ambiguous.</summary>
        public string FullName { get; }
        public int ElectedCount { get; }
        public List<(string Type, string Name, string? Default, bool IsDependency, bool IsRequired)> Parameters { get; }
        public Location Location { get; }
    }

    private static Component? Describe(GeneratorSyntaxContext ctx, System.Threading.CancellationToken token)
    {
        if (ctx.SemanticModel.GetDeclaredSymbol(ctx.Node, token) is not INamedTypeSymbol symbol) return null;
        if (symbol.IsAbstract || symbol.IsGenericType) return null;
        if (symbol.DeclaredAccessibility != Accessibility.Public) return null;
        if (symbol.ContainingNamespace.IsGlobalNamespace) return null;

        if (!IsComponent(symbol)) return null;
        // A PAGE is reached by its route, never composed by hand — a factory for one would be an
        // offer to do the wrong thing.
        if (symbol.GetAttributes().Any(a => a.AttributeClass?.Name == PageAttribute)) return null;

        var constructors = symbol.InstanceConstructors
            .Where(c => c.DeclaredAccessibility == Accessibility.Public && !c.IsStatic)
            .ToList();
        if (constructors.Count == 0) return null;

        var elected = constructors
            .Where(c => c.GetAttributes().Any(a => a.AttributeClass?.Name == FactoryAttribute))
            .ToList();

        // WIDEST by default — the rule the emitter already applies to overloads — or the elected one.
        var chosen = elected.Count == 1
            ? elected[0]
            : constructors.OrderByDescending(c => c.Parameters.Length).First();

        var parameters = chosen.Parameters
            .Select(p => (
                Type: p.Type.ToDisplayString(FullyQualifiedWithNullability),
                Name: p.Name,
                Default: DefaultLiteral(p),
                IsDependency: CapabilityRule.IsDependency(p.Type),
                IsRequired: CapabilityRule.IsRequired(p)))
            .ToList();

        return new Component(
            symbol.ContainingNamespace.ToDisplayString(),
            symbol.Name,
            symbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
            elected.Count,
            parameters,
            symbol.Locations.FirstOrDefault() ?? Location.None);
    }

    private static bool IsComponent(INamedTypeSymbol symbol)
    {
        for (var b = symbol.BaseType; b is not null; b = b.BaseType)
            if (b.Name is "StatelessComponent" or "StatefulComponent" or "UiComponent")
                return true;
        return false;
    }

    /// <summary>
    /// A parameter's default, spelled so the factory MIRRORS the constructor — an omitted argument
    /// has to mean the same on both. Anything that cannot be written as a literal (a struct default
    /// such as <c>BoxStyle</c>) becomes <c>default</c>, which is exactly what it meant.
    /// </summary>
    private static string? DefaultLiteral(IParameterSymbol parameter)
    {
        if (!parameter.HasExplicitDefaultValue) return null;
        var value = parameter.ExplicitDefaultValue;
        if (value is null) return parameter.Type.IsValueType ? "default" : "null";

        var enumType = parameter.Type.TypeKind == TypeKind.Enum ? parameter.Type : null;
        if (enumType is not null)
        {
            var member = enumType.GetMembers().OfType<IFieldSymbol>()
                .FirstOrDefault(f => f.HasConstantValue && Equals(f.ConstantValue, value));
            if (member is not null)
                return $"{enumType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)}.{member.Name}";
            return "default";
        }

        return value switch
        {
            bool flag => flag ? "true" : "false",
            string text => "\"" + text.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"",
            char c => "'" + c + "'",
            float f => f.ToString(System.Globalization.CultureInfo.InvariantCulture) + "f",
            double d => d.ToString(System.Globalization.CultureInfo.InvariantCulture),
            decimal m => m.ToString(System.Globalization.CultureInfo.InvariantCulture) + "m",
            _ => System.Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture) ?? "default",
        };
    }
}
