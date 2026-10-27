using eQuantic.UI.Primitives;
using eQuantic.UI.Web;
using FluentAssertions;

namespace eQuantic.UI.Web.Tests;

/// <summary>
/// Text is set in each of the nine weights on the web (#1082): the server writes a weight as its number,
/// in a text's own style and in a run's. The runtime's half is <c>measure-font.spec.ts</c>, where each
/// name the transpiler emits is read to the same number.
/// </summary>
public class FontWeightWebTests
{
    private static readonly IAppTheme Theme = PhotonTheme.Instance;

    private static string Render(VisualNode node) =>
        HtmlRenderer.RenderNode(WebRealizer.Lower(node, Theme).Render());

    [Theory]
    [InlineData(FontWeight.Thin, 100)]
    [InlineData(FontWeight.ExtraLight, 200)]
    [InlineData(FontWeight.Light, 300)]
    [InlineData(FontWeight.Regular, 400)]
    [InlineData(FontWeight.Medium, 500)]
    [InlineData(FontWeight.SemiBold, 600)]
    [InlineData(FontWeight.Bold, 700)]
    [InlineData(FontWeight.ExtraBold, 800)]
    [InlineData(FontWeight.Black, 900)]
    public void TextSetInAWeight_IsWrittenAtItsNumber(FontWeight weight, int number)
    {
        Render(new Text("Handgloves", TypeRole.BodyM) { StyleOverride = Theme.Type(TypeRole.BodyM) with { Weight = weight } })
            .Should().Contain($"font-weight: {number}");
        Render(new Text("", TypeRole.BodyM) { Spans = [new TextRun("Handgloves") { Weight = weight }] })
            .Should().Contain($"font-weight: {number}");
    }
}
