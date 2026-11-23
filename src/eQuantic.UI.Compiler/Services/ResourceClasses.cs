using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace eQuantic.UI.Compiler.Services;

/// <summary>One rewritten accessor read: the catalog id, the resx key, and where the Designer
/// lives (the .resx sits beside it). Carried on the ConversionContext exactly as diagnostics are —
/// the semantic model's compilation is an EPHEMERAL replace-per-file instance, so nothing durable
/// may key on it.</summary>
public readonly record struct ResourceUse(string Id, string Key, string DesignerPath);

/// <summary>
/// Track L (docs/I18N-PLAN.md D2/D3): resource-class SHAPE detection and catalog identity.
/// <para>
/// Detection is the ResXFileCodeGenerator shape, never a name list (<see cref="ResourceShape"/>,
/// one file linked into eqc and into the source generator, which warns when an accessor is read
/// once into a static).
/// </para>
/// <para>
/// Ids are the MINIMAL UNIQUE name: the simple class name while it is unique among the app's own
/// resource classes (the D2 example's spelling), namespace-qualified the moment two resx share a
/// simple name — the developer owns the resx and may name them anything. Call sites and catalogs
/// derive from the same answer, so they agree by construction.
/// </para>
/// </summary>
public static class ResourceClasses
{
    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<
        Compilation, Dictionary<INamedTypeSymbol, string>> IdCache = new();

    /// <summary>Whether <paramref name="type"/> is a resx catalog's accessor class
    /// (<see cref="ResourceShape"/>, the rule the source generator's analyzer reads too).</summary>
    public static bool IsResourceClass(INamedTypeSymbol? type) => ResourceShape.IsResourceClass(type);

    /// <summary>A static string property on a resource class — the generated accessor for one key
    /// (<see cref="ResourceShape"/>).</summary>
    public static bool IsResourceAccessor(IPropertySymbol? property) => ResourceShape.IsResourceAccessor(property);

    /// <summary>
    /// The resx KEY behind an accessor: the literal its getter hands to <c>GetString("…")</c> —
    /// the generator mangles invalid chars into the PROPERTY name ("Hero.Title" → Hero_Title), so
    /// the property name is only the fallback when no getter syntax is reachable.
    /// </summary>
    public static string KeyFor(IPropertySymbol property)
    {
        foreach (var reference in property.ReadDeclarations())
        {
            if (reference.GetSyntax() is not PropertyDeclarationSyntax declaration) continue;
            foreach (var invocation in declaration.DescendantNodes().OfType<InvocationExpressionSyntax>())
            {
                if (invocation.Expression is not MemberAccessExpressionSyntax { Name.Identifier.ValueText: "GetString" }) continue;
                if (invocation.ArgumentList.Arguments.Count > 0
                    && invocation.ArgumentList.Arguments[0].Expression is LiteralExpressionSyntax literal
                    && literal.Token.Value is string key)
                    return key;
            }
        }
        return property.Name;
    }

    /// <summary>Where the Designer that declares this class lives ("" for metadata-only types).</summary>
    public static string DesignerPathFor(INamedTypeSymbol type) =>
        type.ReadDeclarations().FirstOrDefault()?.SyntaxTree.FilePath ?? "";

    /// <summary>The catalog id for a resource class — minimal unique across the app's own set.
    /// A pure function of the compilation (cached per instance; ephemeral instances re-derive the
    /// same answer, so fragmentation is harmless).</summary>
    public static string IdFor(Compilation compilation, INamedTypeSymbol type)
    {
        var ids = IdCache.GetValue(compilation, BuildIds);
        return ids.TryGetValue(type, out var id) ? id : type.Name;
    }

    private static Dictionary<INamedTypeSymbol, string> BuildIds(Compilation compilation)
    {
        // The APP's own resource classes only (source-declared): a referenced assembly's strings
        // resolve through its own build, and the split is semantic, never a list — the same
        // IsInSource discriminator the runtime-provided scanner uses.
        var declared = new List<INamedTypeSymbol>();
        void Walk(INamespaceSymbol ns)
        {
            foreach (var member in ns.GetMembers())
            {
                if (member is INamespaceSymbol nested) Walk(nested);
                else if (member is INamedTypeSymbol type
                    && type.Locations.Any(l => l.IsInSource)
                    && IsResourceClass(type))
                    declared.Add(type);
            }
        }
        Walk(compilation.Assembly.GlobalNamespace);

        var ids = new Dictionary<INamedTypeSymbol, string>(SymbolEqualityComparer.Default);
        foreach (var group in declared.GroupBy(t => t.Name, StringComparer.Ordinal))
        {
            var collided = group.Count() > 1;
            foreach (var type in group)
                ids[type] = collided ? type.ToDisplayString() : type.Name;
        }
        return ids;
    }
}
