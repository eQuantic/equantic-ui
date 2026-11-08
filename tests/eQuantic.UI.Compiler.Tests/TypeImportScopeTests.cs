using eQuantic.UI.Compiler.Services;
using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace eQuantic.UI.Compiler.Tests;

/// <summary>
/// A type reached through its namespace or an alias is imported only where the browser has a twin of it
/// (#625): a .NET type has none, so naming it imports nothing, never a module of the app's that shares its
/// simple name. `new Values()` over `using Values = List&lt;int&gt;` imported the app's own `List`, and
/// `System.Math.PI` the app's `Math`, which hid the browser's (found by Copilot's second review of #705).
/// </summary>
public class TypeImportScopeTests
{
    [Fact]
    public void ADotNetTypeNamedThroughAnAliasOrItsNamespace_ImportsNoModuleOfItsName()
    {
        var room = CompileScanned("""
            using Values = System.Collections.Generic.List<int>;
            using M = System.Math;

            namespace App
            {
                public class List { public int N = 1; }
                public class Math { public int N = 2; }

                public static class Room
                {
                    public static int Counted() { var v = new Values { 1, 2 }; return v.Count; }
                    public static double Pi() => System.Math.PI + M.E;
                }
            }
            """, "Room");

        room.Errors.Should().BeEmpty();
        room.TypeScript.Should().NotContain("from \"./List\"").And.NotContain("from \"./Math\"").And.NotContain("from \"./Math$\"");
    }

    /// <summary>
    /// A type's name inside a string the module writes is text, never a reference to the type (#824): a
    /// constant read off <c>Copy</c> is inlined as <c>'Copy'</c>, and the module imported <c>./Copy</c>,
    /// a class it never uses, as a page that reads a string imported the catalogue's class from the id in
    /// <c>$eq.str('Strings', …)</c>. Since #624 an import is what compiles a referenced project's type,
    /// so a word in a string compiled a type nothing used.
    /// </summary>
    [Fact]
    public void ATypesNameInsideAString_ImportsNoModuleOfIt()
    {
        var label = CompileBuilt("""
            using eQuantic.UI.Primitives;

            namespace App;

            public static class Copy { public const string Title = "Copy"; public static string Live() => "x"; }

            public sealed class Label : StatelessComponent
            {
                public override VisualNode Build(ComponentContext context) => new Text(Copy.Title, TypeRole.BodyM);
            }
            """, "Label");

        label.Errors.Should().BeEmpty();
        label.TypeScript.Should().Contain("'Copy'").And.NotContain("./Copy");
    }

    /// <summary>
    /// A vocabulary type the runtime ships no twin for is refused however it is named (Copilot's second
    /// review of #705). <c>CurveEvaluator</c> lives in a namespace the runtime provides, so its name
    /// routed to <c>@equantic/runtime</c> written bare or qualified, and through an alias once this change
    /// resolved aliases, and the module died at load on an export the bundle has not. It carries
    /// <c>[ServerOnly]</c> now, as <c>RRect</c> and <c>Matrix2D</c> do, so each spelling fails the build
    /// where it is written instead.
    /// </summary>
    [Theory]
    [InlineData("CurveEvaluator.Ease(curve, t)")]
    [InlineData("eQuantic.UI.Primitives.CurveEvaluator.Ease(curve, t)")]
    [InlineData("Ev.Ease(curve, t)")]
    public void AVocabularyTypeTheRuntimeShipsNoTwinFor_IsRefusedHoweverItIsNamed(string call)
    {
        var source = $$"""
            using eQuantic.UI.Primitives;
            using Ev = eQuantic.UI.Primitives.CurveEvaluator;

            namespace App;

            public static class Curves
            {
                public static float At(Curve curve, float t) => {{call}};
            }
            """;

        var curves = CompileWithVocabulary(source, "Curves");

        curves.Errors.Should().Contain(error => error.Code == "EQ2010",
            "the runtime exports no CurveEvaluator, so naming it is refused at the build");
        curves.TypeScript.Should().NotMatchRegex(@"import \{[^}]*\bCurveEvaluator\b",
            "and no module imports a name the bundle has not");
    }

    /// <summary>A file compiled as eqc compiles a project's: scanned by the dependency resolver first, whose
    /// module set decides every import, then compiled with it.</summary>
    private static CompilationResult CompileScanned(string source, string component)
    {
        var dir = Directory.CreateTempSubdirectory("eq-imports-").FullName;
        try
        {
            var path = Path.Combine(dir, "Probe.cs");
            File.WriteAllText(path, source);
            var resolver = new ComponentDependencyResolver();
            resolver.ScanSourceDirectories([dir]);
            var compiler = new ComponentCompiler { SymbolsAreAuthoritative = false };
            compiler.SetDependencyResolver(resolver);
            return compiler.CompileFile(path).Single(result => result.ComponentName == component);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    /// <summary>A file compiled as an app's build compiles it: against the real vocabulary assembly, with
    /// the project's scan made from the same compilation.</summary>
    private static CompilationResult CompileBuilt(string source, string component)
    {
        var path = Path.Combine(Path.GetTempPath(), "eq-imports-built", "Probe.cs");
        var compilation = CSharpCompilation.Create("BuiltProbe",
            [CSharpSyntaxTree.ParseText(source, path: path)], VocabularyReferences(),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        var compiler = new ComponentCompiler();
        compiler.SetProjectCompilation(compilation);
        compiler.SetDependencyResolver(ComponentDependencyResolver.From(compilation));
        return compiler.CompileSource(source, path).Single(result => result.ComponentName == component);
    }

    private static IEnumerable<MetadataReference> VocabularyReferences() =>
        ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
            .Split(Path.PathSeparator)
            .Where(reference => reference.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
            .Select(reference => (MetadataReference)TestReferences.Of(reference))
            .Append(TestReferences.Of(typeof(eQuantic.UI.Primitives.Curve).Assembly.Location));

    /// <summary>A file compiled against the real vocabulary assembly, which the scanned compile above does
    /// not bind: a Primitives type has to resolve to its symbol for the routing to be asked at all.</summary>
    private static CompilationResult CompileWithVocabulary(string source, string component)
    {
        var path = Path.Combine(Path.GetTempPath(), "eq-imports-vocabulary", "Probe.cs");
        var compilation = CSharpCompilation.Create("VocabularyProbe",
            [CSharpSyntaxTree.ParseText(source, path: path)], VocabularyReferences(),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        var compiler = new ComponentCompiler { SymbolsAreAuthoritative = false };
        compiler.SetProjectCompilation(compilation);
        return compiler.CompileSource(source, path).Single(result => result.ComponentName == component);
    }
}
