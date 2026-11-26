using eQuantic.UI.Email;
using eQuantic.UI.Primitives;
using FluentAssertions;
using Xunit;

namespace eQuantic.UI.Email.Tests;

/// <summary>
/// The look a box hands the text inside it (#1577, <see cref="BoxStyle.Content"/>), in an email: a text
/// that names no role or alignment is written in the box's, as if it had named it, and a line limit the
/// box states is as unrealizable here as one the text names. The oracle is the same text naming the look.
/// </summary>
public class ABoxHandsItsLookDownEmailTests
{
    private static readonly IAppTheme Theme = PhotonTheme.Instance;

    private static string Html(VisualNode node) => EmailRealizer.Lower(node, Theme);

    private static Box Within(ContentStyle? content, VisualNode child) =>
        new(new BoxStyle { Content = content }, child);

    [Fact]
    public void ABoxsRoleAndAlignment_AreTakenByATextThatNamesNone()
    {
        var taken = Html(Within(new ContentStyle { Role = TypeRole.Heading, Align = TextAlignment.Center }, new Text("Welcome")));
        var named = Html(Within(null, new Text("Welcome", TypeRole.Heading, align: TextAlignment.Center)));
        var plain = Html(Within(null, new Text("Welcome")));

        plain.Should().NotBe(named, "with nothing stated the text is BodyL at the start, or the fixture proves nothing");
        taken.Should().Be(named);
    }

    [Fact]
    public void ATextThatNamesItsOwnRole_KeepsThatRolesTypeWhole()
    {
        var look = new ContentStyle { Role = TypeRole.Label, Type = TypeStyle.OfSize(15, FontWeight.SemiBold) };

        Html(Within(look, new Text("Note", TypeRole.Caption))).Should().Be(Html(Within(null, new Text("Note", TypeRole.Caption))));
        Html(Within(look, new Text("Save"))).Should().Contain("font-size: 15px").And.Contain("font-weight: 600");
    }

    [Fact]
    public void NestedBoxes_LayTheirLooksOverEachOther_AndAStringIsAText()
    {
        var nested = Html(Within(new ContentStyle { Role = TypeRole.Heading },
            Within(new ContentStyle { Align = TextAlignment.End }, new Column { "Welcome" })));
        var named = Html(Within(null, Within(null, new Column { new Text("Welcome", TypeRole.Heading, align: TextAlignment.End) })));

        nested.Should().Be(named);
    }

    [Fact]
    public void ABoxsLineLimit_IsRefusedAsATextsIs()
    {
        var act = () => Html(Within(new ContentStyle { MaxLines = 1 }, new Text("Save")));

        act.Should().Throw<NotSupportedException>().WithMessage("*MaxLines*");
    }

    [Fact]
    public void NothingStated_WritesAsBefore() =>
        Html(Within(new ContentStyle(), new Text("Body"))).Should().Be(Html(Within(null, new Text("Body", TypeRole.BodyL))));
}
