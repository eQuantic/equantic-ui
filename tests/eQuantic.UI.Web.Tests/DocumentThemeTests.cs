using eQuantic.UI.Components;
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
            Text = new("Atkinson Hyperlegible Next"),
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
        var theme = new DocumentTheme(Photon) { Text = new("Atkinson Hyperlegible Next"), Code = new("JetBrains Mono") };

        Photon.Type(TypeRole.BodyM).WithThemeFace(theme).Family.Should().Be(new FontFamily("Atkinson Hyperlegible Next"));
        (Photon.Type(TypeRole.BodyM) with { Mono = true }).WithThemeFace(theme).Family
            .Should().Be(new FontFamily("JetBrains Mono"), "monospaced text takes the code face, never the text face");
        (Photon.Type(TypeRole.Heading) with { Family = new("Newsreader") }).WithThemeFace(theme).Family
            .Should().Be(new FontFamily("Newsreader"), "a role's own face stays");
        new Text("Cura", TypeRole.BodyL).Resolve(theme).Family.Should().Be(new FontFamily("Atkinson Hyperlegible Next"));
    }

    /// <summary>
    /// A paragraph in the text face with a run of code inside it: the run drops the family it
    /// inherited and takes the code face, so the words around it stay in the text face.
    /// </summary>
    [Fact]
    public void ARunOfCode_InsideAParagraphInTheTextFace_TakesTheCodeFace()
    {
        var theme = new DocumentTheme(Photon) { Text = new("Atkinson Hyperlegible Next"), Code = new("JetBrains Mono") };
        var paragraph = new Text("run it", TypeRole.BodyM).Resolve(theme);

        new TextRun("dotnet build") { Mono = true }.Resolve(paragraph, theme).Family.Should().Be(new FontFamily("JetBrains Mono"));
        new TextRun("then").Resolve(paragraph, theme).Family.Should().Be(new FontFamily("Atkinson Hyperlegible Next"));
    }

    /// <summary>
    /// The web's document, written from the theme rather than from a block each app wrote: the body,
    /// the selection, every field's placeholder and the page's scrollbars, through the variables the
    /// theme's slots declare.
    /// </summary>
    [Fact]
    public void TheStylesheet_DrawsTheDocumentFromTheTheme()
    {
        var css = ThemeStylesheet.Generate(Photon);

        css.Should().Contain("body { margin: 0; background: var(--eq-color-background); color: var(--eq-color-text-primary); "
            + "font-family: var(--eq-font-family, system-ui, -apple-system, sans-serif); -webkit-font-smoothing: antialiased; "
            + "-moz-osx-font-smoothing: grayscale; }");
        css.Should().Contain("::selection { background: var(--eq-color-selection); }")
            .And.Contain("::placeholder { color: var(--eq-color-placeholder); opacity: 1; }");
        css.Should().NotContain("scrollbar-color",
            "with the default slots the page keeps the platform's scrollbars, so an app's own scrollbar CSS still applies");
        css.Should().Contain($"--eq-color-selection: {TokenCss.Value(Photon.Selection)};")
            .And.Contain($"--eq-color-placeholder: {TokenCss.Value(Photon.Placeholder)};")
            .And.Contain($"--eq-color-scrollbar-thumb: {TokenCss.Value(Photon.ScrollbarThumb)};")
            .And.Contain($"--eq-color-scrollbar-track: {TokenCss.Value(Photon.ScrollbarTrack)};");
        css.Should().NotContain("--eq-font-family:", "Photon names no text face, so the platform's own is the document's")
            .And.NotContain("--eq-color-on-selection", "and its selected text keeps its own colour");
    }

    /// <summary>A theme with an opinion on the scrollbars gets the page's too.</summary>
    [Fact]
    public void AThemesOwnScrollbars_AreThePagesScrollbars()
    {
        var css = ThemeStylesheet.Generate(new DocumentTheme(Photon) { Thumb = new ColorToken(new Color(0x1B, 0x5E, 0x4B, 255)) });

        css.Should().Contain(":root { scrollbar-color: var(--eq-color-scrollbar-thumb) var(--eq-color-scrollbar-track); }");
    }

    [Fact]
    public void AThemesTextFace_IsTheDocumentsFace()
    {
        var css = ThemeStylesheet.Generate(new DocumentTheme(Photon) { Text = new("Atkinson Hyperlegible Next") });

        css.Should().Contain("--eq-font-family: \"Atkinson Hyperlegible Next\", system-ui, -apple-system, sans-serif;",
            "the face is declared in front of the platform's own, which a browser without it falls back to");
    }

    [Fact]
    public void AThemesSelectedTextColour_IsTheSelectionsColour()
    {
        var css = ThemeStylesheet.Generate(new DocumentTheme(Photon)
        {
            Wash = new ColorToken(new Color(0x1B, 0x5E, 0x4B, 255)),
            SelectedText = new ColorToken(Color.White),
        });

        css.Should().Contain("--eq-color-on-selection: #ffffff;")
            .And.Contain("::selection { background: var(--eq-color-selection); color: var(--eq-color-on-selection); }");
    }

    /// <summary>
    /// A table's scrollbars, which the web draws as elements of its own, take the theme's scrollbar
    /// colours through the variables the stylesheet declares, as Photon takes them from the slots.
    /// The thumb used to be the muted text with the element's opacity at 55%, which is the slot's
    /// default now, so the default draws the same.
    /// </summary>
    [Fact]
    public void ATablesScrollbars_AreDrawnFromTheScrollbarSlots()
    {
        var table = new TableView(new UniformTableSpans(100, 28), new UniformTableSpans(10, 96),
            (row, column) => new Text($"{row}:{column}", TypeRole.BodyM));

        var styles = Walk(WebRealizer.Lower(table, Photon).Render())
            .Select(n => n.Attributes.GetValueOrDefault("style") ?? "").ToList();

        styles.Should().Contain(s => s.Contains("background: var(--eq-color-scrollbar-track)"))
            .And.Contain(s => s.Contains("background: var(--eq-color-scrollbar-thumb)") && !s.Contains("opacity"));
    }

    private static IEnumerable<HtmlNode> Walk(HtmlNode node)
    {
        yield return node;
        foreach (var child in node.Children)
            foreach (var descendant in Walk(child))
                yield return descendant;
    }

    /// <summary>A theme over another, answering the document's slots it is given and the rest as the
    /// inner theme does.</summary>
    internal sealed class DocumentTheme(IAppTheme inner) : IAppTheme
    {
        public FontFamily? Text { get; init; }
        public FontFamily? Code { get; init; }
        public ColorToken? Wash { get; init; }
        public ColorToken? SelectedText { get; init; }
        public ColorToken? Hint { get; init; }
        public ColorToken? Thumb { get; init; }
        public ColorToken? Track { get; init; }

        public FontFamily? TextFamily => Text ?? inner.TextFamily;
        public FontFamily? MonoFamily => Code ?? inner.MonoFamily;
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
