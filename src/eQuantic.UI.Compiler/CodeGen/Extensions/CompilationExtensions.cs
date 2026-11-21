using System.Runtime.CompilerServices;
using Microsoft.CodeAnalysis;

namespace eQuantic.UI.Compiler.CodeGen.Extensions;

/// <summary>
/// Questions about a compilation that reach the projects it references as compilations. A symbol's
/// declaring tree used to be a tree of the compilation that bound the symbol, always. A type of a
/// referenced project is declared in that project's compilation, and asking the app's for the model
/// of its file throws <see cref="ArgumentException"/> (#624).
/// </summary>
internal static class CompilationExtensions
{
    /// <summary>
    /// The semantic model of <paramref name="tree"/> from the compilation that holds it: this one, or
    /// one it references as a compilation, directly or through another. Null when none holds it, which
    /// is a tree a caller has no model for and must read as written.
    /// </summary>
    public static SemanticModel? ModelOf(this Compilation compilation, SyntaxTree tree)
    {
        // A tree reached for its model is a tree read, whatever reached it (#1281).
        Services.SourceReads.Note(tree);
        if (compilation.ContainsSyntaxTree(tree)) return compilation.GetSemanticModel(tree);
        foreach (var referenced in compilation.ReferencedCompilations())
            if (referenced.ContainsSyntaxTree(tree))
                return referenced.GetSemanticModel(tree);
        return null;
    }

    private static readonly ConditionalWeakTable<Compilation, StrongBox<bool>> NamedLikeTheRuntime = new();

    /// <summary>
    /// Whether a class or a struct of this compilation, or of a project it references, whose twin eqc
    /// writes, takes the name of a type the runtime carries: the app's own <c>Text</c> beside the
    /// vocabulary's. Asked once a compilation before any module is walked for the two in one module
    /// (#1777), so a build whose types share no name with the runtime's walks nothing for it. The SDK's
    /// own assemblies, compiled into the runtime, declare the runtime's types and are none of these.
    /// </summary>
    public static bool DeclaresATypeNamedLikeTheRuntimes(this Compilation compilation) =>
        NamedLikeTheRuntime.GetValue(compilation, static c => new StrongBox<bool>(NamesLikeTheRuntime(c))).Value;

    private static bool NamesLikeTheRuntime(Compilation compilation)
    {
        var runtime = new HashSet<string>(StringComparer.Ordinal);
        // Every namespace the runtime carries is under eQuantic, so the platform's thousands of types are
        // never walked.
        foreach (var assembly in compilation.SourceModule.ReferencedAssemblySymbols)
            foreach (var root in assembly.GlobalNamespace.GetNamespaceMembers().Where(space => space.Name == "eQuantic"))
                Collect(root, runtime, source: false);
        if (runtime.Count == 0) return false;

        var declared = new HashSet<string>(StringComparer.Ordinal);
        Collect(compilation.Assembly.GlobalNamespace, declared, source: true);
        foreach (var referenced in compilation.ReferencedCompilations())
            Collect(referenced.Assembly.GlobalNamespace, declared, source: true);
        return declared.Overlaps(runtime);
    }

    /// <summary>The twin names of the classes and structs under <paramref name="space"/>: those of the
    /// runtime's namespaces, or with <paramref name="source"/> those of every other namespace.</summary>
    private static void Collect(INamespaceSymbol space, HashSet<string> names, bool source)
    {
        foreach (var member in space.GetMembers())
        {
            if (member is INamespaceSymbol nested)
            {
                Collect(nested, names, source);
                continue;
            }

            if (member is not INamedTypeSymbol type) continue;
            var ofTheRuntime = Services.RuntimeProvidedTypeScanner.IsRuntimeProvidedNamespace(space.ToDisplayString());
            if (ofTheRuntime == source) continue;
            Types(type, names, source);
        }
    }

    private static void Types(INamedTypeSymbol type, HashSet<string> names, bool source)
    {
        if (type.TypeKind is TypeKind.Class or TypeKind.Struct)
        {
            names.Add(type.TwinReference());
            // A type of the runtime's is imported under every name it is exported by (`BuildContext`).
            if (!source) names.Add(Services.RuntimeProvidedTypeScanner.ExportedName(type, asValue: false));
        }
        foreach (var nested in type.GetTypeMembers()) Types(nested, names, source);
    }

    /// <summary>Every compilation this one references as a compilation, directly or through another,
    /// each once.</summary>
    public static IEnumerable<Compilation> ReferencedCompilations(this Compilation compilation)
    {
        var seen = new HashSet<Compilation>(ReferenceEqualityComparer.Instance) { compilation };
        var pending = new Queue<Compilation>();
        pending.Enqueue(compilation);
        while (pending.Count > 0)
        {
            foreach (var reference in pending.Dequeue().References)
            {
                if (reference is not CompilationReference { Compilation: { } referenced } || !seen.Add(referenced))
                    continue;
                pending.Enqueue(referenced);
                yield return referenced;
            }
        }
    }
}
