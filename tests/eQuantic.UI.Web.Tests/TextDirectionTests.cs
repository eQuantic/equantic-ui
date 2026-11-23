using System.Globalization;
using eQuantic.UI.Components;
using eQuantic.UI.Primitives;
using FluentAssertions;

namespace eQuantic.UI.Web.Tests;

/// <summary>
/// Text that runs right to left (#1568): a component reads its direction from the nearest
/// <see cref="Directionality"/> above it, or from its UI culture, which .NET says; and the web writes every
/// start and end as logical CSS, so a page and a subtree mirror under <c>dir="rtl"</c>, while a symmetric
/// value keeps its physical shorthand and the surfaces that place by physical x read left to right.
/// The TypeScript twin is <c>text-direction.spec.ts</c>; <c>component-parity</c> holds the two producers
/// to the same markup.
/// </summary>
public class TextDirectionTests
{
    private static readonly IAppTheme Theme = PhotonTheme.Instance;

    private static T InCulture<T>(string name, Func<T> read)
    {
        var before = CultureInfo.CurrentUICulture;
        CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(name);
        try
        {
            return read();
        }
        finally
        {
            CultureInfo.CurrentUICulture = before;
        }
    }

    private static HtmlNode Render(VisualNode node) => WebRealizer.Lower(node, Theme).Render();

    private static string StyleOf(HtmlNode node) => node.Attributes.TryGetValue("style", out var style) ? style ?? "" : "";

    // ---- the context --------------------------------------------------------------------------

    private sealed class Log
    {
        public List<string> Entries { get; } = new();
    }

    /// <summary>Says which way its text runs.</summary>
    private sealed class Reader(Log log, string name) : StatelessComponent
    {
        public override VisualNode Build(ComponentContext context)
        {
            log.Entries.Add($"{name} {context.TextDirection}");
            return new Text(name, TypeRole.BodyM);
        }
    }

    /// <summary>Reads nothing a subtree holds.</summary>
    private sealed class Bystander(Log log) : StatelessComponent
    {
        public override VisualNode Build(ComponentContext context)
        {
            log.Entries.Add("bystander");
            return new Text("bystander", TypeRole.BodyM);
        }
    }

    /// <summary>Holds a direction over a subtree its parent handed it, built once.</summary>
    private sealed class Holder(TextDirection first, VisualNode child) : StatefulComponent
    {
        private TextDirection _direction = first;

        public void Set(TextDirection direction) => SetState(() => _direction = direction);

        public override VisualNode Build(ComponentContext context) => new Directionality(_direction, child);
    }

    private static Element Frame(BuildOwner owner, VisualNode root, bool rebuildAll = false)
    {
        var element = owner.Build(root, new ComponentContext(Theme), 400f, rebuildAll);
        owner.EndFrame();
        return element;
    }

    [Theory]
    [InlineData("ar", TextDirection.RightToLeft)]
    [InlineData("he-IL", TextDirection.RightToLeft)]
    [InlineData("en", TextDirection.LeftToRight)]
    [InlineData("pt-BR", TextDirection.LeftToRight)]
    public void AFramesContext_ReadsItsUICulturesDirection(string culture, TextDirection expected)
    {
        InCulture(culture, () => new ComponentContext(Theme).TextDirection).Should().Be(expected,
            "with nothing above it, the direction is the UI culture's, as .NET's TextInfo says");
    }

    [Fact]
    public void APosition_ReadsTheNearestDirectionality_AndTheCulturesWithNoneAbove()
    {
        var log = new Log();
        var owner = new ComponentBoundary().Owner();

        InCulture("ar", () => Frame(owner, new Column
        {
            new Directionality(TextDirection.LeftToRight, new Reader(log, "code")),
            new Reader(log, "page"),
            new Directionality(TextDirection.LeftToRight,
                new Directionality(TextDirection.RightToLeft, new Reader(log, "nested"))),
        }, rebuildAll: true));

        log.Entries.Should().Equal("code LeftToRight", "page RightToLeft", "nested RightToLeft");
    }

    [Fact]
    public void ADirectionThatChanges_BuildsWhatReadsIt_AndNothingElse()
    {
        var log = new Log();
        var holder = new Holder(TextDirection.LeftToRight, new Column { new Reader(log, "reader"), new Bystander(log) });
        var owner = new ComponentBoundary().Owner();
        var root = InCulture("en", () => Frame(owner, new Column { holder }, rebuildAll: true));
        log.Entries.Should().Equal("reader LeftToRight", "bystander");

        log.Entries.Clear();
        ((Holder)((ComponentElement)root.ChildAt(0)!).Component).Set(TextDirection.RightToLeft);
        InCulture("en", () => Frame(owner, new Column { holder }));

        log.Entries.Should().Equal(["reader RightToLeft"], "the direction's dependents build, and a bystander does not");
    }

    // ---- the web ------------------------------------------------------------------------------

    [Theory]
    [InlineData(TextDirection.RightToLeft, "rtl")]
    [InlineData(TextDirection.LeftToRight, "ltr")]
    public void ADirectionality_LowersToAHostThatDrawsNoBox_AndSaysItsDirection(TextDirection direction, string dir)
    {
        var host = Render(new Directionality(direction, new Text("x", TypeRole.BodyM)));

        host.Tag.Should().Be("div");
        host.Attributes["dir"].Should().Be(dir);
        StyleOf(host).Should().Be("display: contents", "it adds no box to its parent's layout");
        host.Children.Should().ContainSingle().Which.Tag.Should().Be("span");
    }

    [Fact]
    public void APaddingLargerAtItsStart_IsWrittenAsLogicalPadding()
    {
        var style = StyleOf(Render(new Primitives.Box(new BoxStyle { Padding = new EdgeInsets(24, 8, 8, 4) })));

        style.Should().Contain("padding-block: 8px 4px").And.Contain("padding-inline: 24px 8px");
        style.Should().NotContain("padding:", "a physical shorthand would put the start on the left in every language");
    }

    [Fact]
    public void ASymmetricPadding_KeepsItsPhysicalShorthand()
    {
        // The classes of the catalog's symmetric paddings do not move: the shorthand reads the same either way.
        StyleOf(Render(new Primitives.Box(new BoxStyle { Padding = new EdgeInsets(16, 8, 16, 4) })))
            .Should().Contain("padding: 8px 16px 4px 16px").And.NotContain("padding-inline");
        StyleOf(Render(new Row(padding: new EdgeInsets(12, 0, 4, 0))))
            .Should().Contain("padding-inline: 12px 4px").And.Contain("padding-block: 0");
    }

    [Fact]
    public void ABorderAtOneInlineSide_IsWrittenAsLogicalWidths_AndOneAlongTheBlockStaysPhysical()
    {
        StyleOf(Render(new Primitives.Box(new BoxStyle
        {
            BorderWidth = 2, BorderColor = Theme.Border, BorderSides = BorderSides.Start,
        }))).Should().Contain("border-block-width: 0").And.Contain("border-inline-width: 2px 0")
            .And.NotContain("border-width:");

        StyleOf(Render(new Primitives.Box(new BoxStyle
        {
            BorderWidth = 1, BorderColor = Theme.Border, BorderSides = BorderSides.Bottom,
        }))).Should().Contain("border-width: 0 0 1px 0").And.NotContain("border-inline-width");
    }

    [Fact]
    public void APositionedStartAndEnd_AreMeasuredFromTheInlineSides()
    {
        var stack = new Primitives.Stack();
        stack.Add(new Positioned(new Text("x", TypeRole.BodyM), top: 4, end: 8, start: 16));

        var anchor = StyleOf(Render(stack).Children[0]);

        anchor.Should().Contain("inset-inline-start: 16px").And.Contain("inset-inline-end: 8px");
        anchor.Should().NotContain("left:").And.NotContain("right:");
    }

    [Fact]
    public void AShiftAcross_MovesTowardTheEnd_ThroughTheTokenSheetsSign()
    {
        var stack = new Primitives.Stack();
        stack.Add(new Positioned(new Text("x", TypeRole.BodyM)) { StartFraction = 0.5f, ShiftX = -0.5f, ShiftY = -1f });

        StyleOf(Render(stack).Children[0])
            .Should().Contain("transform: translate(calc(var(--eq-inline, 1) * -50%), -100%)");

        var sheet = ThemeStylesheet.Generate(Theme);
        sheet.Should().Contain("  --eq-inline: 1;").And.Contain("[dir=ltr] { --eq-inline: 1; }")
            .And.Contain("[dir=rtl] { --eq-inline: -1; }");
        sheet.IndexOf("[dir=rtl] { --eq-inline: -1; }", StringComparison.Ordinal)
            .Should().BeGreaterThan(sheet.IndexOf("  --eq-inline: 1;", StringComparison.Ordinal),
                "a dir rule ties with :root, so it has to come after it to win");
    }

    [Fact]
    public void AnAnchoredPanelsCentre_IsPhysicalOnBothHalves()
    {
        var sheet = ThemeStylesheet.Generate(Theme);

        sheet.Should().Contain(".eq-anchor-b-center { top: 100%; left: 50%; transform: translateX(-50%); }")
            .And.Contain(".eq-anchor-t-center { bottom: 100%; left: 50%; transform: translateX(-50%); }");
    }

    [Fact]
    public void ATableViewport_LaysOutLeftToRight_InAnArabicPage()
    {
        var table = new TableView(new UniformTableSpans(2, 28), new UniformTableSpans(2, 96),
            (row, column) => new Text($"{row}:{column}", TypeRole.Caption, Theme.TextPrimary));

        var host = InCulture("ar", () => Find(Render(new Directionality(TextDirection.RightToLeft, table)),
            node => node.Attributes.TryGetValue("role", out var role) && role is "table" or "grid" or "group"));

        host.Should().NotBeNull();
        host!.Attributes["dir"].Should().Be("ltr", "its cells stand at physical x and it scrolls by scrollLeft");
    }

    [Fact]
    public void TheComponentsThatPlaceByPhysicalX_BuildLeftToRight()
    {
        var context = new ComponentContext(Theme);

        Direction(new Mermaid("graph TD\n  A-->B").Build(context)).Should().Be(TextDirection.LeftToRight);
        Direction(new CodeBlock("var x = 1;", "csharp").Build(context)).Should().Be(TextDirection.LeftToRight);
        Direction(new CodeBlock("var x = 1;", "csharp") { Standalone = false }.Build(context))
            .Should().Be(TextDirection.LeftToRight);
        Direction(new CodeEditor("var x = 1;").Build(context)).Should().Be(TextDirection.LeftToRight);
    }

    private static TextDirection? Direction(VisualNode built) => (built as Directionality)?.Direction;

    private static HtmlNode? Find(HtmlNode node, Func<HtmlNode, bool> matches)
    {
        if (matches(node)) return node;
        foreach (var child in node.Children)
            if (Find(child, matches) is { } found) return found;
        return null;
    }
}
