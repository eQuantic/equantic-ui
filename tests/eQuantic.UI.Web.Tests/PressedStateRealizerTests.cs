using eQuantic.UI.Web;
using eQuantic.UI.Primitives;
using FluentAssertions;
using SharedButton = eQuantic.UI.Components.Button;
using Variant = eQuantic.UI.Primitives.Variant;

namespace eQuantic.UI.Web.Tests;

/// <summary>Interaction slice 1 on web: class + custom property on the element, mechanics in the generated CSS.</summary>
public class PressedStateRealizerTests
{
    private static readonly PhotonTheme Theme = PhotonTheme.Instance;

    [Fact]
    public void PressableWithPressedFill_CarriesClassAndCustomProperty()
    {
        var node = WebRealizer.Lower(new SharedButton("Save", onPressed: () => { }), Theme).Render();

        node.Attributes["class"].Should().Be("eq-pressable eq-press-fill");
        // CROSS-PIN: the custom property lands after the ordered entries (TS mirrors byte-for-byte).
        node.Attributes["style"].Should().EndWith(
            $"--eq-pressed-bg: {TokenCss.Value(Theme.Colors(Variant.Primary).Pressed)}");
    }

    /// <summary>
    /// A control with no pressed fill keeps its own while pressed. The swap is a var() with no
    /// fallback, and a var() with no value makes the declaration compute to the property's initial
    /// value: under <c>!important</c> every such control went transparent while pressed, measured in
    /// Chromium as <c>rgba(0, 0, 0, 0)</c> (#508). Only a control that has a fill is marked for it.
    /// </summary>
    [Fact]
    public void PressableWithoutPressedFill_IsNotMarkedForTheSwap()
    {
        var node = WebRealizer.Lower(new Primitives.Pressable(
            new Primitives.Box(new BoxStyle { Background = Theme.Surface }), () => { }), Theme).Render();

        node.Attributes["class"].Should().Be("eq-pressable");
        node.Attributes.TryGetValue("style", out var style);
        (style ?? "").Should().NotContain("--eq-pressed-bg");
    }

    [Fact]
    public void DisabledPressable_HasNoPressedMechanics()
    {
        var node = WebRealizer.Lower(new SharedButton("Save", onPressed: () => { }) { Disabled = true }, Theme).Render();
        node.Attributes.Should().NotContainKey("class");
    }

    [Fact]
    public void GeneratedCss_CarriesThePressedMechanics()
    {
        var css = PhotonCssGenerator.Generate(Theme);
        css.Should().Contain(".eq-press-fill:active > :first-child { background-color: var(--eq-pressed-bg) !important; }");
        css.Should().Contain(".eq-pressed.eq-press-fill > :first-child { background-color: var(--eq-pressed-bg) !important; }");
        css.Should().NotContain(".eq-pressable:active > :first-child { background-color",
            "a control with no pressed fill must not take one");
        // At zero specificity, so a surface's own transition (a press that scales, a hover that
        // lifts) is kept: at (0,2,0) this rule replaced it, measured in Chromium (#508).
        css.Should().Contain(":where(.eq-pressable > :first-child) { transition: background-color var(--eq-motion-fast) ease-out; }");
        css.Should().NotContain("\n.eq-pressable > :first-child { transition",
            "no rule may outrank the surface's own transition");
    }

    /// <summary>
    /// The native dispatch twin: INERT yields to the pressable that wraps it. Without this rule the
    /// browser suppresses the click on a disabled control entirely and a Menu whose trigger is a
    /// disabled-looking Button never opens — the wrapper never hears a thing.
    /// </summary>
    [Fact]
    public void GeneratedCss_LetsAWrappedInertControlYieldTheClick()
    {
        var css = PhotonCssGenerator.Generate(Theme);
        css.Should().Contain(
            ".eq-pressable [disabled], .eq-pressable [aria-disabled=\"true\"] { pointer-events: none; }");
    }
}
