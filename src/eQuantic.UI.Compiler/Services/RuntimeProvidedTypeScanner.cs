using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace eQuantic.UI.Compiler.Services;

/// <summary>
/// Semantic classifier of RUNTIME-PROVIDED type references — the single implementation behind every
/// emission path (components, state classes, static helpers). A referenced type routes to
/// <c>@equantic/runtime</c> when it lives in the shared vocabulary (<c>eQuantic.UI.Primitives</c>),
/// the shared component library (<c>eQuantic.UI.Components</c>, whose transpiled modules ship
/// embedded in the runtime — this is also what disambiguates the deliberate name reuse against the
/// standard web components: the file's usings decide which type a name binds to), or carries
/// <c>[RuntimeProvided]</c>. Enums are tracked separately: they lower to camelCase member-name string
/// literals and must never be imported.
/// </summary>
public static class RuntimeProvidedTypeScanner
{
    /// <summary>Component-model base names — the emitter owns the emitted base class (e.g. the shared
    /// stateful shape swaps to <c>SharedStatefulComponent</c>), so the sweep must not import them.</summary>
    private static readonly HashSet<string> ComponentModelBaseNames =
        new() { "UiComponent", "StatelessComponent", "StatefulComponent" };

    public static bool IsRuntimeProvidedNamespace(string ns) =>
        ns == "eQuantic.UI.Primitives"
        || ns.StartsWith("eQuantic.UI.Primitives.")
        || ns == "eQuantic.UI.Components"
        || ns.StartsWith("eQuantic.UI.Components.");

    /// <summary>Walks every identifier under <paramref name="root"/>, resolving symbols through
    /// <paramref name="model"/>, and buckets runtime-provided type names, enum type names, and —
    /// when <paramref name="appTypes"/> is supplied — APP-LEVEL types declared in this compilation's
    /// own source.
    ///
    /// That third bucket exists for the same reason the runtime-provided one does (see
    /// TypeScriptEmitter's "SOURCE, not just a filter" note): a type reached only through a STATIC
    /// MEMBER (<c>Brand.Violet</c>, <c>Copy.Title</c>) is invisible to the syntactic collectors, so
    /// without it the emitted module references a name it never imports and throws
    /// "&lt;Type&gt; is not defined" in the browser — with NO build error. Instantiated types
    /// (<c>new View()</c>) were already covered; static access was the hole.
    /// </summary>
    public static void Collect(SyntaxNode root, SemanticModel model,
        ISet<string> runtimeProvided, ISet<string> enumTypes, ISet<string>? appTypes = null)
    {
        // A `with` on a vocabulary record REBUILDS through the constructor —
        // `theme.Type(role) with { Size = 15 }` emits `new TypeStyle({ … })` — and the name it
        // constructs appears NOWHERE in the source, so no identifier sweep can find it. Same hole
        // the target-typed `new(...)` had, and the same symptom: "TypeStyle is not defined", with
        // no build error.
        foreach (var with in root.DescendantNodes().OfType<WithExpressionSyntax>())
        {
            ITypeSymbol? rebuilt;
            try { rebuilt = model.GetTypeInfo(with).Type; }
            catch { continue; }
            if (rebuilt is not INamedTypeSymbol { Name.Length: > 0 } named) continue;

            var rebuiltNamespace = named.ContainingNamespace?.ToDisplayString() ?? string.Empty;
            if (IsRuntimeProvidedNamespace(rebuiltNamespace))
                runtimeProvided.Add(named.Name);
            else if (appTypes is not null && named.Locations.Any(l => l.IsInSource))
                appTypes.Add(named.Name);
        }

        foreach (var identifier in root.DescendantNodes().OfType<IdentifierNameSyntax>())
        {
            ISymbol? symbol;
            try { symbol = model.GetSymbolInfo(identifier).Symbol; }
            catch { continue; }

            // `new Text(...)` resolves the identifier to the CONSTRUCTOR — take its containing type.
            var type = symbol as INamedTypeSymbol
                       ?? (symbol is IMethodSymbol { MethodKind: MethodKind.Constructor } ctorSymbol
                           ? ctorSymbol.ContainingType
                           : null);
            if (type is null) continue;

            if (type.TypeKind == TypeKind.Enum)
            {
                enumTypes.Add(type.Name);
                continue;
            }

            // Interfaces exist only in the C# type system — they produce no JS value to import.
            if (type.TypeKind == TypeKind.Interface) continue;

            if (ComponentModelBaseNames.Contains(type.Name))
            {
                // Only the BASE-LIST position is emitter-owned (the authoring bases are swapped at
                // emission). `UiComponent` legitimately surfaces in member signatures — the
                // reconciler's `AdoptConfig(UiComponent next)` — and the runtime exports it.
                var isBaseListUse = identifier.Ancestors().Any(a => a is BaseListSyntax);
                if (type.Name != "UiComponent" || isBaseListUse) continue;
            }

            var ns = type.ContainingNamespace?.ToDisplayString() ?? string.Empty;
            var isRuntimeProvided = IsRuntimeProvidedNamespace(ns)
                || type.GetAttributes().Any(a => a.AttributeClass?.Name == "RuntimeProvidedAttribute");
            if (isRuntimeProvided)
            {
                runtimeProvided.Add(type.Name);
                continue;
            }

            // App-level types are the ones DECLARED IN SOURCE (framework and BCL types arrive as
            // metadata from referenced assemblies) — a semantic distinction, never a name list.
            // The emitter decides which of these actually became modules before importing them.
            if (appTypes is not null && type.Locations.Any(l => l.IsInSource))
                appTypes.Add(type.Name);
        }
    }
}
