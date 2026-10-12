using System.Runtime.Loader;
using System.Text.Json;
using eQuantic.UI.Compiler.Tests.ReferencedProjects;
using eQuantic.UI.Conformance.Tests.Infrastructure;
using FluentAssertions;
using Xunit;

namespace eQuantic.UI.Conformance.Tests;

/// <summary>
/// A type of a project the app references ANSWERS in the browser as it answers in .NET (#624). The
/// compiler's tests read the modules such a build writes. This runs them: the solution is built as the
/// SDK builds it, bundled by the build's own bundler, and bun calls the cases, a static class of the
/// app's that builds and reads the library's types. The same solution's assemblies, compiled as
/// MSBuild compiles them, are loaded and called in .NET, and the two must agree case by case.
/// <para>
/// What it adds to the modules being right one by one is that they are right TOGETHER: a module
/// that imports another, of another project, which the bundler has to find, and a record of a second
/// project inside a record of the first. Before the fix the build wrote none of the library's modules
/// and the first case threw a <c>ReferenceError</c>.
/// </para>
/// </summary>
public class ReferencedProjectConformanceTests
{
    /// <summary>The cases, in the app's ROOT, which makes the file an entry point of the bundle.</summary>
    private const string Cases =
        """
        using Common;
        using Domain;

        namespace App;

        public static class Cases
        {
            // A record of the library, built here: its own method, and equality by value.
            public static string RecordBuiltAndCompared()
            {
                var next = new Cursor(2).Next();
                return next.Position + "|" + (next == new Cursor(3)) + "|" + (next == new Cursor(2));
            }

            // A static member of the library, read by name.
            public static string StaticMemberRead() => string.Join(",", Palette.Names) + "|" + Palette.Names.Length;

            // A record of the SECOND project, which the app references only through the library.
            public static string RecordOfTheSecondProject()
            {
                var total = new Money(12.50m, "EUR").Plus(new Money(0.25m, "EUR"));
                return total.Amount + "|" + total.Currency + "|" + (total == new Money(12.75m, "EUR"));
            }

            // A record of the library that holds one of the second project, a long past 2^53, an
            // instant with its offset and a Guid: each read as what it is, and copied with `with`.
            public static string RecordHoldingARecord()
            {
                var id = Guid.Parse("3f2504e0-4f89-11d3-9a0c-0305e82c3301");
                var receipt = new Receipt("Order", new Money(1m, "EUR"), 9_007_199_254_740_993,
                    new DateTimeOffset(2026, 10, 12, 9, 30, 0, TimeSpan.FromHours(1)), id);
                var copy = receipt with { Title = "Copy" };
                return (receipt.Number + 1) + "|" + receipt.IssuedAt.Year + "|" + receipt.IssuedAt.Offset.Hours + "|"
                    + receipt.Total.Currency + "|" + copy.Title + "|" + (copy.Total == receipt.Total) + "|" + (copy.Id == id);
            }
        }
        """;

    private static readonly string[] Names =
        ["RecordBuiltAndCompared", "StaticMemberRead", "RecordOfTheSecondProject", "RecordHoldingARecord"];

    [SkippableFact]
    public void TypesOfAReferencedProject_AnswerAsDotNet()
    {
        var bun = JsExecutor.RequireBun();
        using var solution = SharedLibrary.Solution().Write("App", "Cases.cs", Cases);
        var built = solution.Build(bun: bun);
        built.Failed.Should().BeFalse($"the solution builds and bundles:\n{built.Errors}");

        var expected = InDotNet(solution);
        var actual = InTheBrowsersEngine(bun, solution, built);

        actual.Should().Equal(expected, "every case answers in JavaScript as it answers in .NET. The module of the cases:\n"
            + built.Module("Cases"));
    }

    [SkippableFact]
    public void HandedNoProjects_TheSameCasesAreNotBuilt()
    {
        // The control: an SDK that hands the compiler no list of projects, which is every build
        // before #624. A call of a method of the library's record is refused at the build (EQ2004),
        // and whatever a page wrote of the library, no module of it was ever written.
        var bun = JsExecutor.RequireBun();
        using var solution = SharedLibrary.Solution().Write("App", "Cases.cs", Cases);
        var built = solution.Build(handProjects: false, bun: bun);

        built.Failed.Should().BeTrue("a type read from an assembly has no module to call");
        built.Modules.Should().NotContain(["Cursor", "Money", "Palette", "Receipt"]);
    }

    /// <summary>The cases called on the assemblies MSBuild would have built, in a context of their
    /// own and in the invariant culture the other side prints in.</summary>
    private static string[] InDotNet(Solution solution)
    {
        var context = new AssemblyLoadContext("ReferencedProjectConformance", isCollectible: true);
        var culture = System.Globalization.CultureInfo.CurrentCulture;
        try
        {
            System.Globalization.CultureInfo.CurrentCulture = System.Globalization.CultureInfo.InvariantCulture;
            foreach (var library in new[] { "Domain", "Common" })
                context.LoadFromStream(new MemoryStream(File.ReadAllBytes(solution.AssemblyOf(library))));
            var cases = context.LoadFromStream(new MemoryStream(File.ReadAllBytes(solution.AssemblyOf("App"))))
                .GetType("App.Cases")!;
            return Names.Select(name => (string)cases.GetMethod(name)!.Invoke(null, null)!).ToArray();
        }
        finally
        {
            System.Globalization.CultureInfo.CurrentCulture = culture;
            context.Unload();
        }
    }

    /// <summary>The same cases called on the bundle the build wrote, by bun.</summary>
    private static string[] InTheBrowsersEngine(string bun, Solution solution, Built built)
    {
        // The runtime a module imports by its bare name, resolved the way a page's import map does.
        var shim = Path.Combine(solution.Root, "node_modules", "@equantic", "runtime");
        Directory.CreateDirectory(shim);
        File.WriteAllText(Path.Combine(shim, "package.json"), """{ "name": "@equantic/runtime", "type": "module", "main": "index.js" }""");
        File.WriteAllText(Path.Combine(shim, "index.js"), $"export * from '{ConformanceRunner.RuntimeJsUrl()}';\n");

        var bundle = Path.Combine(built.OutputDirectory, "Cases.js");
        File.Exists(bundle).Should().BeTrue($"the cases are an entry point of the bundle. Written: {string.Join(", ", Directory.GetFiles(built.OutputDirectory).Select(Path.GetFileName))}");
        var driver = Path.Combine(solution.Root, "run.mjs");
        File.WriteAllText(driver,
            $"import {{ Cases }} from '{new Uri(bundle).AbsoluteUri}';\n"
            + "const answer = (read) => { try { return String(read()); } catch (e) { return 'threw: ' + e; } };\n"
            + $"console.log(JSON.stringify([{string.Join(", ", Names.Select(name => $"answer(() => Cases.{char.ToLowerInvariant(name[0])}{name[1..]}())"))}]));\n");
        var (code, stdout, stderr) = JsExecutor.RunProcess(bun, ["run", driver], 30000);
        code.Should().Be(0, $"the bundle loads and runs:\n{stderr}\n{stdout}");
        return JsonSerializer.Deserialize<string[]>(stdout.Trim())!;
    }
}
