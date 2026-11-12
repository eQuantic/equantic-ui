using eQuantic.UI.Primitives;
using eQuantic.UI.Web;
using FluentAssertions;

namespace eQuantic.UI.Web.Tests;

/// <summary>
/// The families with files a page's text names, collected while the server renders it (#1056), so the
/// server declares their faces in the page's head: once each, and nothing outside a render.
/// </summary>
public class FaceSinkTests
{
    private static readonly IAppTheme Theme = PhotonTheme.Instance;

    private static readonly FontFamily Probe = new("Probe Sans",
        new FontFile("Acme.App", "eQuantic.UI.Fonts/Probe.ttf", 400, 400));

    private static Text Named(FontFamily family) =>
        new("Handgloves", TypeRole.BodyM) { StyleOverride = Theme.Type(TypeRole.BodyM) with { Family = family } };

    [Fact]
    public void AFamilyATextNames_IsCollected_OnceHoweverOftenItIsNamed()
    {
        var channels = new RenderChannels();
        WebRealizer.Lower(Named(Probe), Theme, 1f, channels).Render();
        WebRealizer.Lower(Named(new FontFamily("Probe Sans", Probe.Files[0])), Theme, 1f, channels).Render();
        WebRealizer.Lower(Named(new FontFamily("Georgia")), Theme, 1f, channels).Render();

        channels.Faces.Families.Should().Equal([Probe], "an equal family is the same family, and one with no files is the platform's");
    }

    [Fact]
    public void ARender_CollectsNothingIntoAnother()
    {
        var mine = new RenderChannels();
        var theirs = new RenderChannels();

        WebRealizer.Lower(Named(Probe), Theme, 1f, mine).Render();
        var alone = () => WebRealizer.Lower(Named(Probe), Theme).Render();

        alone.Should().NotThrow("a lowering outside a render has nowhere to collect into, and needs nowhere");
        mine.Faces.Families.Should().Equal([Probe]);
        theirs.Faces.IsEmpty.Should().BeTrue("one render's faces are its own");
    }
}
