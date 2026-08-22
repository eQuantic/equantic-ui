using System.Diagnostics;
using System.Runtime.CompilerServices;
using eQuantic.UI.Compiler;
using eQuantic.UI.Compiler.Services;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;
using Xunit.Abstractions;

namespace eQuantic.UI.Web.Tests;

/// <summary>
/// How long the compiler takes, and where it goes — the one dimension of this compiler that had
/// never been measured. The native engine has had a perf harness for a while; the thing a
/// developer waits for on every build had none, so "is it fast enough" could only be answered by
/// feel.
/// <para>
/// Not a pass/fail gate: wall-clock in CI is noise, and a number that fails on a busy machine
/// teaches people to ignore it. It reports, on demand (EQ_PERF=1), over the real corpus — the
/// framework's own components, which are the biggest real sources there are — and the numbers it
/// printed when it was written are recorded below so a later reading has something to compare to.
/// </para>
/// <para>
/// 2026-08-22, 69 files / 119 modules on an M-series Mac: parse 138 ms, references 48 ms,
/// transpile 2.7 s warm (≈40 ms/file) and 4.0 s cold — the extra second is JIT, and the first file
/// alone goes 600 ms → 30 ms between the first pass and the second. Dispatch tries 89 strategy
/// gates per node; the semantic model is asked 1.5 times per distinct node, down from 6.1 before
/// SemanticHelper remembered its answers. Indexing the gates by syntax kind is the obvious next
/// lever and is NOT measured — the honest reading of these numbers is that nothing is pathological
/// and the cost is spread across every node.
/// </para>
/// </summary>
public class CompilerPerfHarnessTests(ITestOutputHelper output)
{
    [Fact]
    public void TheCompilerReportsWhereItsTimeGoes()
    {
        // Off by default: it compiles the whole corpus twice, which is seconds nobody should pay
        // on an ordinary run. EQ_PERF=1 asks for it.
        if (Environment.GetEnvironmentVariable("EQ_PERF") != "1") return;

        var root = RepoRoot();
        var paths = Directory.GetFiles(Path.Combine(root, "src", "eQuantic.UI.Components"), "*.cs")
            .Concat(Directory.GetFiles(Path.Combine(root, "src", "eQuantic.UI.Primitives", "Code"), "*.cs"))
            .Concat(Directory.GetFiles(Path.Combine(root, "src", "eQuantic.UI.Primitives", "Sheet"), "*.cs"))
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToList();

        var watch = Stopwatch.StartNew();
        var trees = paths
            // eqc parses with ParseDefaults.Options (LanguageVersion.Preview). Parsing with
            // Roslyn's defaults here would build a compilation that rejects syntax a real build
            // accepts — an instrument measuring a corpus the compiler never sees.
            .Select(path => CSharpSyntaxTree.ParseText(File.ReadAllText(path), ParseDefaults.Options, path))
            .Append(CSharpSyntaxTree.ParseText(
                "global using System;\nglobal using System.Collections.Generic;\nglobal using System.Linq;",
                ParseDefaults.Options, "GlobalUsings.g.cs"))
            .ToList();
        var parse = watch.ElapsedMilliseconds;

        watch.Restart();
        var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
            .Split(Path.PathSeparator)
            .Where(path => path.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
            .Select(path => (MetadataReference)MetadataReference.CreateFromFile(path))
            .ToList();
        var compilation = CSharpCompilation.Create("Perf", trees, references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary,
                nullableContextOptions: NullableContextOptions.Enable));
        var metadata = watch.ElapsedMilliseconds;

        var compiler = new ComponentCompiler { SymbolsAreAuthoritative = false };
        compiler.SetProjectCompilation(compilation);

        var perFile = new List<(long Ms, string File)>();
        var modules = 0;
        var failed = new List<string>();
        long Pass(bool record)
        {
            var pass = Stopwatch.StartNew();
            foreach (var path in paths)
            {
                var each = Stopwatch.StartNew();
                foreach (var result in compiler.CompileFile(path))
                {
                    if (record) modules++;
                    if (record && !result.Success) failed.Add($"{Path.GetFileName(path)}:{result.ComponentName}");
                }
                if (record) perFile.Add((each.ElapsedMilliseconds, Path.GetFileName(path)));
            }
            return pass.ElapsedMilliseconds;
        }

        var cold = Pass(false);
        var warm = Pass(true);

        output.WriteLine($"{paths.Count} files, {modules} modules");
        output.WriteLine($"  parse        {parse,6} ms");
        output.WriteLine($"  references   {metadata,6} ms");
        output.WriteLine($"  transpile    {cold,6} ms cold");
        output.WriteLine($"  transpile    {warm,6} ms warm   ({warm / (double)paths.Count:F1} ms/file)");
        output.WriteLine("  slowest, warm:");
        foreach (var (ms, file) in perFile.OrderByDescending(entry => entry.Ms).Take(8))
            output.WriteLine($"    {ms,5} ms  {file}");

        // The corpus really COMPILED, which a timing report otherwise never checks: a compiler
        // that failed on every file would produce the tidiest numbers in this file's history.
        // What is deliberately NOT asserted is the time — a wall-clock gate fails on a busy
        // machine and teaches everyone to re-run it until it passes.
        Assert.Empty(failed);
        Assert.NotEqual(0, modules);
        Assert.Equal(paths.Count, perFile.Count);
    }

    private static string RepoRoot([CallerFilePath] string sourcePath = "") =>
        Path.GetFullPath(Path.Combine(Path.GetDirectoryName(sourcePath)!, "..", ".."));
}
