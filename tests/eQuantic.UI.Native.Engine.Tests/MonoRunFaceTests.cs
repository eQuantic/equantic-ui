using eQuantic.UI.Native.Components;
using eQuantic.UI.Native.Framework;
using eQuantic.UI.Primitives;
using FluentAssertions;
using Xunit;

namespace eQuantic.UI.Native.Engine.Tests;

/// <summary>
/// Inline code takes the theme's CODE FACE, and the paragraph's face does not follow it in.
///
/// <para>
/// Nothing could reach this condition until a theme could name faces at all: a role carries a
/// <see cref="TypeStyle.Family"/> and the theme carries a <see cref="IAppTheme.MonoFamily"/>, and
/// both are new. With the stock theme every family is null, so the merge that was wrong here read
/// as right in every existing test — the instrument had never been exercised in the condition it
/// exists for.
/// </para>
///
/// <para>
/// The defect: a run's style started from the paragraph's, and <c>WithCodeFace</c> only fills a
/// family that is NULL. A branded theme therefore handed inline code the body face, and every shell
/// resolves a face as <c>family ?? the OS fixed-pitch face</c> (<c>CoreTextService.FontFor</c>,
/// <c>AndroidTextService.PaintFor</c>, <c>DirectWriteTextService</c>), so the inherited family won
/// over the mono flag — Photon measured and drew a code span proportional while the web
/// (<c>run.Mono ? MonoStack</c>) and email (<c>theme.MonoFamily</c>) both set it monospaced. Same
/// text, three realizers, two answers.
/// </para>
///
/// <para>
/// The asymmetry with a paragraph is deliberate and is the thing to keep: a paragraph that NAMES a
/// family keeps it, because someone chose it. A run cannot name one — <see cref="TextRun.StyleOverride"/>
/// carries a size and nothing else — so a family reaching a run was inherited, never chosen for that
/// span. <c>TheParagraphFace_IsStillHonoured</c> below is what fails if that distinction is "fixed"
/// away by clearing the family for everything monospaced.
/// </para>
/// </summary>
public class MonoRunFaceTests
{
    private const string BodyFace = "IBM Plex Sans";
    private const string CodeFace = "JetBrains Mono";

    /// <summary>The stock theme with two faces named — the shape of an app that branded its type.</summary>
    private sealed class BrandedTheme : IAppTheme
    {
        private static readonly IAppTheme Base = PhotonTheme.Instance;

        public string? MonoFamily => CodeFace;
        public TypeStyle Type(TypeRole role) => Base.Type(role) with { Family = BodyFace };

        public ColorToken Background => Base.Background;
        public ColorToken Surface => Base.Surface;
        public ColorToken SurfaceSubtle => Base.SurfaceSubtle;
        public ColorToken SurfaceHighlight => Base.SurfaceHighlight;
        public ColorToken Border => Base.Border;
        public ColorToken BorderStrong => Base.BorderStrong;
        public ColorToken TextPrimary => Base.TextPrimary;
        public ColorToken TextSecondary => Base.TextSecondary;
        public ColorToken TextMuted => Base.TextMuted;
        public ColorToken TextInverse => Base.TextInverse;
        public ColorToken FocusRing => Base.FocusRing;
        public ColorToken LinkColor => Base.LinkColor;
        public ColorToken Scrim => Base.Scrim;
        public VariantColors Colors(Variant variant) => Base.Colors(variant);
        public ShadowSpec Elevation(int level) => Base.Elevation(level);
        public float Shape(ShapeScale scale) => Base.Shape(scale);
        public float DisabledOpacity => Base.DisabledOpacity;
    }

    private static readonly IAppTheme Branded = new BrandedTheme();

    /// <summary>A sentence with one code span in it — prose, code, prose.</summary>
    private static Text Sentence() => new("", TypeRole.BodyM)
    {
        Spans =
        [
            new TextRun("run "),
            new TextRun("dotnet build", Mono: true),
            new TextRun(" first"),
        ],
    };

    private static IReadOnlyList<TextFragment> Fragments(Text text, IAppTheme theme)
    {
        var node = LayoutEngine.Layout(text, 4000, 300,
            new LayoutContext(theme, ApproximateTextMeasurer.Instance));
        node.TextRuns.Should().NotBeNull("the paragraph has to be laid out as runs for this to mean anything");
        return node.TextRuns!;
    }

    private static TypeStyle StyleOf(IReadOnlyList<TextFragment> fragments, string word) =>
        fragments.First(f => f.Content.Contains(word, StringComparison.Ordinal)).Style;

    /// <summary>
    /// The fix, stated as the consumer sees it: the code span is set in the code face.
    /// </summary>
    [Fact]
    public void AMonoRun_TakesTheThemesCodeFace()
    {
        var fragments = Fragments(Sentence(), Branded);

        StyleOf(fragments, "dotnet").Family.Should().Be(CodeFace,
            "every shell resolves `family ?? the OS fixed-pitch face`, so an inherited proportional "
            + "family wins over the Mono flag and the span draws as prose");
    }

    /// <summary>The other half of the same sentence — the prose around it must NOT have changed.</summary>
    [Fact]
    public void TheProseAroundIt_KeepsTheRoleFace()
    {
        var fragments = Fragments(Sentence(), Branded);

        StyleOf(fragments, "run").Family.Should().Be(BodyFace);
        StyleOf(fragments, "first").Family.Should().Be(BodyFace);
    }

    /// <summary>
    /// The A/B against the WRONG fix. "A mono style drops its family" would pass the first test and
    /// break this one: a PARAGRAPH that names a mono face chose it, and specificity wins over a
    /// theme default here the way it does everywhere else in this file.
    /// </summary>
    [Fact]
    public void TheParagraphFace_IsStillHonoured()
    {
        var chosen = new Text("cargo run", TypeRole.BodyM)
        {
            StyleOverride = Branded.Type(TypeRole.BodyM) with { Family = "Fira Code", Mono = true },
        };

        chosen.Resolve(Branded).Family.Should().Be("Fira Code",
            "the call site was specific; only an INHERITED family is the theme's to replace");
    }

    /// <summary>
    /// Without a code face there is nothing to put in front of the shell's own, and the run must
    /// arrive carrying no family at all — a proportional one left behind would be the original
    /// defect wearing the stock theme.
    /// </summary>
    [Fact]
    public void WithNoCodeFaceNamed_TheRunCarriesNoFamilyForTheShellToPrefer()
    {
        var fragments = Fragments(Sentence(), PhotonTheme.Instance);

        var code = StyleOf(fragments, "dotnet");
        code.Family.Should().BeNull();
        code.Mono.Should().BeTrue("the flag is all the shell has left to pick a fixed-pitch face by");
    }

    /// <summary>
    /// The run's own properties still travel. This is the merge that moved out of the layout engine,
    /// and a move that quietly drops one of them is the failure mode worth pinning.
    /// </summary>
    [Fact]
    public void TheRunsOwnStyleStillTravels()
    {
        var paragraph = Branded.Type(TypeRole.BodyM);
        var run = new TextRun("loud")
        {
            Weight = FontWeight.Bold,
            Italic = true,
            StyleOverride = paragraph with { Size = 13.5f },
        };

        var style = run.Resolve(paragraph, Branded);

        style.Weight.Should().Be(FontWeight.Bold);
        style.Italic.Should().BeTrue();
        style.Size.Should().Be(13.5f);
        style.Family.Should().Be(BodyFace, "a run that is not mono inherits the role's face");
    }

    /// <summary>
    /// "The run keeps the paragraph's line box" is a claim about LAYOUT, so it is asserted on the
    /// laid-out fragments rather than on the style record — <c>WithSize</c> scales a style's own
    /// line height proportionally, and reading that number back would have pinned the wrong thing
    /// while looking like the documented one.
    /// </summary>
    [Fact]
    public void ASmallerRun_SitsOnTheParagraphsLineBox()
    {
        var paragraph = Branded.Type(TypeRole.BodyM);
        var sentence = new Text("", TypeRole.BodyM)
        {
            Spans =
            [
                new TextRun("prose "),
                new TextRun("code", Mono: true) { StyleOverride = paragraph with { Size = 11f } },
                new TextRun(" more"),
            ],
        };

        var fragments = Fragments(sentence, Branded);

        StyleOf(fragments, "code").Size.Should().Be(11f, "the run's own size is the one thing it may say");
        fragments.Select(f => f.Y).Distinct().Should().ContainSingle(
            "a smaller inline span must not reopen the leading — one line box for the paragraph");
    }
}
