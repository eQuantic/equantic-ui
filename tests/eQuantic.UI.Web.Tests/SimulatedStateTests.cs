using eQuantic.UI.Primitives;
using FluentAssertions;

namespace eQuantic.UI.Web.Tests;

/// <summary>
/// A subtree drawn AS IF it were hovered, pressed or focused.
/// <para>
/// A pressed button cannot be handed to a constructor: pressed is something the host observes, not
/// something a tree states. So a documentation gallery, a design review and a visual-regression
/// suite could only ever show a control's rest state — the three states a reader most wants to
/// compare were the three no page could draw.
/// </para>
/// </summary>
public class SimulatedStateTests
{
    private static readonly IAppTheme Theme = PhotonTheme.Instance;

    private static (string Html, string Css) Render(VisualNode node)
    {
        var sink = new StyleSink();
        var html = HtmlRenderer.RenderNode(
            WebRealizer.Lower(node, Theme, 1f, sink).Render());
        return (html, sink.Css);
    }

    private static Primitives.Box Hoverable() => new(new BoxStyle
    {
        Background = Theme.Surface,
        Hover = new StyleDiff { Background = Theme.Colors(Variant.Primary).Base },
    }, new Text("x", TypeRole.BodyM));

    /// <summary>
    /// The web cannot force <c>:hover</c> from CSS — nothing can, which is the point of a
    /// pseudo-class — so the diff folds into the BASE rule. Same declaration, which is what makes
    /// the preview honest rather than an approximation of one.
    /// </summary>
    [Fact]
    public void AHoveredSubtree_DrawsTheHoverDiffAtRest()
    {
        var (_, resting) = Render(Hoverable());
        var (_, simulated) = Render(new Simulated(SimulatedState.Hovered, Hoverable()));

        resting.Should().Contain(":hover", "a real hover still rides its pseudo-class");
        simulated.Should().NotContain(":hover", "there is nothing to hover — it is already drawn");
        simulated.Should().Contain("--eq-color-primary", "the hover's colour is the resting colour now");
    }

    /// <summary>A pictured press lays the box's <c>Pressed</c> diff over its base, as a real one does
    /// under the control's <c>:active</c>, and a pictured focus marks the control so the ring is drawn
    /// as a real focus draws it (#508).</summary>
    [Fact]
    public void ASimulatedPressAndFocus_DrawWhatTheRealOnesDo()
    {
        Primitives.Pressable Control() => new(new Primitives.Box(new BoxStyle
        {
            Pressed = new StyleDiff { Transform = Transform2D.Scale(0.985f) },
            Focus = new StyleDiff { Opacity = 0.9f },
        }, new Text("x", TypeRole.BodyM)), () => { });

        var (html, css) = Render(new Simulated(SimulatedState.Pressed | SimulatedState.Focused, Control()));

        css.Should().Contain("{transform:scale(0.985)}", "the press is on the base");
        css.Should().Contain("{opacity:0.9}", "and so is the focus");
        css.Should().NotContain(":active", "there is nothing to press: it is already drawn");
        html.Should().MatchRegex("class=\"[^\"]*eq-focused", "the control is marked, so the ring is drawn");
    }

    /// <summary>A picture of a DISABLED control shows no press and no focus: the control's simulated
    /// states are masked for its subtree, as the real ones never select under it (found by Copilot
    /// on #617). A hover is the box's own state and is still pictured.</summary>
    [Fact]
    public void ASimulatedPressAndFocusOfADisabledControl_DrawNothing()
    {
        var (html, css) = Render(new Simulated(SimulatedState.Pressed | SimulatedState.Focused | SimulatedState.Hovered,
            new Primitives.Pressable(new Primitives.Box(new BoxStyle
            {
                Pressed = new StyleDiff { Transform = Transform2D.Scale(0.985f) },
                Focus = new StyleDiff { Opacity = 0.9f },
                Hover = new StyleDiff { Opacity = 0.7f },
            }, new Text("x", TypeRole.BodyM)), () => { }) { Disabled = true }));

        css.Should().NotContain("scale(0.985)", "a disabled control is never pressed, pictured or real");
        css.Should().NotContain("opacity:0.9", "nor focused");
        css.Should().Contain("{opacity:0.7}", "the box's own hover is still pictured");
        html.Should().NotContain("eq-pressed").And.NotContain("eq-focused");
    }

    /// <summary>A disabled control inside an ENABLED one: the outer control's press selects every box
    /// inside it, and the disabled control's boxes write no press rule for it to select (#508).</summary>
    [Fact]
    public void ADisabledControlInsideAnEnabledOne_WritesNoPressForItsBoxes()
    {
        var (_, css) = Render(new Primitives.Pressable(new Primitives.Pressable(new Primitives.Box(new BoxStyle
        {
            Pressed = new StyleDiff { Transform = Transform2D.Scale(0.985f) },
        }, new Text("x", TypeRole.BodyM)), () => { }) { Disabled = true }, () => { }));

        css.Should().NotContain("scale(0.985)");
    }

    /// <summary>
    /// Every member a diff can set has a place in the simulated style. The pseudo path writes a
    /// state's declarations as they come; the simulated one maps each back onto the element's style,
    /// and a declaration with no place there throws while the page renders. The members are READ off
    /// the type, so the next one added to <see cref="StyleDiff"/> is asked here before a preview meets
    /// it (#507).
    /// </summary>
    [Fact]
    public void EveryMemberOfADiff_HasAPlaceInTheSimulatedStyle()
    {
        var diff = new StyleDiff();
        var members = typeof(StyleDiff).GetProperties().Where(p => p.CanWrite).ToList();
        foreach (var member in members) member.SetValue(diff, Sample(member.PropertyType));
        members.Should().HaveCountGreaterThan(8, "a diff with nothing set would prove nothing");

        var box = new Primitives.Box(new BoxStyle { Hover = diff, Focus = diff }, new Text("x", TypeRole.BodyM));

        FluentActions.Invoking(() => Render(box)).Should().NotThrow("the pseudo path writes every member");
        FluentActions.Invoking(() => Render(new Simulated(SimulatedState.Hovered | SimulatedState.Focused, box)))
            .Should().NotThrow("the simulated path has a place for every declaration the pseudo path writes");
    }

    /// <summary>A value for each type a <see cref="StyleDiff"/> member holds. A type with no sample
    /// fails here by name, which is the question to answer when the vocabulary grows.</summary>
    private static object Sample(Type type) => (Nullable.GetUnderlyingType(type) ?? type) switch
    {
        var t when t == typeof(ColorToken) => Theme.FocusRing,
        var t when t == typeof(float) => 0.5f,
        var t when t == typeof(int) => 3,
        var t when t == typeof(LinearGradient) => new LinearGradient(Theme.Surface, Theme.SurfaceSubtle),
        var t when t == typeof(Transform2D) => Transform2D.Translate(0, -2),
        var t when t == typeof(IReadOnlyList<ShadowSpec>) => new[] { new ShadowSpec(4, 8, 0, Theme.Border) },
        var t => throw new InvalidOperationException(
            $"A StyleDiff member of type {t.Name} has no sample here: add one, and a place for what it writes in ApplyDiff."),
    };

    /// <summary>
    /// The pictured hover writes what the real one does, the composed lists included: the raised
    /// elevation keeps the glow beside it, and the lift moves the box (#504).
    /// </summary>
    [Fact]
    public void AHoveredSubtree_DrawsEveryMemberOfTheDiff()
    {
        var glow = new ShadowSpec(0, 24, 0, new ColorToken(Color.FromRgb(0x44, 0x88, 0xFF)));
        var (_, css) = Render(new Simulated(SimulatedState.Hovered, new Primitives.Box(new BoxStyle
        {
            Elevation = 1,
            Shadow = glow,
            Hover = new StyleDiff { Elevation = 3, Transform = Transform2D.Translate(0, -2) },
        }, new Text("x", TypeRole.BodyM))));

        css.Should().Contain($"{{box-shadow:{TokenCss.RingSlot}, {TokenCss.Shadow(Theme.Elevation(3))}, {TokenCss.Shadow(glow)}}}");
        css.Should().Contain("{transform:translate(0, -2px)}");
        css.Should().NotContain(":hover");
    }

    /// <summary>
    /// ONE declaration per property, not two. Every atomic class has equal specificity, so an
    /// element carrying both the base and the simulated colour would be decided by stylesheet
    /// insertion order — by whatever the rest of the page happened to emit first.
    /// </summary>
    [Fact]
    public void TheSimulatedDeclaration_REPLACESTheBaseOne()
    {
        var (html, _) = Render(new Simulated(SimulatedState.Hovered, Hoverable()));

        var classes = System.Text.RegularExpressions.Regex.Match(html, "class=\"([^\"]*)\"").Groups[1].Value;
        classes.Split(' ').Should().OnlyHaveUniqueItems();
        // The surface colour is gone: it was replaced, not overlaid.
        Render(new Simulated(SimulatedState.Hovered, Hoverable())).Css
            .Should().NotContain("light-dark(#ffffff, #14181e)");
    }

    [Fact]
    public void APressedSubtree_CarriesTheSameDeclarationAPressDoes()
    {
        var pressable = new Pressable(new Primitives.Box(new BoxStyle { Background = Theme.Surface },
            new Text("Go", TypeRole.Label)))
        {
            PressedBackground = Theme.Colors(Variant.Primary).Base,
        };

        var (html, _) = Render(new Simulated(SimulatedState.Pressed, pressable));

        html.Should().Contain("eq-pressed");
        html.Should().Contain("eq-pressable", "it is still the control it is picturing");
    }

    /// <summary>Nested previews COMBINE — a hovered card holding a pressed button is two nodes, and
    /// the inner one must not turn the outer's hover off.</summary>
    [Fact]
    public void NestedSimulations_Combine()
    {
        var inner = new Pressable(new Primitives.Box(new BoxStyle { Background = Theme.Surface },
            new Text("Go", TypeRole.Label)))
        {
            PressedBackground = Theme.Colors(Variant.Primary).Base,
        };
        var card = new Primitives.Box(new BoxStyle
        {
            Background = Theme.Surface,
            Hover = new StyleDiff { Elevation = 2 },
        }, new Simulated(SimulatedState.Pressed, inner));

        var (html, css) = Render(new Simulated(SimulatedState.Hovered, card));

        html.Should().Contain("eq-pressed", "the inner press survives the outer hover");
        css.Should().NotContain(":hover", "and the outer hover is drawn, not deferred to a pseudo");
    }

    /// <summary>The state does not LEAK past the node — a preview beside an ordinary control must
    /// leave that control alone.</summary>
    [Fact]
    public void TheStateEndsWithTheNode()
    {
        var row = new Row(gap: Space.S2)
        {
            new Simulated(SimulatedState.Hovered, Hoverable()),
            Hoverable(),
        };

        var (_, css) = Render(row);

        css.Should().Contain(":hover", "the sibling still hovers for real");
    }

    [Fact]
    public void WithoutTheNode_NothingChanges()
    {
        var (html, css) = Render(Hoverable());

        html.Should().NotContain("eq-pressed");
        css.Should().Contain(":hover");
    }
}
