using eQuantic.UI.Components.Shared;
using eQuantic.UI.Core;
using eQuantic.UI.Primitives;
using FluentAssertions;
using SharedButton = eQuantic.UI.Components.Shared.Button;
using SizeVariant = eQuantic.UI.Primitives.SizeVariant;
using Variant = eQuantic.UI.Primitives.Variant;

namespace eQuantic.UI.Web.Tests;

/// <summary>
/// Web lowering rules — the SAME abstract trees the native realizer turns into Photon pixels, lowered
/// to HtmlElement/DOM + CSS. These mappings are normative: the TypeScript runtime lowering must match
/// them exactly (hydration), and the cross-target layout harness compares the two realizations.
/// </summary>
public class WebRealizerTests
{
    private static readonly PhotonTheme Theme = PhotonTheme.Instance;

    private static HtmlNode Render(VisualNode node) => WebRealizer.Lower(node, Theme).Render();

    [Fact]
    public void Box_LowersToDiv_WithTokenStyles()
    {
        var box = new Primitives.Box(new BoxStyle
        {
            Width = 120,
            Height = 40,
            Padding = EdgeInsets.Symmetric(16, 0),
            Background = Theme.Colors(Variant.Primary).Base,
            CornerRadius = new CornerRadii(Radius.Md),
            BorderWidth = 1,
            BorderColor = Theme.BorderStrong,
        });

        var node = Render(box);
        node.Tag.Should().Be("div");
        var style = node.Attributes["style"]!;
        style.Should().Contain("box-sizing: border-box", "Photon borders are inside — border-box is the parity contract");
        style.Should().Contain("width: 120px");
        style.Should().Contain("height: 40px");
        style.Should().Contain("padding: 0 16px 0 16px");
        style.Should().Contain("background-color: light-dark(#0050a0, #5ca2e8)");
        style.Should().Contain("border-radius: 10px");
        style.Should().Contain("border: 1px solid light-dark(#c9ced6, #3d4754)");
    }

    [Fact]
    public void RowAndColumn_LowerToFlex_WithSpecDefaults()
    {
        var row = new Row(gap: Space.S2) { Main = MainAlign.SpaceBetween };
        row.Add(new Primitives.Box(new BoxStyle { Width = 20, Height = 20 }));
        var rowStyle = Render(row).Attributes["style"]!;
        rowStyle.Should().Contain("display: flex");
        rowStyle.Should().Contain("flex-direction: row");
        rowStyle.Should().Contain("gap: 8px");
        rowStyle.Should().Contain("justify-content: space-between");
        rowStyle.Should().Contain("align-items: center", "Row cross defaults to Center (spec A2)");

        var column = new Column();
        column.Add(new Primitives.Box(new BoxStyle { Height = 10 }));
        var columnStyle = Render(column).Attributes["style"]!;
        columnStyle.Should().Contain("flex-direction: column");
        columnStyle.Should().Contain("align-items: stretch", "Column cross defaults to Stretch (spec A2)");
    }

    [Fact]
    public void Text_LowersToRoleClassedSpan_WithEllipsisContract()
    {
        var text = new Primitives.Text("Saldo disponível", TypeRole.Caption, Theme.TextMuted, maxLines: 1);
        var node = Render(text);

        node.Tag.Should().Be("span");
        node.Attributes["class"].Should().Be("eq-type-caption");
        var style = node.Attributes["style"]!;
        style.Should().Contain($"color: {TokenCss.Value(Theme.TextMuted)}");
        style.Should().Contain("white-space: nowrap");
        style.Should().Contain("text-overflow: ellipsis");
        node.Children[0].TextContent.Should().Be("Saldo disponível");
    }

    [Fact]
    public void Flexible_LowersToBasisZeroGrow_MatchingNativeLeftoverSemantics()
    {
        var row = new Row(gap: 0);
        row.Add(new Flexible(new Primitives.Text("t"), flex: 2));
        var node = Render(row);

        var wrapper = node.Children[0];
        wrapper.Attributes["style"].Should().Contain("flex: 2 1 0%");
        wrapper.Attributes["style"].Should().Contain("min-width: 0", "text must shrink to ellipsis, not push siblings");
    }

    [Fact]
    public void Spacer_Flexible_And_Fixed_FollowTheAxis()
    {
        var row = new Row(gap: 0);
        row.Add(new Spacer());
        row.Add(Spacer.Fixed(24));
        var node = Render(row);

        node.Children[0].Attributes["style"].Should().Contain("flex: 1 1 0%");
        node.Children[1].Attributes["style"].Should().Contain("width: 24px");

        var column = new Column();
        column.Add(Spacer.Fixed(24));
        Render(column).Children[0].Attributes["style"].Should().Contain("height: 24px");
    }

    [Fact]
    public void Pressable_LowersToNeutralizedButton()
    {
        var fired = false;
        var pressable = new Pressable(new Primitives.Text("Go"), onPressed: () => fired = true) { Label = "Go" };
        var element = WebRealizer.Lower(pressable, Theme);
        var node = element.Render();

        node.Tag.Should().Be("button");
        node.Attributes["aria-label"].Should().Be("Go");
        var style = node.Attributes["style"]!;
        style.Should().Contain("background: none");
        style.Should().Contain("border: none");
        style.Should().Contain("cursor: pointer");

        element.OnClick!.Invoke();
        fired.Should().BeTrue("OnPressed wires to OnClick");
    }

    [Fact]
    public void PressableDisabled_SetsDisabledAndDropsHandler()
    {
        var pressable = new Pressable(new Primitives.Text("X"), onPressed: () => { }) { Disabled = true };
        var element = WebRealizer.Lower(pressable, Theme);

        element.OnClick.Should().BeNull();
        element.Render().Attributes.Should().ContainKey("disabled");
    }

    [Fact]
    public void SharedButton_LowersTheSameCompositionAsNative()
    {
        // The SAME component instance the native goldens render — now as DOM.
        var button = new SharedButton("Continuar", Variant.Primary, SizeVariant.Medium, onPressed: () => { });
        var node = Render(button);

        node.Tag.Should().Be("button");
        var box = node.Children[0];
        box.Tag.Should().Be("div");
        var boxStyle = box.Attributes["style"]!;
        boxStyle.Should().Contain("height: 40px", "Medium height from the spec A12 table");
        boxStyle.Should().Contain("min-width: 64px");
        boxStyle.Should().Contain("border-radius: 10px");
        boxStyle.Should().Contain("background-color: light-dark(#0050a0, #5ca2e8)");
        boxStyle.Should().Contain("padding: 0 16px 0 16px");

        var label = box.Children[0].Children[0];
        label.Tag.Should().Be("span");
        label.Attributes["style"].Should().Contain("font-size: 15px", "Medium label from the size table");
        label.Children[0].TextContent.Should().Be("Continuar");
    }

    [Fact]
    public void SharedCard_LowersSurfaceAndRadius()
    {
        var card = new Card(new Primitives.Text("body"), CardKind.Filled);
        var node = Render(card);

        var style = node.Attributes["style"]!;
        style.Should().Contain($"background-color: {TokenCss.Value(Theme.SurfaceSubtle)}");
        style.Should().Contain("border-radius: 14px");
        style.Should().Contain("padding: 16px 16px 16px 16px");
    }

    [Fact]
    public void WriteOnceProof_SameTreeLowersOnWebAndComposesLegacyStyle()
    {
        // The abstract-card tree from the NATIVE goldens, lowered to DOM — one source, two targets.
        var identity = new Column(gap: Space.S1);
        identity.Add(new Primitives.Text("Ana Beatriz Nogueira", TypeRole.BodyM));
        identity.Add(new Primitives.Text("Premium account", TypeRole.Caption, Theme.TextMuted));

        var header = new Row(gap: Space.S3);
        header.Add(new Primitives.Box(new BoxStyle
        {
            Width = 40, Height = 40,
            Background = Theme.Colors(Variant.Primary).Subtle,
            CornerRadius = new CornerRadii(Radius.Full),
        }));
        header.Add(new Flexible(identity));

        var content = new Column(gap: Space.S3);
        content.Add(new Primitives.Text("Portfolio overview", TypeRole.Title));
        content.Add(header);
        content.Add(new SharedButton("Transferir", onPressed: () => { }));

        var html = Render(new Card(content, CardKind.Outlined));

        html.Tag.Should().Be("div");
        var column = html.Children[0];
        column.Attributes["style"].Should().Contain("flex-direction: column");
        column.Children.Should().HaveCount(3);
        column.Children[2].Tag.Should().Be("button", "the shared Button composes inside the shared Card");
    }
}
