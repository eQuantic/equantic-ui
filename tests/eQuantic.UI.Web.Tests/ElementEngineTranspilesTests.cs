using System.Runtime.CompilerServices;
using eQuantic.UI.Compiler;
using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace eQuantic.UI.Web.Tests;

/// <summary>
/// The element engine goes through eqc with no diagnostic (docs/ELEMENT-TREE-PLAN.md 3.9, #1127).
/// <para>
/// The engine is written once and transpiled into the runtime like the code editor's and the
/// spreadsheet's. Until its modules are pinned by <see cref="SharedComponentTranspilationTests"/>, the
/// engine keeps <c>[ServerOnly]</c>, and a server-only type compiles to NOTHING: a check that read eqc's
/// results with the attribute in place would pass over every file without compiling one. So this one
/// lifts the attribute from the sources it hands eqc, and fails on a file that yields no module as it
/// fails on a refusal. A construct eqc cannot translate, written into the engine, fails here by name
/// rather than on the day the engine reaches the browser.
/// </para>
/// </summary>
public class ElementEngineTranspilesTests
{
    private static string RepoRoot([CallerFilePath] string sourcePath = "") =>
        Path.GetFullPath(Path.Combine(Path.GetDirectoryName(sourcePath)!, "..", ".."));

    [Fact]
    public void EveryFileOfTheEngine_TranspilesWithNoDiagnostic()
    {
        var engine = Directory.GetFiles(Path.Combine(RepoRoot(), "src", "eQuantic.UI.Primitives", "Elements"), "*.cs")
            .OrderBy(path => path, StringComparer.Ordinal)
            .Select(path => (Path: path, Source: WithoutServerOnly(File.ReadAllText(path), path)))
            .ToList();
        engine.Should().HaveCountGreaterThan(4, "the engine is a folder of files, and an empty one proves nothing");

        var trees = engine.Select(file => CSharpSyntaxTree.ParseText(file.Source, path: file.Path)).ToList();
        trees.Add(SdkImplicitUsings.Tree());
        var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
            .Split(Path.PathSeparator)
            .Where(path => path.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
            .Select(path => (MetadataReference)TestReferences.Of(path))
            .ToList();
        var compiler = new ComponentCompiler { SymbolsAreAuthoritative = false };
        compiler.SetProjectCompilation(CSharpCompilation.Create("ElementEngine", trees, references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable)));

        var refused = new List<string>();
        foreach (var (path, source) in engine)
        {
            var name = Path.GetFileName(path);
            var results = compiler.CompileSource(source, path).ToList();
            if (results.Count == 0) refused.Add($"{name}: eqc wrote no module for it");
            foreach (var result in results)
            foreach (var diagnostic in result.Errors.Concat(result.Warnings))
                refused.Add($"{name} ({result.ComponentName}): {diagnostic.Code} {diagnostic.Message}");
        }

        refused.Should().BeEmpty("the engine crosses to the browser, so every construct in it must be one eqc translates");
    }

    /// <summary>The source with every <c>[ServerOnly]</c> attribute removed, the rest of it as written: an
    /// attribute beside it in the same brackets stays, and only a list left empty goes.</summary>
    private static string WithoutServerOnly(string source, string path)
    {
        var root = CSharpSyntaxTree.ParseText(source, path: path).GetRoot();
        var stripped = root.ReplaceNodes(
            root.DescendantNodes().OfType<AttributeListSyntax>().Where(list => list.Attributes.Any(IsServerOnly)),
            (_, list) => list.WithAttributes(SyntaxFactory.SeparatedList(list.Attributes.Where(attribute => !IsServerOnly(attribute)))));
        var emptied = stripped.DescendantNodes().OfType<AttributeListSyntax>().Where(list => list.Attributes.Count == 0);
        return stripped.RemoveNodes(emptied, SyntaxRemoveOptions.KeepExteriorTrivia)!.ToFullString();
    }

    private static bool IsServerOnly(AttributeSyntax attribute) =>
        attribute.Name.ToString() is "ServerOnly" or "ServerOnlyAttribute";
}
