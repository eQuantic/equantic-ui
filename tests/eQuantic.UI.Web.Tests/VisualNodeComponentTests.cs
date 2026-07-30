using eQuantic.UI.Components;
using eQuantic.UI.Core;
using eQuantic.UI.Primitives;
using FluentAssertions;
using SharedButton = eQuantic.UI.Components.Button;
using Variant = eQuantic.UI.Primitives.Variant;

namespace eQuantic.UI.Web.Tests;

/// <summary>
/// The Core⇄Shared SSR bridge under the ATOMIC style pipeline (docs/STYLE-SEMANTICS-PLAN.md §2):
/// the render emits CLASS NAMES on the markup and collects the deduplicated rules into
/// <see cref="VisualNodeComponent.Styles"/>; theme-sourced colors ride <c>var(--eq-color-*, …)</c>
/// so the rules are theme-independent. The client lowering computes the same classes (fixture
/// cross-pin), so hydration matches by class identity.
/// </summary>
public class VisualNodeComponentTests
{
    [Fact]
    public void RendersTheSharedCard_InsideACoreComposition()
    {
        var shared = new VisualNodeComponent(new Card(new Primitives.Text("body"), CardKind.Filled));

        var node = shared.Render();

        node.Tag.Should().Be("div");
        node.Attributes["class"].Should().NotBeNullOrEmpty("styles become atomic classes");
        node.Attributes.Should().NotContainKey("style", "regular declarations never stay inline");
        shared.Styles.Css.Should().Contain("border-radius:14px");
        shared.Styles.Css.Should().Contain(
            $"background-color:var(--eq-color-surface-subtle, {TokenCss.Value(PhotonTheme.Instance.SurfaceSubtle)})",
            "theme colors reference their token variable with the resolved fallback");
    }

    [Fact]
    public void RendersTheSharedButton_WithTheCrossPinnedTokens()
    {
        var adapter = new VisualNodeComponent(new SharedButton("Continuar", Variant.Primary, onPressed: () => { }));

        var node = adapter.Render();

        node.Tag.Should().Be("button");
        node.Children[0].Attributes["class"].Should().NotBeNullOrEmpty();
        adapter.Styles.Css.Should().Contain("height:40px");
        adapter.Styles.Css.Should().Contain(
            "background-color:var(--eq-color-primary-base, light-dark(#0050a0, #5ca2e8))");
    }

    [Fact]
    public void HonorsACustomTheme_WhenPassedExplicitly()
    {
        var theme = PhotonTheme.Instance;
        var adapter = new VisualNodeComponent(
            new Primitives.Box(new BoxStyle { Background = theme.Colors(Variant.Success).Base, Width = 10, Height = 10 }),
            theme);

        adapter.Render();
        adapter.Styles.Css.Should().Contain(
            $"background-color:var(--eq-color-success-base, {TokenCss.Value(theme.Colors(Variant.Success).Base)})");
    }

    [Fact]
    public void ComposesAsAChildOfARegularCoreElement()
    {
        IComponent adapter = new VisualNodeComponent(new Primitives.Text("hello", TypeRole.Caption));
        adapter.Render().Tag.Should().Be("span");
        adapter.Render().Attributes["class"].Should().StartWith("eq-type-caption",
            "the semantic role class leads; atomic classes follow");
    }

    [Fact]
    public void AtomicRules_Deduplicate_AcrossRepeatedElements()
    {
        // The 100-cards property: N identical elements contribute ONE set of rules.
        var column = new Column(gap: Space.S2);
        for (var i = 0; i < 100; i++)
            column.Add(new Card(new Primitives.Text($"card {i}"), CardKind.Filled));

        var one = new VisualNodeComponent(new Card(new Primitives.Text("card"), CardKind.Filled));
        one.Render();
        var many = new VisualNodeComponent(column);
        many.Render();

        var oneRules = one.Styles.Css.Count(c => c == '{');
        var manyRules = many.Styles.Css.Count(c => c == '{');
        // The column adds its own flex declarations, but the 100 cards collapse to the same rules.
        (manyRules - oneRules).Should().BeLessThan(10,
            "rule count grows with DISTINCT declarations, never with element count");
    }
}
