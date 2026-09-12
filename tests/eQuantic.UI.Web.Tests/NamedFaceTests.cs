using eQuantic.UI.Primitives;
using eQuantic.UI.Web;
using FluentAssertions;

namespace eQuantic.UI.Web.Tests;

/// <summary>
/// A theme that brands its type names a FACE, and the web quotes it in front of the stack the page
/// would have used anyway. Reported by the eQuantic Code IDE: a design handoff draws every metric
/// against IBM Plex Sans and JetBrains Mono, the SDK rendered the system faces, and nothing said so
/// — typography being most of what a UI looks like, that was the largest gap between the design and
/// what could be built.
/// </summary>
public class NamedFaceTests
{
    private static readonly IAppTheme Theme = PhotonTheme.Instance;

    private static string? FamilyOf(Text text) =>
        WebRealizer.Lower(text, Theme).Render().Attributes.GetValueOrDefault("style") is { } style
        && style.Contains("font-family")
            ? style[(style.IndexOf("font-family", StringComparison.Ordinal))..]
            : null;

    [Fact]
    public void NoFaceNamed_LeavesTheStackAlone()
    {
        var text = new Text("hello", TypeRole.BodyM);

        FamilyOf(text).Should().BeNull("an app with no opinion inherits the document's own face");
    }

    [Fact]
    public void ANamedFace_IsQuotedInFrontOfTheStackThePageWouldHaveUsed()
    {
        var text = new Text("hello", TypeRole.BodyM)
        {
            StyleOverride = Theme.Type(TypeRole.BodyM) with { Family = "IBM Plex Sans" },
        };

        var family = FamilyOf(text);
        family.Should().NotBeNull();
        // Quoted, because the common case has a space in it and an unquoted family is a parse error
        // that takes the whole declaration with it.
        family.Should().Contain("\"IBM Plex Sans\"");
        family.Should().Contain("--eq-font-family", "an unavailable face lands on the document's stack");
    }

    [Fact]
    public void ANamedMonoFace_FallsBackToTheMonoStack()
    {
        var text = new Text("code", TypeRole.BodyM)
        {
            StyleOverride = Theme.Type(TypeRole.BodyM) with { Family = "JetBrains Mono", Mono = true },
        };

        var family = FamilyOf(text);
        family.Should().Contain("\"JetBrains Mono\"");
        family.Should().Contain("--eq-font-mono", "a code face that is missing must not land on a proportional one");
    }

    /// <summary>A family with a quote in it would otherwise close the declaration early and take
    /// whatever follows with it.</summary>
    [Fact]
    public void AFaceNameWithAQuote_DoesNotEscapeItsDeclaration()
    {
        TokenCss.Face("Ha\"ck", mono: false).Should().StartWith("\"Ha\\\"ck\"");
    }
}
