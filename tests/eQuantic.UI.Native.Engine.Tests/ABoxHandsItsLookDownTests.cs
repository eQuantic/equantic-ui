using eQuantic.UI.Native.Components;
using eQuantic.UI.Native.Engine;
using eQuantic.UI.Native.Framework;
using eQuantic.UI.Primitives;
using FluentAssertions;
using Xunit;

namespace eQuantic.UI.Native.Engine.Tests;

/// <summary>
/// The look a box hands the text and icons inside it (#1577, <see cref="BoxStyle.Content"/>), on Photon:
/// a text that names no role, no line limit and no alignment is measured and painted in the box's, and
/// an icon that names no size takes the box's, as Flutter's <c>DefaultTextStyle</c> and
/// <c>IconTheme</c> hand theirs down. The web half is <c>ABoxHandsItsLookDownRealizerTests</c>.
/// <para>
/// The oracle is the same text NAMING the look: a text that takes the box's role is measured "as if it
/// had named it", so the two measurements must be the same box, and each case also measures the text
/// with nothing stated, which must differ, or the fixture proves nothing.
/// </para>
/// </summary>
public class ABoxHandsItsLookDownTests
{
    private static readonly IAppTheme Theme = PhotonTheme.Instance;

    /// <summary>Long enough to wrap at <see cref="Width"/> in every role here.</summary>
    private const string Long = "The quick brown fox jumps over the lazy dog, again and again";

    private const float Width = 120;

    private static LayoutNode Laid(VisualNode root) =>
        LayoutEngine.Layout(root, 400, 400, new LayoutContext(Theme, ApproximateTextMeasurer.Instance));

    private static Box Within(ContentStyle? content, VisualNode child) =>
        new(new BoxStyle { Width = Width, Content = content }, child);

    private static IEnumerable<LayoutNode> Walk(LayoutNode node)
    {
        yield return node;
        foreach (var child in node.Children)
            foreach (var descendant in Walk(child))
                yield return descendant;
    }

    private static LayoutNode Node<T>(LayoutNode root) where T : VisualNode =>
        Walk(root).Single(node => node.Source is T);

    private static TextMeasurement Measured(VisualNode root) => Node<Text>(Laid(root)).Text!;

    private static RealizeResult Realize(VisualNode root, DisplayListBuilder builder) =>
        PhotonRealizer.Realize(root, 400, 400, Theme, ThemeMode.Light, ContrastLevel.Standard, builder);

    /// <summary>The placeholder bars a frame with no text service draws, one per line, top to bottom.</summary>
    private static List<Rect> Bars(VisualNode root)
    {
        var builder = new DisplayListBuilder();
        Realize(root, builder);
        return builder.Build().Commands.ToArray()
            .Where(command => command.Kind == DrawCommandKind.FillRRect)
            .Select(command => command.Shape.Rect)
            .OrderBy(rect => rect.Y)
            .ToList();
    }

    [Fact]
    public void ABoxsRoleAndLineLimit_AreTakenByATextThatNamesNone()
    {
        var taken = Measured(Within(new ContentStyle { Role = TypeRole.Label, MaxLines = 1 }, new Text(Long)));
        var named = Measured(Within(null, new Text(Long, TypeRole.Label, maxLines: 1)));
        var plain = Measured(Within(null, new Text(Long)));

        plain.Lines.Count.Should().BeGreaterThan(1, "with nothing stated the text wraps, or the fixture proves nothing");
        plain.LineHeight.Should().NotBe(named.LineHeight, "and BodyL's line is not Label's");
        taken.Lines.Should().HaveCount(1, "the box's line limit cuts it");
        taken.LineHeight.Should().Be(named.LineHeight, "it is measured in the box's role");
        taken.Width.Should().Be(named.Width, "exactly as if it had named both");
    }

    [Fact]
    public void ABoxsAlignment_IsTakenByATextThatNamesNone()
    {
        var taken = Bars(Within(new ContentStyle { Align = TextAlignment.End }, new Text("Hi")));
        var named = Bars(Within(null, new Text("Hi", align: TextAlignment.End)));
        var plain = Bars(Within(null, new Text("Hi")));

        plain.Should().ContainSingle().Which.X.Should().BeApproximately(0, 0.01f, "with nothing stated a line starts at the start");
        named.Should().ContainSingle().Which.X.Should().BeGreaterThan(1, "and an end-aligned one does not, or the fixture proves nothing");
        taken.Should().Equal(named, "the box's alignment is painted as if the text had named it");
    }

    [Fact]
    public void ABoxsIconSize_IsTakenByAnIconThatNamesNone()
    {
        Node<Icon>(Laid(Within(new ContentStyle { IconSize = IconSize.Sm }, new Icon(Icons.Search))))
            .Bounds.Width.Should().Be(IconSize.Sm, "measured at the box's size");
        Node<Icon>(Laid(Within(null, new Icon(Icons.Search)))).Bounds.Width.Should().Be(24, "and 24 where none is stated");
        Node<Icon>(Laid(Within(new ContentStyle { IconSize = IconSize.Sm }, new Icon(Icons.Search, IconSize.Lg))))
            .Bounds.Width.Should().Be(IconSize.Lg, "an icon that names its size keeps it");

        // Painted at the size it was measured at: the placeholder disc is the icon's box.
        var builder = new DisplayListBuilder();
        Realize(Within(new ContentStyle { IconSize = IconSize.Sm }, new Icon(Icons.Search)), builder);
        builder.Build().Commands.ToArray().Where(command => command.Kind == DrawCommandKind.FillRRect)
            .Should().ContainSingle().Which.Shape.Rect.Width.Should().Be(IconSize.Sm);
    }

    [Fact]
    public void ATextThatNamesItsOwnRole_KeepsThatRolesTypeWhole()
    {
        var look = new ContentStyle { Role = TypeRole.Label, Type = TypeStyle.OfSize(30, FontWeight.SemiBold) };

        var own = Measured(Within(look, new Text("Note", TypeRole.Caption)));
        var caption = Measured(Within(null, new Text("Note", TypeRole.Caption)));
        var took = Measured(Within(look, new Text("Note")));
        var styled = Measured(Within(null, new Text("Note", TypeRole.Label, styleOverride: look.Type)));

        took.LineHeight.Should().Be(styled.LineHeight, "a text that took the box's role takes the box's type too");
        took.LineHeight.Should().NotBe(caption.LineHeight, "which is not Caption's, or the fixture proves nothing");
        own.LineHeight.Should().Be(caption.LineHeight, "a text that names its role keeps that role's type, not the box's");
        own.Width.Should().Be(caption.Width);
    }

    [Fact]
    public void NestedBoxes_LayTheirLooksOverEachOther()
    {
        var nested = Measured(Within(new ContentStyle { Role = TypeRole.Label },
            new Box(new BoxStyle { Content = new ContentStyle { MaxLines = 1 } }, new Text(Long))));
        var named = Measured(Within(null, new Text(Long, TypeRole.Label, maxLines: 1)));

        nested.Lines.Should().HaveCount(1, "the inner box's line limit");
        nested.LineHeight.Should().Be(named.LineHeight, "over the outer box's role");
    }

    [Fact]
    public void NothingStated_DrawsAsBefore()
    {
        var plain = Measured(Within(new ContentStyle(), new Text(Long)));
        var bodyL = Measured(Within(null, new Text(Long, TypeRole.BodyL)));

        plain.LineHeight.Should().Be(bodyL.LineHeight, "BodyL");
        plain.Lines.Count.Should().Be(bodyL.Lines.Count, "with no line limit");
    }

    [Fact]
    public void AStringWhereANodeIsTaken_IsATextInTheBoxsLook()
    {
        var column = new Column { "Title" };
        var taken = Measured(Within(new ContentStyle { Role = TypeRole.Label }, column));
        var named = Measured(Within(null, new Column { new Text("Title", TypeRole.Label) }));

        column.Children.Should().ContainSingle().Which.Should().BeOfType<Text>().Which.Role.Should().BeNull();
        taken.LineHeight.Should().Be(named.LineHeight);
        taken.Width.Should().Be(named.Width);
    }

    /// <summary>A text the flex pass cuts is measured again, inside the row's measurement: the look of
    /// the box around the row is still in effect for it.</summary>
    [Fact]
    public void ATextTheRowCuts_IsMeasuredAgainInTheBoxsLook()
    {
        static Row Line(Text text) => new(gap: 0) { new Box(new BoxStyle { Width = 60 }), text };
        var taken = Measured(Within(new ContentStyle { Role = TypeRole.Label }, Line(new Text(Long))));
        var named = Measured(Within(null, Line(new Text(Long, TypeRole.Label))));

        taken.Lines.Should().HaveCount(1, "the row cut it to a line");
        taken.LineHeight.Should().Be(named.LineHeight, "and its re-measure kept the box's role");
        taken.Width.Should().Be(named.Width);
    }

    /// <summary>An overlay is laid out on its own against the viewport, outside the measurement of the
    /// box it stands in: the look of that box still reaches its layer, as the web lowers the layer in
    /// place.</summary>
    [Fact]
    public void AnOverlaysLayer_TakesTheLookWhereItStands()
    {
        RealizeResult Frame(ContentStyle? look, Text text) =>
            Realize(Within(look, new Overlay(new Box(new BoxStyle { Width = Width }, text))), new DisplayListBuilder());

        var taken = Frame(new ContentStyle { Role = TypeRole.Label, MaxLines = 1 }, new Text(Long));
        var named = Frame(null, new Text(Long, TypeRole.Label, maxLines: 1));

        var layer = Node<Text>(taken.OverlayRoots.Should().ContainSingle().Subject).Text!;
        var oracle = Node<Text>(named.OverlayRoots.Single()).Text!;
        layer.Lines.Should().HaveCount(1, "the box's line limit reaches the layer");
        layer.LineHeight.Should().Be(oracle.LineHeight, "and so does its role");
    }
}
