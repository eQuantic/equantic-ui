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
        if (compilation.ContainsSyntaxTree(tree)) return compilation.GetSemanticModel(tree);
        foreach (var referenced in compilation.ReferencedCompilations())
            if (referenced.ContainsSyntaxTree(tree))
                return referenced.GetSemanticModel(tree);
        return null;
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
