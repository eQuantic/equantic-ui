using eQuantic.UI.Primitives;
using FluentAssertions;
using Xunit;

namespace eQuantic.UI.Web.Tests;

/// <summary>
/// The look a box hands the text and icons inside it (#1577, <see cref="BoxStyle.Content"/>), as the
/// server lowers it: a text that names no role, no line limit and no alignment is lowered with the box's,
/// as if it had named them (its <c>eq-type-*</c> class, its clamp, its <c>text-align</c>), and an icon that
/// names no size takes the box's. Nothing rides CSS inheritance: the box itself writes no type. The
/// Photon half is <c>ABoxHandsItsLookDownTests</c>.
/// </summary>
public class ABoxHandsItsLookDownRealizerTests
{
    private static readonly IAppTheme Theme = PhotonTheme.Instance;

    private static HtmlElement Lower(VisualNode node) => WebRealizer.Lower(node, Theme);

    private static Box Within(ContentStyle? content, VisualNode child) =>
        new(new BoxStyle { Content = content }, child);

    private static IEnumerable<HtmlElement> Walk(HtmlElement element)
    {
        yield return element;
        foreach (var child in element.Children.OfType<HtmlElement>())
            foreach (var descendant in Walk(child))
                yield return descendant;
    }

    /// <summary>The element a text lowered to: the one carrying a type class.</summary>
    private static HtmlElement TextOf(VisualNode root) =>
        Walk(Lower(root)).Single(element => element.ClassName?.StartsWith("eq-type-") == true);

    private static HtmlElement SvgOf(VisualNode root) =>
        Walk(Lower(root)).Single(element => element is RealizedElement { Tag: "svg" });

    [Fact]
    public void ABoxsRoleAndLineLimit_AreTakenByATextThatNamesNone()
    {
        var text = TextOf(Within(new ContentStyle { Role = TypeRole.Label, MaxLines = 1 }, new Text("Save")));

        text.ClassName.Should().Be("eq-type-label", "the text is lowered with the role it took");
        text.Style!.TextOverflow.Should().Be("ellipsis", "and cut to the box's one line");
        text.Style.Overflow.Should().Be("hidden");
    }

    [Fact]
    public void ABoxsMultiLineLimitAndAlignment_AreTakenByATextThatNamesNone()
    {
        var text = TextOf(Within(new ContentStyle { MaxLines = 3, Align = TextAlignment.Center }, new Text("Body")));

        text.Style!.WebkitLineClamp.Should().Be("3");
        text.Style.TextAlign.Should().Be(TextAlign.Center);
        text.ClassName.Should().Be("eq-type-bodyl", "a box that states no role leaves BodyL");
    }

    [Fact]
    public void ABoxsIconSize_IsTakenByAnIconThatNamesNone()
    {
        SvgOf(Within(new ContentStyle { IconSize = IconSize.Sm }, new Icon(Icons.Search))).Style!.Width
            .Should().Be("16px");
        SvgOf(Within(new ContentStyle { IconSize = IconSize.Sm }, new Icon(Icons.Search, IconSize.Lg))).Style!.Width
            .Should().Be("32px", "an icon that names its size keeps it");
        SvgOf(Within(null, new Icon(Icons.Search))).Style!.Width.Should().Be("24px", "and 24 where none is stated");
    }

    [Fact]
    public void ATextThatNamesItsOwnRole_KeepsThatRolesTypeWhole()
    {
        var look = new ContentStyle { Role = TypeRole.Label, Type = TypeStyle.OfSize(15, FontWeight.SemiBold) };

        var own = TextOf(Within(look, new Text("Note", TypeRole.Caption)));
        own.ClassName.Should().Be("eq-type-caption");
        own.Style!.FontSize.Should().BeNull("the box's type was written for the box's role");
        own.Style.FontWeight.Should().BeNull();

        var took = TextOf(Within(look, new Text("Save")));
        took.ClassName.Should().Be("eq-type-label");
        took.Style!.FontSize.Should().Be("15px", "a text that took the box's role takes its type too");
        took.Style.FontWeight.Should().Be("600");
    }

    [Fact]
    public void NestedBoxes_LayTheirLooksOverEachOther()
    {
        var text = TextOf(Within(new ContentStyle { Role = TypeRole.Label },
            Within(new ContentStyle { MaxLines = 1 }, new Text("Save"))));

        text.ClassName.Should().Be("eq-type-label", "the outer box's role");
        text.Style!.TextOverflow.Should().Be("ellipsis", "under the inner box's line limit");
    }

    [Fact]
    public void ALookStopsAtTheBoxThatStatesIt()
    {
        var row = new Row(gap: 0) { Within(new ContentStyle { Role = TypeRole.Label }, new Text("Inside")), new Text("Beside") };

        var texts = Walk(Lower(row)).Where(element => element.ClassName?.StartsWith("eq-type-") == true).ToList();
        texts.Select(text => text.ClassName).Should().Equal("eq-type-label", "eq-type-bodyl");
    }

    [Fact]
    public void NothingStated_LowersAsBefore()
    {
        var text = TextOf(Within(new ContentStyle(), new Text("Body")));

        text.ClassName.Should().Be("eq-type-bodyl");
        text.Style!.TextOverflow.Should().BeNull();
        text.Style.WebkitLineClamp.Should().BeNull();
        text.Style.TextAlign.Should().BeNull();
        text.Style.FontSize.Should().BeNull();
    }

    [Fact]
    public void AStringWhereANodeIsTaken_IsATextInTheBoxsLook()
    {
        var text = TextOf(Within(new ContentStyle { Role = TypeRole.Label }, new Column { "Title" }));

        text.ClassName.Should().Be("eq-type-label");
        text.InnerHtml.Should().Be("Title");
    }

    [Fact]
    public void ANullString_IsNoNode_NeverATextOfNothing()
    {
        string? description = null;
        VisualNode? subtitle = description;

        subtitle.Should().BeNull("an optional slot fed from a string? stays empty when the string is, as with null written");
    }
}
