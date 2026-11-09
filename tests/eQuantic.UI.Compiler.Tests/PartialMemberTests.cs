using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using eQuantic.UI.Compiler;
using FluentAssertions;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace eQuantic.UI.Compiler.Tests;

/// <summary>
/// The two halves of a partial property, indexer or event are ONE member of the twin, written from the
/// half that implements it, by every emitter (#1172). The defining half was written beside the
/// implementation: a class held the property's name on each instance, over the accessors, so the setter
/// never ran; a record's constructor ran that setter and its equality counted the state twice; a partial
/// indexer was refused as an overload of itself; and a partial event was written as a field.
/// </summary>
public class PartialMemberTests
{
    private static CompilationResult Compile(string source, string name) =>
        new ComponentCompiler().CompileSource(source, "Probe.cs").Single(result => result.ComponentName == name);

    private const string Level =
        "public partial int Level { get; set; } public partial int Level { get => field; set => field = value * 2; }";

    /// <summary>A member held under <paramref name="name"/> as a value rather than through accessors: a
    /// field declared (<c>level!: number</c>, <c>declare level: number</c>), started by a constructor
    /// (<c>this.level = 0</c>), or compared as a record's state (<c>o.level</c>), where an accessor reads
    /// <c>level(</c> and its store is <c>$level</c>.</summary>
    private static string Held(string name) => $@"(?<![$\w]){name}!?\s*:|this\.{name}\s*=[^=]|\bo\.{name}\b";

    public static TheoryData<string> Properties => new()
    {
        "public sealed partial class Gauge { " + Level + " }",
        "public sealed partial record Gauge(int N) { " + Level + " }",
        "public partial struct Gauge { " + Level + " }",
        "public static partial class Gauge { public static partial int Level { get; set; } public static partial int Level { get => field; set => field = value * 2; } }",
        """
        using eQuantic.UI.Primitives;

        [Page("/probe")]
        public sealed partial class Gauge : StatefulComponent
        {
            public partial int Level { get; set; }
            public partial int Level { get => field; set => field = value * 2; }
            public override VisualNode Build(ComponentContext context) => new Text("" + Level, TypeRole.BodyM);
        }
        """,
    };

    [Theory]
    [MemberData(nameof(Properties))]
    public void APartialProperty_IsWrittenFromItsImplementation(string source)
    {
        var result = Compile(source, "Gauge");

        result.Errors.Should().BeEmpty();
        result.TypeScript.Should().Contain("level(").And.Contain("$level");
        result.TypeScript.Should().NotMatchRegex(Held("level"));
    }

    /// <summary>
    /// A defining half whose implementing half is in another declaration, a generated file's, stays in the
    /// twin of its own declaration: eqc writes one module per declaration (EQ1006), and left out, the
    /// property was in no twin at all, where it was held as a value of its own.
    /// </summary>
    [Fact]
    public void APartialPropertyImplementedInAnotherDeclaration_StaysInItsOwn()
    {
        var dir = Directory.CreateTempSubdirectory("eq-partial-members");
        try
        {
            File.WriteAllText(Path.Combine(dir.FullName, "Gauge.cs"),
                "public sealed partial class Gauge { public partial int Level { get; set; } public int Mark() => 1; }");
            File.WriteAllText(Path.Combine(dir.FullName, "Gauge.Level.g.cs"),
                "public sealed partial class Gauge { public partial int Level { get => field; set => field = value * 2; } }");
            var files = Directory.GetFiles(dir.FullName, "*.cs");
            var project = CSharpCompilation.Create("PartialMembersProbe",
                files.Select(file => CSharpSyntaxTree.ParseText(File.ReadAllText(file), path: file)),
                [TestReferences.Of(typeof(object).Assembly.Location)]);
            var compiler = new ComponentCompiler();
            compiler.SetProjectCompilation(project);

            var defining = compiler.CompileDirectory(dir.FullName).Single(result => result.TypeScript.Contains("mark("));

            defining.TypeScript.Should().MatchRegex(Held("level"));
        }
        finally
        {
            dir.Delete(recursive: true);
        }
    }

    [Theory]
    [InlineData("public sealed partial class Grid { public partial int this[int i] { get; } public partial int this[int i] { get => i * 2; } }")]
    [InlineData("public sealed partial record Grid(int N) { public partial int this[int i] { get; } public partial int this[int i] { get => i * N; } }")]
    [InlineData("public partial struct Grid { public partial int this[int i] { get; set; } public partial int this[int i] { get => i * 2; set { } } }")]
    public void APartialIndexer_IsOneIndexer(string source)
    {
        var result = Compile(source, "Grid");

        result.Errors.Should().BeEmpty();
        Regex.Matches(result.TypeScript, @"\bitem\(").Should().ContainSingle();
    }

    [Theory]
    [InlineData("public sealed partial class Bell { public partial event System.Action? Rung; public partial event System.Action? Rung { add { } remove { } } }")]
    [InlineData("public sealed partial record Bell(int N) { public partial event System.Action? Rung; public partial event System.Action? Rung { add { } remove { } } }")]
    public void APartialEvent_IsNoField(string source)
    {
        var result = Compile(source, "Bell");

        result.Errors.Should().BeEmpty();
        result.TypeScript.Should().NotMatchRegex(Held("rung"));
    }
}
