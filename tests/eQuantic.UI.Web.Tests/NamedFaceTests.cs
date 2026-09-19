using eQuantic.UI.Primitives;
using eQuantic.UI.Web.Build;
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

    // ---- The theme's CODE face -------------------------------------------------------------------

    /// <summary>
    /// A theme could name ONE face and a handoff names two. `Family` is themeable per ROLE and
    /// `Mono` is a per-NODE flag — a branch name is monospaced and the word beside it is not, both
    /// Caption — so the two never met, and no shipped role sets Mono at all, which makes a
    /// `style.Mono ? code : text` branch inside `Type()` dead code rather than the answer.
    /// <para>
    /// On the web it resolves through the VARIABLE the mono stack already names, so the role class,
    /// every node that says mono, the client's own lowering and the canvas measurer all pick it up
    /// without any of them learning about a theme they cannot see.
    /// </para>
    /// </summary>
    [Fact]
    public void AThemeCanNameTheFaceItSetsCodeIn()
    {
        var css = PhotonCssGenerator.Generate(new CodeFacedTheme(Theme, "JetBrains Mono"));

        css.Should().Contain("--eq-font-mono: \"JetBrains Mono\", ui-monospace",
            "the declaration carries the brand in front of the stack it falls back to");
    }

    [Fact]
    public void NoCodeFace_LeavesTheVariableUndeclared()
    {
        PhotonCssGenerator.Generate(Theme).Should().NotContain("--eq-font-mono:",
            "an app with no opinion keeps the platform's own fixed-pitch face, and the hook stays free");
    }

    /// <summary>The node's own family beats the theme's default, the way specificity does everywhere
    /// else here: the call site was specific and the theme was not.</summary>
    [Fact]
    public void AStyleThatNamesItsOwnFace_KeepsIt()
    {
        var theme = new CodeFacedTheme(Theme, "JetBrains Mono");
        var named = Theme.Type(TypeRole.BodyM) with { Mono = true, Family = "IBM Plex Mono" };

        named.WithCodeFace(theme).Family.Should().Be("IBM Plex Mono");
        (Theme.Type(TypeRole.BodyM) with { Mono = true }).WithCodeFace(theme).Family
            .Should().Be("JetBrains Mono", "and an unnamed one takes the theme's");
    }

    [Fact]
    public void AProportionalStyle_NeverTakesTheCodeFace()
    {
        var theme = new CodeFacedTheme(Theme, "JetBrains Mono");

        Theme.Type(TypeRole.BodyM).WithCodeFace(theme).Family.Should().BeNull();
    }

    /// <summary>
    /// The merge itself, which used to be written once per realizer. The NODE adds to what the role
    /// said and never takes away: `mono: true` on an upright role is code inside prose.
    /// </summary>
    [Fact]
    public void TheNodeAddsToTheRole_AndTheCodeFaceFollows()
    {
        var theme = new CodeFacedTheme(Theme, "JetBrains Mono");
        var style = new Text("git log", TypeRole.Caption) { Mono = true }.Resolve(theme);

        style.Mono.Should().BeTrue();
        style.Family.Should().Be("JetBrains Mono");
        style.Size.Should().Be(Theme.Type(TypeRole.Caption).Size, "the role still sets the scale");
    }

    /// <summary>
    /// The code face CROSSES. <c>IAppTheme</c> is vocabulary, so <c>context.Theme.MonoFamily</c> is
    /// legal inside a component's Build — and a theme property the server answers and the client does
    /// not is the hydration hole <c>[ServerOnly]</c> and EQ2010 exist to close. Carried rather than
    /// fenced, because a theme property nobody can read is a hole in write-once.
    ///
    /// <para>
    /// The three writers are checked together on purpose: the SSR bridge, the generated default
    /// theme, and the client's rehydration (<c>theme-bridge.spec.ts</c>). The <c>family</c> half of
    /// this same slice shipped with the C# side done and the wire not, and nothing said so.
    /// </para>
    /// </summary>
    [Fact]
    public void TheCodeFace_ReachesTheClient()
    {
        var json = ThemeBridge.SerializeJson(new CodeFacedTheme(Theme, "JetBrains Mono"));

        json.Should().Contain("\"monoFamily\":\"JetBrains Mono\"");
    }

    /// <summary>A theme with no opinion sends the payload it always sent — the type tail's rule, for
    /// the same reason: the common case must stay byte-identical.</summary>
    [Fact]
    public void NoCodeFace_AddsNothingToTheWire()
    {
        ThemeBridge.SerializeJson(Theme).Should().NotContain("monoFamily");
    }

    /// <summary>
    /// A family no emitter will accept does not reach the wire here either. It lands inside JSON in
    /// a <c>&lt;script&gt;</c> element, where quoting is not the defence it looks like: a JSON string
    /// does not neutralise <c>&lt;/script&gt;</c> for the HTML parser.
    /// </summary>
    [Fact]
    public void AMonoFaceThatWouldCloseTheScriptElement_NeverReachesTheWire()
    {
        var json = ThemeBridge.SerializeJson(
            new CodeFacedTheme(Theme, "</script><script>alert(1)</script>"));

        json.Should().NotContain("monoFamily");
        json.Should().NotContain("<script");
    }

    /// <summary>The generated default theme is the client's OTHER producer, and it mirrors the
    /// bridge — two mirrors that disagree is what makes one of them stale.</summary>
    [Fact]
    public void TheGeneratedTheme_CarriesTheCodeFaceToo()
    {
        DesignSystemTsGenerator.Generate(new CodeFacedTheme(Theme, "JetBrains Mono"))
            .Should().Contain("monoFamily: 'JetBrains Mono'");
        DesignSystemTsGenerator.Generate(Theme).Should().NotContain("monoFamily");
    }

    /// <summary>
    /// The case that made <c>MonoFamily</c> inert for exactly the themes it was built for.
    ///
    /// <para>
    /// Reported by the eQuantic Code IDE after adopting it. Their theme is the normal way to brand
    /// type — <c>Base.Type(role) with { Family = brandSans }</c> — so every role named a face, and
    /// <c>WithCodeFace</c> read that as a specific call site and stepped aside. The rule it was
    /// applying is right; what it could not see is that the family came from the ROLE and was never
    /// chosen for this text. They worked around it by naming the family at every node, which is the
    /// work a theme exists to remove.
    /// </para>
    /// </summary>
    [Fact]
    public void ANodeAskingForCode_LeavesTheRolesProportionalFaceBehind()
    {
        var theme = new BrandedTheme(Theme, sans: "IBM Plex Sans", mono: "JetBrains Mono");

        new Text("git log", TypeRole.Caption) { Mono = true }.Resolve(theme).Family
            .Should().Be("JetBrains Mono",
                "the role's face is a DEFAULT, and a proportional default must not outlive a node "
                + "that asked for code");
    }

    /// <summary>The other half, and the A/B against "mono means drop the family": prose on the same
    /// theme still gets the brand face.</summary>
    [Fact]
    public void OrdinaryProse_KeepsTheRolesFace()
    {
        var theme = new BrandedTheme(Theme, sans: "IBM Plex Sans", mono: "JetBrains Mono");

        new Text("hello", TypeRole.BodyM).Resolve(theme).Family.Should().Be("IBM Plex Sans");
    }

    /// <summary>
    /// And the A/B against the OTHER wrong fix. A theme that sets <c>Mono</c> and a family on the
    /// SAME role chose that pairing, and it is more specific than the theme-wide code face.
    /// </summary>
    [Fact]
    public void ARoleThatIsCodeItself_KeepsTheFaceTheThemeGaveIt()
    {
        var theme = new RoleTheme(new BrandedTheme(Theme, sans: "IBM Plex Sans", mono: "JetBrains Mono"),
            TypeRole.Caption, Theme.Type(TypeRole.Caption) with { Mono = true, Family = "Fira Code" });

        new Text("git log", TypeRole.Caption).Resolve(theme).Family.Should().Be("Fira Code");
        new Text("git log", TypeRole.Caption) { Mono = true }.Resolve(theme).Family
            .Should().Be("Fira Code", "the node repeating what the role already said changes nothing");
    }

    /// <summary>A family the CALL SITE named is still specific, which is the rule this preserves.</summary>
    [Fact]
    public void AFaceNamedAtTheNode_StillWins()
    {
        var theme = new BrandedTheme(Theme, sans: "IBM Plex Sans", mono: "JetBrains Mono");
        var text = new Text("cargo run", TypeRole.BodyM)
        {
            StyleOverride = theme.Type(TypeRole.BodyM) with { Family = "Fira Code", Mono = true },
        };

        text.Resolve(theme).Family.Should().Be("Fira Code");
    }

    /// <summary>A theme that brands its type: a face per role, and a code face beside it — the shape
    /// the reporting consumer uses and the one every brand theme ends up with.</summary>
    private sealed class BrandedTheme(IAppTheme inner, string sans, string mono) : IAppTheme
    {
        public string? MonoFamily => mono;
        public TypeStyle Type(TypeRole role) => inner.Type(role) with { Family = sans };

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
        public ShadowSpec Elevation(int level) => inner.Elevation(level);
        public float Shape(ShapeScale scale) => inner.Shape(scale);
    }

    private sealed class CodeFacedTheme(IAppTheme inner, string mono) : IAppTheme
    {
        public string? MonoFamily => mono;
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
