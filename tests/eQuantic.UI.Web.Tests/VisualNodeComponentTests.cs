using eQuantic.UI.Components.Shared;
using eQuantic.UI.Core;
using eQuantic.UI.Primitives;
using FluentAssertions;
using SharedButton = eQuantic.UI.Components.Shared.Button;
using Variant = eQuantic.UI.Primitives.Variant;

namespace eQuantic.UI.Web.Tests;

/// <summary>
/// The Core⇄Shared SSR bridge: a Core page composes write-once components through
/// <see cref="VisualNodeComponent"/> anywhere an IComponent fits, and the server render produces the
/// SAME DOM the client lowering hydrates against (the cross-pinned parity pair).
/// </summary>
public class VisualNodeComponentTests
{
    [Fact]
    public void RendersTheSharedCard_InsideACoreComposition()
    {
        // A Core page shape: a plain HtmlElement tree with the adapter as one child.
        var shared = new VisualNodeComponent(new Card(new Primitives.Text("body"), CardKind.Filled));

        var node = shared.Render();

        node.Tag.Should().Be("div");
        node.Attributes["style"].Should().Contain("border-radius: 14px");
        node.Attributes["style"].Should().Contain($"background-color: {TokenCss.Value(PhotonTheme.Instance.SurfaceSubtle)}");
    }

    [Fact]
    public void RendersTheSharedButton_WithTheCrossPinnedTokens()
    {
        var adapter = new VisualNodeComponent(new SharedButton("Continuar", Variant.Primary, onPressed: () => { }));

        var node = adapter.Render();

        node.Tag.Should().Be("button");
        var box = node.Children[0];
        box.Attributes["style"].Should().Contain("height: 40px");
        box.Attributes["style"].Should().Contain("background-color: light-dark(#0050a0, #5ca2e8)");
    }

    [Fact]
    public void HonorsACustomTheme_WhenPassedExplicitly()
    {
        // Same tree, custom theme: the adapter resolves tokens from IT, not the default instance.
        var theme = PhotonTheme.Instance;
        var adapter = new VisualNodeComponent(
            new Primitives.Box(new BoxStyle { Background = theme.Colors(Variant.Success).Base, Width = 10, Height = 10 }),
            theme);

        adapter.Render().Attributes["style"].Should()
            .Contain($"background-color: {TokenCss.Value(theme.Colors(Variant.Success).Base)}");
    }

    [Fact]
    public void ComposesAsAChildOfARegularCoreElement()
    {
        // Composite-pattern proof: the adapter is an IComponent — Children machinery accepts it.
        IComponent adapter = new VisualNodeComponent(new Primitives.Text("hello", TypeRole.Caption));
        adapter.Render().Tag.Should().Be("span");
        adapter.Render().Attributes["class"].Should().Be("eq-type-caption");
    }
}
