using eQuantic.UI.Primitives;
using eQuantic.UI.Web;
using FluentAssertions;
using Xunit;

namespace eQuantic.UI.Web.Tests;

/// <summary>
/// What the document takes from the theme (#1038): the slots for the text face, the selection, the
/// placeholder and the scrollbars, their defaults, and how they cross to the browser.
/// </summary>
public class DocumentThemeTests
{
    private static readonly IAppTheme Photon = PhotonTheme.Instance;

    /// <summary>
    /// A theme that sets none of the slots draws as the SDK drew before they were the theme's: the
    /// code editor's selection was the focus colour at 28%, a field's placeholder the muted text, and
    /// a table's scrollbars the muted text at 55% over the subtle surface.
    /// </summary>
    [Fact]
    public void TheDefaults_AreTheValuesTheSdkDrewWith()
    {
        Photon.Selection.Should().Be(Photon.FocusRing.WithOpacity(0.28f));
        Photon.OnSelection.Should().BeNull("selected text keeps its own colour through a translucent wash");
        Photon.Placeholder.Should().Be(Photon.TextMuted);
        Photon.ScrollbarThumb.Should().Be(Photon.TextMuted.WithOpacity(0.55f));
        Photon.ScrollbarTrack.Should().Be(Photon.SurfaceSubtle);
        Photon.TextFamily.Should().BeNull("a theme with no opinion keeps the platform's own face");
    }

    [Fact]
    public void TheBridge_CarriesTheSlotsEveryThemeHas_AsSurfaces()
    {
        var json = ThemeBridge.SerializeJson(Photon);

        foreach (var surface in new[] { "selection", "placeholder", "scrollbarThumb", "scrollbarTrack" })
            json.Should().Contain($"\"{surface}\":", "every colour of the contract crosses as a surface");
        json.Should().NotContain("\"textFamily\"", "a theme that names no face sends the payload it always sent")
            .And.NotContain("\"onSelection\"");
    }

    [Fact]
    public void TheBridge_CarriesTheTextFaceAndTheSelectedTextsColour_WhenTheThemeNamesThem()
    {
        var theme = new DocumentTheme(Photon)
        {
            Text = "Atkinson Hyperlegible Next",
            SelectedText = new ColorToken(Color.White),
        };

        var json = ThemeBridge.SerializeJson(theme);

        json.Should().Contain("\"textFamily\":\"Atkinson Hyperlegible Next\"")
            .And.Contain("\"onSelection\":[[255,255,255,255],[255,255,255,255]]");
    }

    /// <summary>The text face reaches every text that named no face, as the code face reaches code.</summary>
    [Fact]
    public void AStyleThatNamesNoFace_TakesTheThemesTextFace()
    {
        var theme = new DocumentTheme(Photon) { Text = "Atkinson Hyperlegible Next", Code = "JetBrains Mono" };

        Photon.Type(TypeRole.BodyM).WithThemeFace(theme).Family.Should().Be("Atkinson Hyperlegible Next");
        (Photon.Type(TypeRole.BodyM) with { Mono = true }).WithThemeFace(theme).Family
            .Should().Be("JetBrains Mono", "monospaced text takes the code face, never the text face");
        (Photon.Type(TypeRole.Heading) with { Family = "Newsreader" }).WithThemeFace(theme).Family
            .Should().Be("Newsreader", "a role's own face stays");
        new Text("Cura", TypeRole.BodyL).Resolve(theme).Family.Should().Be("Atkinson Hyperlegible Next");
    }

    /// <summary>
    /// A paragraph in the text face with a run of code inside it: the run drops the family it
    /// inherited and takes the code face, so the words around it stay in the text face.
    /// </summary>
    [Fact]
    public void ARunOfCode_InsideAParagraphInTheTextFace_TakesTheCodeFace()
    {
        var theme = new DocumentTheme(Photon) { Text = "Atkinson Hyperlegible Next", Code = "JetBrains Mono" };
        var paragraph = new Text("run it", TypeRole.BodyM).Resolve(theme);

        new TextRun("dotnet build") { Mono = true }.Resolve(paragraph, theme).Family.Should().Be("JetBrains Mono");
        new TextRun("then").Resolve(paragraph, theme).Family.Should().Be("Atkinson Hyperlegible Next");
    }

    /// <summary>A theme over another, answering the document's slots it is given and the rest as the
    /// inner theme does.</summary>
    internal sealed class DocumentTheme(IAppTheme inner) : IAppTheme
    {
        public string? Text { get; init; }
        public string? Code { get; init; }
        public ColorToken? Wash { get; init; }
        public ColorToken? SelectedText { get; init; }
        public ColorToken? Hint { get; init; }
        public ColorToken? Thumb { get; init; }
        public ColorToken? Track { get; init; }

        public string? TextFamily => Text ?? inner.TextFamily;
        public string? MonoFamily => Code ?? inner.MonoFamily;
        public ColorToken Selection => Wash ?? inner.Selection;
        public ColorToken? OnSelection => SelectedText ?? inner.OnSelection;
        public ColorToken Placeholder => Hint ?? inner.Placeholder;
        public ColorToken ScrollbarThumb => Thumb ?? inner.ScrollbarThumb;
        public ColorToken ScrollbarTrack => Track ?? inner.ScrollbarTrack;

        public ColorToken Background => inner.Background;
        public ColorToken Surface => inner.Surface;
        public ColorToken SurfaceSubtle => inner.SurfaceSubtle;
        public ColorToken SurfaceHighlight => inner.SurfaceHighlight;
        public ColorToken Border => inner.Border;
        public ColorToken BorderStrong => inner.BorderStrong;
        public ColorToken TextPrimary => inner.TextPrimary;
        public ColorToken TextSecondary => inner.TextSecondary;
        public ColorToken TextMuted => inner.TextMuted;
        public ColorToken TextInverse => inner.TextInverse;
        public ColorToken FocusRing => inner.FocusRing;
        public ColorToken LinkColor => inner.LinkColor;
        public ColorToken Scrim => inner.Scrim;
        public float DisabledOpacity => inner.DisabledOpacity;
        public VariantColors Colors(Variant variant) => inner.Colors(variant);
        public TypeStyle Type(TypeRole role) => inner.Type(role);
        public ShadowSpec Elevation(int level) => inner.Elevation(level);
        public float Shape(ShapeScale scale) => inner.Shape(scale);
    }
}
