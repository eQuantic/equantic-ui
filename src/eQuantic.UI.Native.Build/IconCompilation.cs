using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace eQuantic.UI.Build;

/// <summary>
/// The compilation the app's icon is rasterized from: its source, the app's vector catalog when the
/// SDK hands it over, and the usings the app compiles with (#1240).
/// <para>
/// The icon is a file of the app and is written like one, so the usings the app has reach it here
/// too: <c>Box(...)</c> is the factory in the app, and without the app's <c>global using static</c>
/// the same line named the vocabulary's type and failed here alone. The SDK hands every <c>Using</c>
/// item of the app, and the ones that name nothing the vocabulary has (a web host's ASP.NET
/// namespaces) are dropped, since an icon drawn before the app runs could not use them anyway. The
/// decision is the compiler's: a using is kept when it resolves against the references the icon is
/// compiled with, and only then.
/// </para>
/// </summary>
public static class IconCompilation
{
    /// <summary>The diagnostics that say a using names nothing: a namespace or a type not found.</summary>
    private static readonly HashSet<string> Unresolved = ["CS0246", "CS0234", "CS0400"];

    /// <summary>
    /// The icon's compilation. <paramref name="usings"/> are spelled as a <c>using</c> directive is
    /// after its keyword: <c>System</c>, <c>static eQuantic.UI.Components.UI</c>,
    /// <c>StatefulComponent = eQuantic.UI.Primitives.StatefulComponent</c>.
    /// </summary>
    public static CSharpCompilation Create(IReadOnlyList<SyntaxTree> sources, IReadOnlyList<string> usings,
        IReadOnlyList<MetadataReference> references)
    {
        var options = new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary,
            nullableContextOptions: NullableContextOptions.Enable);
        var parse = sources.Count > 0 ? (CSharpParseOptions)sources[0].Options : CSharpParseOptions.Default;

        var kept = usings.Distinct(StringComparer.Ordinal).ToList();
        var compilation = With(kept);

        // One pass decides: a using that names nothing reports on its own line of the usings file,
        // and removing it changes nothing about whether another one resolves.
        var usingsTree = compilation.SyntaxTrees.LastOrDefault(tree => tree.FilePath == UsingsPath);
        if (usingsTree is null) return compilation;
        var unresolved = compilation.GetDiagnostics()
            .Where(diagnostic => diagnostic.Location.SourceTree == usingsTree && Unresolved.Contains(diagnostic.Id))
            .Select(diagnostic => diagnostic.Location.GetLineSpan().StartLinePosition.Line)
            .ToHashSet();
        return unresolved.Count == 0
            ? compilation
            : With(kept.Where((_, line) => !unresolved.Contains(line)).ToList());

        CSharpCompilation With(IReadOnlyList<string> spellings)
        {
            var trees = sources.ToList();
            if (spellings.Count > 0)
                trees.Add(CSharpSyntaxTree.ParseText(
                    string.Join('\n', spellings.Select(spelling => $"global using {spelling};")), parse, UsingsPath));
            return CSharpCompilation.Create("eQuantic.AppIcon", trees, references, options);
        }
    }

    /// <summary>The name the usings file is parsed under, one using a line, in the order handed.</summary>
    private const string UsingsPath = "AppIcon.GlobalUsings.g.cs";
}
