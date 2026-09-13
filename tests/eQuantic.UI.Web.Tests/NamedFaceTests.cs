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

    // ---- What may be spelled as a family ------------------------------------------------------

    /// <summary>
    /// The rule, against the SAME fixture the TypeScript spec reads. Two hand-written tables would
    /// drift, and the drift lands as a hydration mismatch: SSR emits <c>font-family</c> through this
    /// predicate and the client emits it through <c>isWellFormedFace</c>.
    /// </summary>
    [Fact]
    public void TheFamilyRule_IsTheSameRuleTheClientHolds()
    {
        var disagreed = FaceCases()
            .Where(c => FaceName.IsWellFormed(c.Family) != c.WellFormed)
            .Select(c => $"{Printable(c.Family)} (expected {c.WellFormed}: {c.Why})")
            .ToArray();

        disagreed.Should().BeEmpty("the fixture is the contract both sides answer");
    }

    /// <summary>
    /// The CSS injection. A family is the only free-form text in the style pipeline, and the role
    /// sheet writes it into a <c>&lt;style&gt;</c> element — where a CSS string does NOT neutralise
    /// <c>&lt;/style&gt;</c> for the HTML parser, so quoting was never the defence it looked like.
    /// </summary>
    [Fact]
    public void AFamilyThatWouldCloseTheStyleElement_NeverReachesTheSheet()
    {
        FaceResolution.Clear();
        var text = new Text("hello", TypeRole.BodyM)
        {
            StyleOverride = Theme.Type(TypeRole.BodyM) with { Family = "</style><script>alert(1)</script>" },
        };

        FamilyOf(text).Should().BeNull("a name no emitter will accept is not embedded");
        FaceResolution.Unresolved.Should().ContainSingle()
            .Which.Should().Contain("</style>", "and it is REPORTED rather than dropped in silence");
    }

    /// <summary>
    /// The JSON injection, which is the same defect in the other syntax: the theme payload is
    /// written verbatim into a <c>&lt;script&gt;</c> element, so a family containing
    /// <c>&lt;/script&gt;</c> ends the element rather than the string and no JSON quoting helps.
    /// </summary>
    [Fact]
    public void AFamilyThatWouldCloseTheScriptElement_NeverReachesTheWire()
    {
        FaceResolution.Clear();
        var json = ThemeBridge.SerializeJson(new FacedTheme(Theme, "</script>"));

        json.Should().NotContain("</script>", "the payload sits inside a script element");
        FaceResolution.Unresolved.Should().Contain("</script>");
    }

    [Fact]
    public void AWellFormedFamily_StillReachesTheWire()
    {
        FaceResolution.Clear();
        var json = ThemeBridge.SerializeJson(new FacedTheme(Theme, "IBM Plex Sans"));

        json.Should().Contain("IBM Plex Sans", "the rule rejects names, not brands");
        FaceResolution.Unresolved.Should().BeEmpty();
    }

    /// <summary>
    /// A C# <c>string?</c> reaches the client AS null, not as undefined, so the twin guards both.
    /// Here the answer is simply the documented default — an unnamed face is the platform's own.
    /// </summary>
    [Fact]
    public void NoFamilyAtAll_IsTheDefault_NotAnError()
    {
        FaceName.IsWellFormed(null).Should().BeFalse();
        FaceName.Usable(null).Should().BeNull();
        FaceResolution.Unresolved.Should().BeEmpty("absence is the default, never a missing face");
    }

    // ---- What the ROLE styles, and what the NODE styles ------------------------------------------

    /// <summary>
    /// Everything a theme cuts into a ROLE has to reach the element through the CLASS. The client's
    /// lowering cannot read the type scale — its component context is opaque there — so anything SSR
    /// renders inline from the role is dropped on the first client re-render, and the page changes
    /// under the reader. Family, the mono stack, code white-space and the slant, all four.
    /// </summary>
    [Fact]
    public void EverythingARoleCuts_RidesItsClass()
    {
        var css = PhotonCssGenerator.Generate(new RoleTheme(Theme, TypeRole.Caption,
            Theme.Type(TypeRole.Caption) with { Family = "IBM Plex Mono", Mono = true, Italic = true }));

        var block = Block(css, ".eq-type-caption");
        block.Should().Contain("font-family: \"IBM Plex Mono\"");
        block.Should().Contain("white-space: pre-wrap", "a code role keeps its indentation anywhere");
        block.Should().Contain("font-style: italic");
    }

    /// <summary>
    /// A form control does not inherit the document's face, so something must say so — but only
    /// where it cannot collide with the role's own family. It used to be inline on every entry,
    /// which beat the class and meant a themed brand reached every Text and no field.
    /// </summary>
    [Fact]
    public void TheEntryResetYieldsToARoleThatNamesAFace()
    {
        var css = PhotonCssGenerator.Generate(new RoleTheme(Theme, TypeRole.BodyL,
            Theme.Type(TypeRole.BodyL) with { Family = "IBM Plex Sans" }));

        css.Should().NotContain(".eq-entry.eq-type-bodyl {",
            "the role names a face, so the reset must not override it");
        css.Should().Contain(".eq-entry.eq-type-bodym {",
            "a role with no face of its own still needs the UA font defeated");
    }

    private static string Block(string css, string selector)
    {
        var start = css.IndexOf(selector + " {", StringComparison.Ordinal);
        start.Should().BeGreaterThanOrEqualTo(0, $"the sheet declares {selector}");
        var end = css.IndexOf('}', start);
        return css[start..end];
    }

    // ---- The released surface -------------------------------------------------------------------

    /// <summary>
    /// <c>TypeStyle</c> gained a parameter, and a positional record answers that by REPLACING both
    /// halves of its surface: the constructor and the synthesised <c>Deconstruct</c>. C# optional
    /// parameters are not overloads — the default is baked into each call site — so an assembly
    /// compiled against the released package calls signatures that no longer exist and finds a
    /// <c>MissingMethodException</c> at load.
    /// <para>
    /// Reflection rather than a call, because a call would be compiled against THIS tree and would
    /// pass by binding to the eight-parameter form. The question is what the metadata offers an
    /// assembly that was compiled elsewhere.
    /// </para>
    /// </summary>
    [Fact]
    public void TypeStyle_StillOffersTheSurfaceItShippedWith()
    {
        var constructor = typeof(TypeStyle).GetConstructors()
            .Where(c => c.GetParameters().Length == 7)
            .ToArray();
        constructor.Should().ContainSingle("a consumer compiled against the 7-parameter form calls it");

        var deconstruct = typeof(TypeStyle).GetMethods()
            .Where(m => m.Name == "Deconstruct" && m.GetParameters().Length == 7)
            .ToArray();
        deconstruct.Should().ContainSingle(
            "and `var (size, line, weight, tracking, scale, mono, italic) = style;` binds to it");

        // The half that is easy to lose: the same value, whichever way it is read.
        var style = new TypeStyle(15, 20, FontWeight.Regular, 0, 1.3f, false, false);
        var (size, line, weight, tracking, scale, mono, italic) = style;
        (size, line, weight, tracking, scale, mono, italic)
            .Should().Be((15f, 20f, FontWeight.Regular, 0f, 1.3f, false, false));
        style.Family.Should().BeNull("the compatibility shape has no face, which is the default");
    }

    // ---- fixture plumbing -----------------------------------------------------------------------

    private sealed record FaceCase(string Family, bool WellFormed, string Why);

    private static IEnumerable<FaceCase> FaceCases()
    {
        using var document = System.Text.Json.JsonDocument.Parse(File.ReadAllText(FixturePath()));
        foreach (var element in document.RootElement.GetProperty("cases").EnumerateArray())
            yield return new FaceCase(
                element.GetProperty("family").GetString()!,
                element.GetProperty("wellFormed").GetBoolean(),
                element.GetProperty("why").GetString()!);
    }

    private static string FixturePath()
    {
        var here = new DirectoryInfo(AppContext.BaseDirectory);
        while (here is not null && !Directory.Exists(Path.Combine(here.FullName, "src", "eQuantic.UI.Runtime")))
            here = here.Parent;

        here.Should().NotBeNull("the test has to find the repository root to read the fixture");
        return Path.Combine(here!.FullName, "src", "eQuantic.UI.Runtime", "src", "shared",
            "face-names.fixture.json");
    }

    /// <summary>A failure has to NAME the input, and a raw control character in a test message is
    /// invisible exactly where it matters.</summary>
    private static string Printable(string value) =>
        string.Concat(value.Select(c => char.IsControl(c) ? $"\\u{(int)c:x4}" : c.ToString()));

    /// <summary>The theme under test, with ONE role recut — everything else is Photon's.</summary>
    private sealed class RoleTheme(IAppTheme inner, TypeRole role, TypeStyle style) : IAppTheme
    {
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
        public TypeStyle Type(TypeRole asked) => asked == role ? style : inner.Type(asked);
        public ShadowSpec Elevation(int level) => inner.Elevation(level);
        public float Shape(ShapeScale scale) => inner.Shape(scale);
    }

    /// <summary>The theme under test, with one face named — everything else is Photon's.</summary>
    private sealed class FacedTheme(IAppTheme inner, string family) : IAppTheme
    {
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
        public TypeStyle Type(TypeRole role) => inner.Type(role) with { Family = family };
        public ShadowSpec Elevation(int level) => inner.Elevation(level);
        public float Shape(ShapeScale scale) => inner.Shape(scale);
    }
}
