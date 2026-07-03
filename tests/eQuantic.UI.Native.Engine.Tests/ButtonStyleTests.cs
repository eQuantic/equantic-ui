using eQuantic.UI.Native.Engine;
using eQuantic.UI.Native.Components;
using eQuantic.UI.Primitives;
using FluentAssertions;

namespace eQuantic.UI.Native.Engine.Tests;

/// <summary>
/// Button resolver rules (spec A12 — the reference implementation of variant × size × state;
/// every other control follows its conventions).
/// </summary>
public class ButtonStyleTests
{
    private static readonly PhotonTheme Theme = PhotonTheme.Instance;

    private static ButtonStyle Resolve(Variant v, SizeVariant s = SizeVariant.Medium,
        InteractionState state = InteractionState.Default, ThemeMode mode = ThemeMode.Light) =>
        ButtonStyles.Resolve(Theme, mode, v, s, state);

    [Theory]
    [InlineData(SizeVariant.Small, 32, 12, 6, 13, 16, 10, 48)]
    [InlineData(SizeVariant.Medium, 40, 16, 8, 15, 20, 10, 48)]
    [InlineData(SizeVariant.Large, 48, 20, 8, 16, 20, 10, 48)]
    [InlineData(SizeVariant.XLarge, 56, 24, 10, 17, 24, 14, 56)]
    public void SizeTable_MatchesSpec(SizeVariant size, float h, float padX, float gap, float label, float icon, float radius, float hit)
    {
        var style = Resolve(Variant.Primary, size);
        style.Height.Should().Be(h);
        style.PaddingX.Should().Be(padX);
        style.Gap.Should().Be(gap);
        style.LabelSize.Should().Be(label);
        style.IconSize.Should().Be(icon);
        style.CornerRadius.Should().Be(radius);
        style.HitTarget.Should().Be(hit);
        style.MinWidth.Should().Be(64);
        style.LabelWeight.Should().Be(FontWeight.SemiBold);
    }

    [Fact]
    public void Primary_PressSwapsFillToPressedToken()
    {
        Resolve(Variant.Primary).Fill.Should().Be(Color.FromRgb(0x00, 0x50, 0xA0));
        Resolve(Variant.Primary, state: InteractionState.Pressed).Fill.Should().Be(Color.FromRgb(0x00, 0x42, 0x7F));
        // Content stays OnBase through the press.
        Resolve(Variant.Primary, state: InteractionState.Pressed).TextColor.Should().Be(Color.White);
    }

    [Fact]
    public void Outline_HasBorderAndPressedSurfaceSubtle()
    {
        var rest = Resolve(Variant.Outline);
        rest.Fill.A.Should().Be(0, "outline rests transparent");
        rest.BorderWidth.Should().Be(1);
        rest.BorderColor.Should().Be(Theme.BorderStrong.Resolve(ThemeMode.Light));
        rest.TextColor.Should().Be(Theme.TextPrimary.Resolve(ThemeMode.Light));

        Resolve(Variant.Outline, state: InteractionState.Pressed).Fill
            .Should().Be(Theme.SurfaceSubtle.Resolve(ThemeMode.Light));
    }

    [Fact]
    public void Ghost_IsOutlineWithoutBorder()
    {
        var style = Resolve(Variant.Ghost);
        style.Fill.A.Should().Be(0);
        style.BorderWidth.Should().Be(0);
        Resolve(Variant.Ghost, state: InteractionState.Pressed).Fill
            .Should().Be(Theme.SurfaceSubtle.Resolve(ThemeMode.Light));
    }

    [Fact]
    public void Link_NeverFills_PressUnderlinesAndSwapsText()
    {
        var rest = Resolve(Variant.Link);
        rest.Fill.A.Should().Be(0);
        rest.PaddingX.Should().Be(6, "Link is an inline-text control");
        rest.TextColor.Should().Be(Theme.LinkColor.Resolve(ThemeMode.Light));
        rest.UnderlineLabel.Should().BeFalse("no underline at rest");

        var pressed = Resolve(Variant.Link, state: InteractionState.Pressed);
        pressed.Fill.A.Should().Be(0, "pressed swaps the TEXT, not the fill");
        pressed.UnderlineLabel.Should().BeTrue();
        pressed.TextColor.Should().Be(Theme.Colors(Variant.Primary).Pressed.Resolve(ThemeMode.Light));
    }

    [Fact]
    public void Disabled_Is38PercentGroup_NotAColorSwap()
    {
        var style = Resolve(Variant.Primary, state: InteractionState.Disabled);
        style.Opacity.Should().BeApproximately(0.38f, 1e-4f);
        style.Fill.Should().Be(Color.FromRgb(0x00, 0x50, 0xA0), "the resolved colors stay — opacity is a group");
    }

    [Fact]
    public void Focused_ShowsTheDoubleRing()
    {
        var style = Resolve(Variant.Primary, state: InteractionState.Focused);
        style.ShowFocusRing.Should().BeTrue();
        style.FocusRingColor.Should().Be(Theme.FocusRing.Resolve(ThemeMode.Light));
        style.FocusRingGapColor.Should().Be(Theme.Surface.Resolve(ThemeMode.Light));
        Resolve(Variant.Primary).ShowFocusRing.Should().BeFalse();
    }

    [Fact]
    public void Renderer_EmitsChromeOnly_WithinBudget()
    {
        var builder = new DisplayListBuilder();
        ButtonRenderer.Draw(builder, new Rect(0, 0, 120, 40), Resolve(Variant.Primary));
        builder.Build().Count.Should().Be(1, "a filled button at rest is exactly one rrect");

        builder = new DisplayListBuilder();
        ButtonRenderer.Draw(builder, new Rect(0, 0, 120, 40), Resolve(Variant.Outline, state: InteractionState.Focused));
        builder.Build().Count.Should().Be(3, "outline focused = 2 focus rings + 1 border (no fill at rest)");
    }
}
