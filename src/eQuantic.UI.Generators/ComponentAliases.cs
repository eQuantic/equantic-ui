using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace eQuantic.UI.Generators;

/// <summary>
/// Which of the app's components get a global alias beside their factory, so a static member declared on
/// one reads as <c>ProductEditor.Toggled(…)</c> from anywhere in the app (#1781).
/// <para>
/// The factory surface is a <c>global using static</c>, a file's <c>using Shop.Components;</c> stands at the
/// same level, and between a method and a type found there C# takes the method: the call did not compile
/// (CS0119) wherever the component's namespace was not the caller's own or one around it. A
/// <c>global using</c> alias outranks both, and a call of the name still finds the factory, since a call
/// looks past a type for something it can invoke.
/// </para>
/// <para>
/// An alias outranks every other type of its name a file brings in at that level too, so it is written
/// only where there is none: no namespace a file of the app imports at the compilation unit's level
/// (<c>System.Threading</c>, which ASP.NET imports for every file, holds a <c>Timer</c>) declares a type of
/// the name, and the global namespace declares nothing of it. Where one does, the component keeps what it
/// had, and nothing an app wrote changes meaning.
/// </para>
/// </summary>
internal static class ComponentAliases
{
    /// <summary>The names of <paramref name="candidates"/> an alias can take without changing the meaning
    /// of any name the app writes.</summary>
    public static IEnumerable<string> Of(IEnumerable<(string Name, string Namespace)> candidates, Compilation compilation)
    {
        var list = candidates.ToList();
        if (list.Count == 0) return [];

        // What a file brings in at the compilation unit's level: a namespace's types, a class's nested types
        // through `using static`, and a name of its own through an alias, which a global alias of the same
        // name would collide with.
        var imported = new List<INamespaceOrTypeSymbol>();
        var aliased = new HashSet<string>(System.StringComparer.Ordinal);
        var seen = new HashSet<string>(System.StringComparer.Ordinal);
        foreach (var tree in compilation.SyntaxTrees)
        {
            if (tree.GetRoot() is not CompilationUnitSyntax unit) continue;
            foreach (var directive in unit.Usings)
            {
                if (directive.Alias is { } alias)
                {
                    aliased.Add(alias.Name.Identifier.ValueText);
                    continue;
                }

                var name = directive.Name?.ToString().Replace("global::", "") ?? "";
                if (name.Length == 0 || !seen.Add(name)) continue;
                if (directive.StaticKeyword.IsKind(SyntaxKind.None))
                {
                    if (Namespace(compilation, name) is { } space) imported.Add(space);
                }
                else if (compilation.GetSemanticModel(tree).GetSymbolInfo(directive.Name!).Symbol is INamedTypeSymbol type)
                {
                    imported.Add(type);
                }
            }
        }

        return list
            .Where(candidate => !aliased.Contains(candidate.Name))
            .Where(candidate => !compilation.GlobalNamespace.GetMembers(candidate.Name).Any())
            .Where(candidate => !imported.Any(holder =>
                holder.ToDisplayString() != candidate.Namespace && !holder.GetTypeMembers(candidate.Name).IsEmpty))
            .Select(candidate => candidate.Name)
            .ToList();
    }

    private static INamespaceSymbol? Namespace(Compilation compilation, string dotted)
    {
        INamespaceSymbol? space = compilation.GlobalNamespace;
        foreach (var part in dotted.Split('.'))
        {
            space = space?.GetNamespaceMembers().FirstOrDefault(member => member.Name == part);
            if (space is null) return null;
        }

        return space;
    }
}
