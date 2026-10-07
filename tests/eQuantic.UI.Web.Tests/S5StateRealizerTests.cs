using eQuantic.UI.Primitives;
using FluentAssertions;

namespace eQuantic.UI.Web.Tests;

/// <summary>
/// Spec S5 on the web realizer: Hover/Focus StyleDiffs become PSEUDO-VARIANT atomic rules
/// (`.eq-x:hover{…}`) — zero JavaScript, and the rules ride theme variables like every other color.
/// Pseudo-states REQUIRE the atomic pipeline: without a sink they are (documentedly) not realizable
/// inline and are skipped.
/// </summary>
public class S5StateRealizerTests
{
    private static readonly IAppTheme Theme = PhotonTheme.Instance;

    [Fact]
    public void HoverDiff_BecomesAPseudoVariantRule_OnTheSameElement()
    {
        var sink = new StyleSink();
        var element = WebRealizer.Lower(new Primitives.Box(new BoxStyle
        {
            Background = Theme.Surface,
            Hover = new StyleDiff { Background = Theme.SurfaceSubtle, Elevation = 2 },
            Width = 10, Height = 10,
        }), Theme, 1f, sink);

        var subtle = TokenCss.Value(Theme.SurfaceSubtle);
        sink.Css.Should().Contain($":hover{{background-color:var(--eq-color-surface-subtle, {subtle})}}");
        sink.Css.Should().Contain($":hover{{box-shadow:{TokenCss.RingSlot}, 0 2px 8px 0");
        element.ClassName.Should().NotBeNullOrEmpty();
        // The pseudo classes ride the SAME element, after the base atomic set (TS parity order).
        var classes = element.ClassName!.Split(' ');
        classes.Should().OnlyHaveUniqueItems();

        // §10: hover never fires on touch — a tap's sticky emulated :hover must find no rule, so
        // EVERY hover rule lives behind the capability gate (focus-visible stays ungated:
        // keyboard focus exists everywhere).
        var hoverRules = System.Text.RegularExpressions.Regex.Count(sink.Css, ":hover\\{");
        var gatedRules = System.Text.RegularExpressions.Regex.Count(
            sink.Css, "@media \\(hover: hover\\)\\{\\.eq-[0-9a-z]+:hover\\{");
        hoverRules.Should().BeGreaterThan(0);
        gatedRules.Should().Be(hoverRules, "every hover rule rides its own gate");
    }

    /// <summary>
    /// A focus is the CONTROL's: a box is never focusable, so a rule on the box's own
    /// <c>:focus-visible</c> could not match, and the diff applied nowhere. It applies to every box
    /// inside the focused control (#508).
    /// </summary>
    [Fact]
    public void FocusDiff_AppliesUnderTheFocusedControl()
    {
        var sink = new StyleSink();
        WebRealizer.Lower(new Primitives.Box(new BoxStyle
        {
            Focus = new StyleDiff { BorderWidth = 2, BorderColor = Theme.FocusRing },
            Width = 10, Height = 10,
        }), Theme, 1f, sink);

        sink.Css.Should().MatchRegex(
            $@"\.eq-pressable:focus-visible \.eq-[0-9a-z]+\{{border:2px solid var\(--eq-color-focus, {System.Text.RegularExpressions.Regex.Escape(TokenCss.Value(Theme.FocusRing))}\)\}}");
        sink.Css.Should().NotContain("}:focus-visible{", "no rule on the box's own focus, which it never has");
    }

    /// <summary>A press is the CONTROL's: every box inside the pressed control shows its diff (#508).</summary>
    [Fact]
    public void PressedDiff_AppliesUnderThePressedControl()
    {
        var sink = new StyleSink();
        WebRealizer.Lower(new Primitives.Box(new BoxStyle
        {
            Pressed = new StyleDiff { Transform = Transform2D.Scale(0.985f) },
            Width = 10, Height = 10,
        }), Theme, 1f, sink);

        sink.Css.Should().MatchRegex(@"\.eq-pressable\.eq-pressable:active \.eq-[0-9a-z]+\{transform:scale\(0\.985\)\}");
    }

    /// <summary>
    /// The handoff's §10 order, held by SPECIFICITY: the server writes its rules sorted by class and
    /// the browser inserts them in the order it lowers, so a tie between two states would be decided
    /// by whichever class sorted or lowered later. Hover under focus under pressed, on every page.
    /// </summary>
    [Fact]
    public void TheStates_RankBySpecificity_PressedOverFocusOverHover()
    {
        var sink = new StyleSink();
        WebRealizer.Lower(new Primitives.Box(new BoxStyle
        {
            Hover = new StyleDiff { Opacity = 0.8f },
            Focus = new StyleDiff { Opacity = 0.6f },
            Pressed = new StyleDiff { Opacity = 0.4f },
            Width = 10, Height = 10,
        }), Theme, 1f, sink);

        int SpecificityOf(string declaration)
        {
            var rule = System.Text.RegularExpressions.Regex.Match(sink.Css, $@"([^{{}}]+)\{{{declaration}\}}");
            rule.Success.Should().BeTrue($"a rule writes {declaration}");
            var selector = rule.Groups[1].Value;
            // Classes and pseudo-classes: every selector here is made of those and combinators.
            return selector.Count(c => c == '.') + System.Text.RegularExpressions.Regex.Count(selector, @"(?<!:):(?!:)");
        }

        var hover = SpecificityOf("opacity:0\\.8");
        var focus = SpecificityOf("opacity:0\\.6");
        var pressed = SpecificityOf("opacity:0\\.4");
        hover.Should().BeLessThan(focus, "focus beats hover");
        focus.Should().BeLessThan(pressed, "pressed beats focus");
    }

    /// <summary>
    /// The ring is a slot in the surface's own shadow list, never a rule of its own: a box-shadow rule
    /// on the focused control's child outranked the box's list, and keyboard focus took a raised or
    /// glowing control's elevation and glow away (#508). The handoff has it "coexist with any fill".
    /// </summary>
    [Fact]
    public void TheFocusRing_IsASlotInTheSurfacesShadowList()
    {
        var css = PhotonCssGenerator.Generate(Theme);

        css.Should().Contain("@property --eq-ring { syntax: \"*\"; inherits: false; }",
            "a shadowed box inside the control draws no ring of its own");
        css.Should().Contain(".eq-pressable:focus-visible > :first-child, .eq-focused > :first-child "
            + "{ --eq-ring: 0 0 0 2px var(--eq-color-surface), 0 0 0 4px var(--eq-color-focus); }");
        css.Should().Contain(":where(.eq-pressable:focus-visible > :first-child, .eq-focused > :first-child) "
            + "{ box-shadow: var(--eq-ring); }", "a surface with no list of its own still draws the ring");
        css.Should().NotMatchRegex(@"(?<!:where\()\.eq-pressable:focus-visible > :first-child \{ box-shadow",
            "no rule may replace the surface's own list");
    }

    // ---- #504: a state writes every member of its diff, and the lists it shares with the base ---

    // Colours outside the theme, so the atomizer leaves them as written and the CSS can be read
    // back with the same TokenCss calls that wrote it.
    private static readonly ColorToken Glow = new(Color.FromRgb(0x44, 0x88, 0xFF));
    private static readonly ColorToken Halo = new(Color.FromRgb(0x10, 0x20, 0x30));
    private static readonly ColorToken Highlight = new(Color.FromRgb(0xFF, 0xFF, 0xFF));

    private static string Css(BoxStyle style)
    {
        var sink = new StyleSink();
        WebRealizer.Lower(new Primitives.Box(style with { Width = 10, Height = 10 }), Theme, 1f, sink);
        return sink.Css;
    }

    private static string Inset => $"inset 0 1px 0 {TokenCss.Value(Highlight)}";

    /// <summary>CSS replaces box-shadow whole: a hover that only raised the elevation used to write
    /// the elevation's shadow alone, and the glow and the inset highlight went out under the pointer.</summary>
    [Fact]
    public void AHoverThatRaisesTheElevation_KeepsTheGlowAndTheInset()
    {
        var css = Css(new BoxStyle
        {
            Elevation = 1,
            Shadow = new ShadowSpec(0, 24, 0, Glow),
            InsetHighlight = Highlight,
            Hover = new StyleDiff { Elevation = 3 },
        });

        css.Should().Contain(
            $":hover{{box-shadow:{TokenCss.RingSlot}, {TokenCss.Shadow(Theme.Elevation(3))}, {TokenCss.Shadow(new ShadowSpec(0, 24, 0, Glow))}, {Inset}}}");
    }

    [Fact]
    public void AHoversShadows_ReplaceBothOfTheBasesCustomShadows()
    {
        var css = Css(new BoxStyle
        {
            Elevation = 1,
            Shadow = new ShadowSpec(0, 24, 0, Glow),
            Shadows = [new ShadowSpec(2, 4, 0, Halo)],
            InsetHighlight = Highlight,
            Hover = new StyleDiff { Shadows = [new ShadowSpec(4, 32, 2, Glow)] },
        });

        css.Should().Contain(
            $":hover{{box-shadow:{TokenCss.RingSlot}, {TokenCss.Shadow(Theme.Elevation(1))}, {TokenCss.Shadow(new ShadowSpec(4, 32, 2, Glow))}, {Inset}}}");
    }

    [Fact]
    public void AStateThatLeavesNothingToDraw_WritesTheRingsSlotAlone()
    {
        var css = Css(new BoxStyle { Elevation = 2, Hover = new StyleDiff { Elevation = 0 } });

        css.Should().Contain($":hover{{box-shadow:{TokenCss.RingSlot}}}",
            "a hover that drops the shadow has to say so, leaving the property out keeps the base's, "
            + "and the slot alone draws nothing until the control is focused (#508)");
    }

    [Fact]
    public void AStateTransform_ReplacesTheBasesTransform()
    {
        var css = Css(new BoxStyle
        {
            Transform = Transform2D.Rotate(2),
            Hover = new StyleDiff { Transform = Transform2D.Translate(0, -2) },
            Focus = new StyleDiff { Transform = Transform2D.Scale(1) },
        });

        css.Should().Contain(":hover{transform:translate(0, -2px)}");
        css.Should().MatchRegex(@"\.eq-pressable:focus-visible \.eq-[0-9a-z]+\{transform:none\}",
            "an identity transform undoes the base's");
    }

    /// <summary>The gradient is the first of the background's layers: a hover that wrote it alone
    /// took the pattern (and the glow) away, and left their sizes on the gradient.</summary>
    [Fact]
    public void AHoverGradient_KeepsTheBasesOtherLayers()
    {
        var pattern = new GridPattern(16, Halo);
        var gradient = new LinearGradient(Glow, Halo);
        var css = Css(new BoxStyle { Pattern = pattern, Hover = new StyleDiff { Gradient = gradient } });

        css.Should().Contain($":hover{{background-image:{TokenCss.Gradient(gradient)}, {TokenCss.GridPattern(pattern)}}}");
        css.Should().Contain($":hover{{background-size:auto, {TokenCss.GridPatternSize(pattern)}}}");
    }

    [Fact]
    public void AHoverBackdropBlur_IsWritten_AsOneClass()
    {
        var sink = new StyleSink();
        var element = WebRealizer.Lower(new Primitives.Box(new BoxStyle
        {
            Width = 10, Height = 10,
            Hover = new StyleDiff { BackdropBlur = 8 },
        }), Theme, 1f, sink);

        sink.Css.Should().Contain(":hover{-webkit-backdrop-filter:blur(8px);backdrop-filter:blur(8px)}");
        element.ClassName!.Split(' ').Should().OnlyHaveUniqueItems(
            "both spellings are one declaration and one class, which goes on the element once");
    }

    /// <summary>A shadow with no geometry draws nothing. Written, it was a <c>none</c> inside the
    /// list, and CSS drops a list with a <c>none</c> in it whole, the shadows that did draw too.</summary>
    [Fact]
    public void ACustomShadowWithNoGeometry_IsLeftOutOfTheList()
    {
        var css = Css(new BoxStyle
        {
            Shadows = [new ShadowSpec(0, 0, 0, Glow), new ShadowSpec(2, 4, 0, Halo)],
        });

        css.Should().Contain($"{{box-shadow:{TokenCss.RingSlot}, {TokenCss.Shadow(new ShadowSpec(2, 4, 0, Halo))}}}")
            .And.NotContain("none, ").And.NotContain(", none");
    }

    [Fact]
    public void PseudoAndBaseVariants_OfTheSameDeclaration_AreDistinctClasses()
    {
        var sink = new StyleSink();
        var baseClass = sink.ClassFor("opacity", "0.5");
        var hoverClass = sink.ClassFor("opacity", "0.5", ":hover");
        baseClass.Should().NotBe(hoverClass, "the pseudo is part of the hash");
        sink.Css.Should().Contain($".{hoverClass}:hover{{opacity:0.5}}");
    }
}
