using eQuantic.UI.Components;
using eQuantic.UI.Core;
using eQuantic.UI.Primitives;
using FluentAssertions;
using SharedButton = eQuantic.UI.Components.Button;
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
    public void Box_ExactStyleString_CrossPin()
    {
        // CROSS-PIN: this literal is asserted verbatim by the TS runtime's lowering.spec.ts — the two
        // realizers must produce byte-identical style strings (hydration parity).
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

        Render(box).Attributes["style"].Should().Be(
            "flex-shrink: 0; width: 120px; height: 40px; padding: 0 16px 0 16px; " +
            "background-color: light-dark(#0050a0, #5ca2e8); " +
            "border: 1px solid light-dark(#c9ced6, #3d4754); border-radius: 10px; box-sizing: border-box");
    }

    /// <summary>
    /// FIXED means fixed. A flex item's <c>flex-shrink</c> is 1 by default, so a box with an
    /// explicit width beside an overflowing sibling was quietly squeezed — the code editor's gutter
    /// lost width on exactly the lines whose code ran past the viewport, and every number in it slid
    /// left. Photon never shrinks a fixed box; the web mirror must not either.
    /// </summary>
    [Fact]
    public void AFixedSize_DoesNotShrink()
    {
        var gutter = new Primitives.Box(new BoxStyle { Width = 68, Height = SizeValue.Fill });
        Render(gutter).Attributes["style"].Should().Contain("flex-shrink: 0");

        // HUG and FILL still shrink, by design: that is how a long label ellipsizes instead of
        // pushing its row wider than the window.
        Render(new Primitives.Box(new BoxStyle())).Attributes["style"].Should().NotContain("flex-shrink");
        Render(new Primitives.Box(new BoxStyle { Width = SizeValue.Fill }))
            .Attributes["style"].Should().NotContain("flex-shrink");
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

    /// <summary>
    /// The slant is an AXIS on the style, so a paragraph that asks for it says so in one
    /// declaration — the same shape the mono face already had. Cross-pinned with
    /// <c>lowering.spec.ts</c>: SSR and hydration have to compute the same class for it.
    /// </summary>
    [Fact]
    public void ItalicText_StatesTheSlant()
    {
        var style = Render(new Primitives.Text("ementa", TypeRole.BodyM) { Italic = true })
            .Attributes["style"]!;

        style.Should().Contain("font-style: italic");
    }

    /// <summary>
    /// A RUN's own emphasis: the slant that markdown's <c>*single asterisk*</c> means, and the
    /// size an inline code span sits at. Both belong to the run rather than to the paragraph,
    /// which is the whole reason runs exist.
    /// </summary>
    [Fact]
    public void ARun_CarriesItsOwnSlantAndItsOwnSize()
    {
        var code = new TypeStyle(13.5f, 20, FontWeight.Regular, 0, 1.3f, Mono: true);
        var text = new Primitives.Text("", TypeRole.BodyM)
        {
            Spans =
            [
                new TextRun("plain "),
                new TextRun("emphasis") { Italic = true },
                new TextRun(" and "),
                new TextRun("code", Mono: true) { StyleOverride = code },
            ],
        };

        var runs = Render(text).Children;
        runs[1].Attributes["style"].Should().Contain("font-style: italic");
        runs[3].Attributes["style"].Should().Contain("font-size: 13.5px");
        // An unemphasised run states nothing — a declaration nobody asked for is a class nobody
        // shares, and every extra class is a byte on every paragraph on the page. (The empty
        // attribute itself never reaches a page: the atomizer drops a style with no declarations.)
        runs[0].Attributes["style"].Should().BeEmpty();
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

    /// <summary>
    /// A POSITIVE basis is the size the line breaker measures against, so it decides whether a
    /// wrapping row BREAKS or squeezes. Regression: the realizer hardcoded `{Flex} 1 0%` while the
    /// TS twin emitted the node's own basis and shrink — so SSR and hydration disagreed on the
    /// class, and on the first paint a wrapping row measured ZERO for a child asking for 220dp,
    /// never broke the line, and shrank the child to nothing.
    /// </summary>
    [Fact]
    public void Flexible_CarriesItsBasisAndShrink_SoAWrappingRowCanBreakInsteadOfSqueezing()
    {
        var row = new Row(gap: 0) { Wrap = true };
        row.Add(new Flexible(new Primitives.Text("t"), flex: 1, basis: 220));
        row.Add(new Flexible(new Primitives.Text("t"), flex: 1, basis: 180, shrink: 0));
        var node = Render(row);

        node.Children[0].Attributes["style"].Should().Contain("flex: 1 1 220px");
        node.Children[1].Attributes["style"].Should().Contain("flex: 1 0 180px",
            "shrink is the node's, not a default");
    }

    /// <summary>A basis of zero keeps the historical `0%`, which is what native's leftover-by-weight
    /// distribution matches — the default must not change shape.</summary>
    [Fact]
    public void Flexible_WithoutABasis_KeepsPercentZero()
    {
        var row = new Row(gap: 0);
        row.Add(new Flexible(new Primitives.Text("t"), flex: 2));
        Render(row).Children[0].Attributes["style"].Should().Contain("flex: 2 1 0%");
    }

    /// <summary>
    /// `overflow` and `text-overflow` are INERT on a non-replaced inline box, and a Text lowers to a
    /// `span`. Without a display the ellipsis contract was a no-op: a squeezed single-line Text
    /// painted its full width straight out of its parent — in a topbar, the placeholder ran over the
    /// ⌘K chip and off the screen.
    /// </summary>
    [Fact]
    public void SingleLineText_IsABlock_SoTheEllipsisContractApplies()
    {
        var node = Render(new Primitives.Text("Saldo disponível", TypeRole.Caption, maxLines: 1));

        var style = node.Attributes["style"];
        style.Should().Contain("display: block");
        style.Should().Contain("overflow: hidden");
        style.Should().Contain("text-overflow: ellipsis");
    }

    /// <summary>
    /// Spec B14 / hydration identity: a WEIGHTED spacer is the ratio's counterweight (ProgressBar), so
    /// AnimateChanges must animate its weight exactly as it does the Flexible fill's. Regression: the
    /// realizer emitted the transition for Flexible only, so SSR shipped a bare `flex: n 1 0%` while the
    /// TS twin (lowerSpacer) added the transition — the client repainted instead of adopting.
    /// </summary>
    [Fact]
    public void Spacer_AnimateChanges_TransitionsItsWeight_LikeFlexible()
    {
        var row = new Row(gap: 0);
        row.Add(new Flexible(new Primitives.Text("t"), flex: 42) { AnimateChanges = true });
        row.Add(new Spacer { Flex = 58, AnimateChanges = true });
        var node = Render(row);

        const string transition = "transition: flex-grow var(--eq-motion-base) var(--eq-curve-standard)";
        node.Children[0].Attributes["style"].Should().Contain(transition);
        node.Children[1].Attributes["style"].Should().Contain(transition, "the counterweight must glide with the fill");
        node.Children[1].Attributes["style"].Should().Contain("flex: 58 1 0%");
    }

    [Fact]
    public void Spacer_WithoutAnimateChanges_Snaps()
    {
        var row = new Row(gap: 0);
        row.Add(new Spacer { Flex = 58 });

        Render(row).Children[0].Attributes["style"].Should().NotContain("transition",
            "the component omits the flag on a regression so the change SNAPS (forward-only honesty)");
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
