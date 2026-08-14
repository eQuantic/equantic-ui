using eQuantic.UI.Design;
using FluentAssertions;

namespace eQuantic.UI.Design.Tests;

/// <summary>
/// What may be DONE with the node an origin names.
/// <para>
/// The tiers exist because a canvas that treats every rendered element as a free-standing object
/// will eventually offer to delete a row that has no separate existence — it is one expression in a
/// loop — or to move a node that is written in another method entirely. An editor that corrupts a
/// file is worse than one that says "this comes from a loop at line 243, here it is".
/// </para>
/// <para>
/// This is computable rather than guessed only because an origin is an EXACT span: "is there a loop
/// between this expression and the Build it lives in" is a question the syntax tree answers.
/// </para>
/// </summary>
public class OriginTierTests
{
    private const string Path = "/app/Screens/Probe.cs";

    private const string Source = """
        using eQuantic.UI.Core;
        using eQuantic.UI.Primitives;

        [Page("/probe")]
        public sealed class Probe : StatefulComponent
        {
            private string? _notice;
            private readonly string[] _rows = { "a", "b" };

            public override VisualNode Build(ComponentContext context)
            {
                var column = new Column(gap: Space.S2);
                column.Add(new Text("title", TypeRole.Heading, context.Theme.TextPrimary));
                foreach (var row in _rows)
                {
                    column.Add(new Text(row, TypeRole.BodyM, context.Theme.TextPrimary));
                }
                if (_notice is { } notice)
                {
                    column.Add(new Banner(Variant.Info, notice));
                }
                column.Add(Footer(context));
                return column;
            }

            private VisualNode Footer(ComponentContext context) =>
                new Text("footer", TypeRole.Caption, context.Theme.TextMuted);
        }
        """;

    /// <summary>Zero-based, like the editor and like the stamp.</summary>
    private static string OriginAt(int line, int column, string path = Path) =>
        $"{path}|{line}:{column}|{line}:{column + 1}";

    private static OriginTier Classify(string origin) => new DesignSession().Classify(Path, Source, origin);

    /// <summary>The line, zero-based, whose text contains <paramref name="fragment"/>.</summary>
    private static int LineOf(string fragment)
    {
        var lines = Source.Replace("\r\n", "\n").Split('\n');
        var index = Array.FindIndex(lines, line => line.Contains(fragment, StringComparison.Ordinal));
        index.Should().BeGreaterThanOrEqualTo(0, $"the probe source should contain '{fragment}'");
        return index;
    }

    [Fact]
    public void ANodeBuiltStraightInBuild_IsLiteral()
    {
        var line = LineOf("new Text(\"title\"");
        var tier = Classify(OriginAt(line, Source.Split('\n')[line].IndexOf("new Text", StringComparison.Ordinal)));

        tier.Tier.Should().Be("literal");
        tier.Reason.Should().Contain("unconditionally");
    }

    /// <summary>
    /// A row built five hundred times has ONE source site. "Which of the five hundred?" is the wrong
    /// question — the loop body is the only editable thing there is.
    /// </summary>
    [Fact]
    public void ANodeBuiltInALoop_IsDerived()
    {
        var line = LineOf("new Text(row,");
        var tier = Classify(OriginAt(line, Source.Split('\n')[line].IndexOf("new Text", StringComparison.Ordinal)));

        tier.Tier.Should().Be("derived");
        tier.Reason.Should().Contain("loop");
    }

    [Fact]
    public void ANodeBuiltInAConditional_IsDerived()
    {
        var line = LineOf("new Banner(");
        var tier = Classify(OriginAt(line, Source.Split('\n')[line].IndexOf("new Banner", StringComparison.Ordinal)));

        tier.Tier.Should().Be("derived");
        tier.Reason.Should().Contain("conditional");
    }

    /// <summary>
    /// The common case in real screens, and the reason this tier is not an edge case: a page is
    /// twenty-odd helper methods deep, so most of what you can see is written somewhere other than
    /// Build. Naming the member is the useful answer — it is where the reader has to go.
    /// </summary>
    [Fact]
    public void ANodeBuiltByAHelperMethod_IsForeign_AndNamesIt()
    {
        var line = LineOf("new Text(\"footer\"");
        var tier = Classify(OriginAt(line, Source.Split('\n')[line].IndexOf("new Text", StringComparison.Ordinal)));

        tier.Tier.Should().Be("foreign");
        tier.Member.Should().Be("Footer");
        tier.Reason.Should().Contain("Footer()");
    }

    /// <summary>Another file is foreign before anything is parsed: the buffer in hand is not its
    /// source, so nothing here can say more than where to look.</summary>
    [Fact]
    public void ANodeFromAnotherFile_IsForeign_AndNamesTheFile()
    {
        var tier = Classify(OriginAt(10, 8, "/app/Screens/ConsoleShell.cs"));

        tier.Tier.Should().Be("foreign");
        tier.Member.Should().Be("ConsoleShell.cs");
        tier.Reason.Should().Contain("not the file being previewed");
    }

    /// <summary>A malformed or out-of-range origin must answer, not throw: it arrives from a DOM
    /// attribute, and the preview has to survive whatever is in one.</summary>
    [Theory]
    [InlineData("nonsense")]
    [InlineData("/app/Screens/Probe.cs|9999:0|9999:1")]
    [InlineData("/app/Screens/Probe.cs|x:y|z:w")]
    public void AnUnusableOrigin_IsAnsweredRatherThanThrown(string origin)
    {
        var tier = Classify(origin);

        tier.Tier.Should().BeOneOf("literal", "derived", "foreign");
        tier.Reason.Should().NotBeNullOrWhiteSpace();
    }
}
