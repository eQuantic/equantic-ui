using System.Linq;
using eQuantic.UI.Compiler;
using FluentAssertions;
using Xunit;

namespace eQuantic.UI.Compiler.Tests;

/// <summary>
/// A component's indexer is written into its twin on every component path (#1513): a concrete component,
/// an abstract base that holds nothing else the emitter counts, and a primitive, an <c>HtmlElement</c>.
/// The two last were written no <c>item</c> or <c>setItem</c>, and every access bound to the indexer
/// called a method nothing defined.
/// </summary>
public class ComponentTwinMemberTests
{
    private const string Indexer = "public int this[int i] { get => _c[i]; set => _c[i] = value; }";

    public static TheoryData<string, string> EveryComponentPath => new()
    {
        {
            "Grid",
            """
            using eQuantic.UI.Primitives;
            public abstract class Grid : StatelessComponent { protected int[] _c = new int[3]; INDEXER }
            """.Replace("INDEXER", Indexer)
        },
        {
            "Strip",
            """
            using eQuantic.UI.Web;
            public sealed class Strip : HtmlElement { public int[] _c { get; set; } INDEXER public override HtmlNode Render() => new HtmlNode { Tag = "span" }; }
            """.Replace("INDEXER", Indexer)
        },
        {
            "Cells",
            """
            using eQuantic.UI.Primitives;
            public sealed class Cells : StatelessComponent
            {
                protected int[] _c = new int[3];
                INDEXER
                public override VisualNode Build(ComponentContext context) => new Text("cells", TypeRole.BodyM);
            }
            """.Replace("INDEXER", Indexer)
        },
    };

    [Theory]
    [MemberData(nameof(EveryComponentPath))]
    public void AComponentsIndexer_IsWrittenOnEveryPath(string name, string source)
    {
        var result = new ComponentCompiler().CompileSource(source, "Probe.cs").Single(module => module.ComponentName == name);

        result.Errors.Should().BeEmpty();
        result.TypeScript.Should().MatchRegex(@"\bitem\(i\b").And.MatchRegex(@"\bsetItem\(i\b");
    }
    /// <summary>An abstract base's statics that initialize in order are written into its twin (#1513): the base
    /// the concrete branch's condition skips held no <c>seed</c>, and a subclass read <c>undefined</c>.</summary>
    [Fact]
    public void AnAbstractBasesOrderedStatic_IsWrittenIntoItsTwin()
    {
        const string source = """
            using eQuantic.UI.Primitives;
            public abstract class Board : StatelessComponent { protected static readonly int[] Seed = new[] { 1, 2, 3 }; }
            """;

        var result = new ComponentCompiler().CompileSource(source, "Probe.cs").Single(module => module.ComponentName == "Board");

        result.Errors.Should().BeEmpty();
        result.TypeScript.Should().MatchRegex(@"\bseed\b", "the static is the twin's");
        result.TypeScript.Should().MatchRegex(@"1,\s*2,\s*3", "and starts with its initializer:\n" + result.TypeScript);
    }

    /// <summary>A primitive's bodied and static properties are written into its twin (#1513): its branch wrote
    /// neither, so <c>s.Twice</c> read <c>undefined</c> and <c>Strip.Count</c> too.</summary>
    [Fact]
    public void APrimitivesBodiedAndStaticProperties_AreWrittenIntoItsTwin()
    {
        const string source = """
            using eQuantic.UI.Web;
            public sealed class Strip : HtmlElement
            {
                public int Last { get; set; }
                public int Twice => Last * 2;
                public static int Count { get; set; } = 7;
                public override HtmlNode Render() => new HtmlNode { Tag = "span" };
            }
            """;

        var result = new ComponentCompiler().CompileSource(source, "Probe.cs").Single(module => module.ComponentName == "Strip");

        result.Errors.Should().BeEmpty();
        result.TypeScript.Should().MatchRegex(@"get twice\(\)", "the bodied property is a getter:\n" + result.TypeScript);
        result.TypeScript.Should().MatchRegex(@"static count\b[^;]*=\s*7", "the static starts at its initializer:\n" + result.TypeScript);
    }
}
