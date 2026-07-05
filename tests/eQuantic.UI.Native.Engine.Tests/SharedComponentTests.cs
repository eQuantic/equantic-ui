using eQuantic.UI.Components;
using eQuantic.UI.Native.Components;
using eQuantic.UI.Native.Engine;
using eQuantic.UI.Native.Framework;
using eQuantic.UI.Primitives;
using FluentAssertions;

namespace eQuantic.UI.Native.Engine.Tests;

/// <summary>
/// Shared components (eQuantic.UI.Components — authored once against the abstract vocabulary,
/// zero platform dependencies): Build-tree shape, spec metrics through layout, and token purity.
/// </summary>
public class SharedComponentTests
{
    private static readonly ComponentContext Ctx = new(PhotonTheme.Instance);
    private static readonly LayoutContext Layout = new(PhotonTheme.Instance, ApproximateTextMeasurer.Instance);

    [Fact]
    public void Button_Builds_PressableBoxRowText()
    {
        var button = new Button("Continue", onPressed: () => { });
        var tree = button.Build(Ctx);

        var pressable = tree.Should().BeOfType<Pressable>().Subject;
        pressable.Label.Should().Be("Continue");
        var box = pressable.Child.Should().BeOfType<Box>().Subject;
        var row = box.Child.Should().BeOfType<Row>().Subject;
        var text = row.Children[0].Should().BeOfType<Text>().Subject;
        text.Content.Should().Be("Continue");
        text.MaxLines.Should().Be(1, "single line, ellipsis — never two lines (spec A12)");
    }

    [Theory]
    [InlineData(SizeVariant.Small, 32, 12, 13)]
    [InlineData(SizeVariant.Medium, 40, 16, 15)]
    [InlineData(SizeVariant.Large, 48, 20, 16)]
    [InlineData(SizeVariant.XLarge, 56, 24, 17)]
    public void Button_MetricsFlowThroughLayout(SizeVariant size, float height, float padX, float labelSize)
    {
        var button = new Button("Go", size: size);
        var box = (Box)((Pressable)button.Build(Ctx)).Child;
        box.Style.Height.Should().Be(new SizeValue(SizeKind.Fixed, height));
        box.Style.Padding.Start.Should().Be(padX);
        ((Text)((Row)box.Child!).Children[0]).StyleOverride!.Value.Size.Should().Be(labelSize);

        // Through the layout engine: fixed height, hugged width floored at MinWidth 64.
        var node = LayoutEngine.Layout(button, 400, 300, Layout);
        var laidBox = node.Children[0].Children[0]; // component → pressable → box
        laidBox.Bounds.Height.Should().Be(height);
        laidBox.Bounds.Width.Should().BeGreaterThanOrEqualTo(ButtonStyles.MinWidth);
    }

    [Fact]
    public void Button_Tokens_ArePureAndModeFree()
    {
        var box = (Box)((Pressable)new Button("Pay").Build(Ctx)).Child;
        var colors = PhotonTheme.Instance.Colors(Variant.Primary);
        box.Style.Background.Should().Be(colors.Base, "components author TOKENS — realizers resolve the mode");
    }

    [Fact]
    public void Button_DerivedVariants_NoFill_OutlineBorder_LinkPadding()
    {
        var outline = (Box)((Pressable)new Button("A", Variant.Outline).Build(Ctx)).Child;
        outline.Style.Background.Should().BeNull();
        outline.Style.BorderWidth.Should().Be(1);
        outline.Style.BorderColor.Should().Be(PhotonTheme.Instance.BorderStrong);

        var ghost = (Box)((Pressable)new Button("B", Variant.Ghost).Build(Ctx)).Child;
        ghost.Style.Background.Should().BeNull();
        ghost.Style.BorderWidth.Should().Be(0);

        var link = (Box)((Pressable)new Button("C", Variant.Link).Build(Ctx)).Child;
        link.Style.Background.Should().BeNull();
        link.Style.Padding.Start.Should().Be(6, "Link is an inline-text control");
    }

    [Fact]
    public void Button_Disabled_AppliesOpacityGroupAndSwallowsPress()
    {
        var pressed = false;
        var button = new Button("Del", Variant.Destructive, onPressed: () => pressed = true) { Disabled = true };
        var pressable = (Pressable)button.Build(Ctx);

        pressable.Disabled.Should().BeTrue();
        pressable.OnPressed.Should().BeNull("disabled presses are swallowed");
        var box = (Box)pressable.Child;
        box.Style.Background!.Value.Light.A.Should().BeLessThan(255, "38% group applied at the token level");
        pressed.Should().BeFalse();
    }

    [Fact]
    public void Button_Expand_FillsTheRow()
    {
        var box = (Box)((Pressable)new Button("CTA") { Expand = true }.Build(Ctx)).Child;
        box.Style.Width.Kind.Should().Be(SizeKind.Fill);
    }

    [Fact]
    public void Card_Kinds_MapToSpecSurfaces()
    {
        var theme = PhotonTheme.Instance;
        var content = new Text("body");

        var elevated = (Box)new Card(content).Build(Ctx);
        elevated.Style.Background.Should().Be(theme.Surface);
        elevated.Style.BorderWidth.Should().Be(1, "border fallback until the engine draws E1 shadows");
        elevated.Style.CornerRadius.TopLeft.Should().Be(Radius.Lg);
        elevated.Style.Padding.Start.Should().Be(Space.S4);

        var outlined = (Box)new Card(content, CardKind.Outlined).Build(Ctx);
        outlined.Style.Background.Should().Be(theme.Surface);
        outlined.Style.BorderWidth.Should().Be(1);

        var filled = (Box)new Card(content, CardKind.Filled).Build(Ctx);
        filled.Style.Background.Should().Be(theme.SurfaceSubtle);
        filled.Style.BorderWidth.Should().Be(0);
    }

    [Fact]
    public void Components_ComposeInsideFlexTrees()
    {
        // A component IS a VisualNode: it slots into rows/columns and expands inline during layout.
        var row = new Row(gap: Space.S2);
        row.Add(new Button("One", size: SizeVariant.Small));
        row.Add(new Button("Two", size: SizeVariant.Small));
        var node = LayoutEngine.Layout(row, 400, 100, Layout);

        node.Children.Should().HaveCount(2);
        node.Children[1].Bounds.X.Should().BeGreaterThan(node.Children[0].Bounds.Width, "gap separates the buttons");
        node.Bounds.Height.Should().Be(32, "row hugs the Small button height");
    }
}
