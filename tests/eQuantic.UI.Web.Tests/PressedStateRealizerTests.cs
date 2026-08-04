using eQuantic.UI.Core;
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

        node.Attributes["class"].Should().Be("eq-pressable");
        // CROSS-PIN: the custom property lands after the ordered entries (TS mirrors byte-for-byte).
        node.Attributes["style"].Should().EndWith(
            $"--eq-pressed-bg: {TokenCss.Value(Theme.Colors(Variant.Primary).Pressed)}");
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
        css.Should().Contain(".eq-pressable:active > :first-child { background-color: var(--eq-pressed-bg) !important; }");
        css.Should().Contain("transition: background-color var(--eq-motion-fast) ease-out");
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
