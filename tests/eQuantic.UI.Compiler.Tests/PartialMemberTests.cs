using System.Linq;
using System.Text.RegularExpressions;
using eQuantic.UI.Compiler;
using FluentAssertions;
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
