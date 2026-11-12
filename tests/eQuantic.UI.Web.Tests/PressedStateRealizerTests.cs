using System.Text.RegularExpressions;
using eQuantic.UI.Web;
using eQuantic.UI.Primitives;
using FluentAssertions;
using SharedButton = eQuantic.UI.Components.Button;
using Variant = eQuantic.UI.Primitives.Variant;

namespace eQuantic.UI.Web.Tests;

/// <summary>
/// A pressed fill on the web is the <c>Pressed</c> diff of the box it fills (#616): an atomic rule in
/// the pressed family, like every other member of the diff, and no mechanic of the control's. Since
/// #508 it could also be said as <c>Pressable.PressedBackground</c>, drawn by a custom property, two
/// <c>!important</c> rules and two markers; those are gone, and these facts are what keep them gone.
/// </summary>
public class PressedStateRealizerTests
{
    private static readonly PhotonTheme Theme = PhotonTheme.Instance;

    private static (HtmlElement Element, string Css) Lower(VisualNode node)
    {
        var sink = new StyleSink();
        var element = WebRealizer.Lower(node, Theme, 1f, sink);
        return (element, sink.Css);
    }

    /// <summary>
    /// The selector that writes <paramref name="declaration"/> for one of <paramref name="element"/>'s
    /// classes, and its specificity as the browser computes it: (ids, classes and pseudo-classes and
    /// attributes, types), with <c>:where()</c> counting nothing, which every selector this sheet
    /// writes is made of.
    /// </summary>
    private static (string Selector, (int, int, int) Specificity) RuleWriting(string css, HtmlElement element,
        string declaration)
    {
        foreach (var cls in (element.ClassName ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            // After a closing brace, or the opening one of the hover family's @media gate.
            var rule = Regex.Match(css, $@"(?:^|[{{}}])([^{{}}@]*\.{Regex.Escape(cls)}(?:[:\[][^{{}}]*)?)\{{{declaration}\}}");
            if (!rule.Success) continue;
            var selector = rule.Groups[1].Value.Trim();
            selector.Should().NotContain(",", "one selector, whose specificity is the rule's");
            return (selector, Specificity(selector));
        }
        throw new InvalidOperationException($"no rule of the element writes {declaration}");
    }

    /// <summary>
    /// A selector's specificity, as Selectors Level 4 computes it: <c>:where()</c> weighs nothing,
    /// <c>:not()</c>, <c>:is()</c> and <c>:has()</c> weigh their heaviest argument, and the state
    /// families carry both (#616), so a regex count would not do.
    /// </summary>
    private static (int, int, int) Specificity(string selector)
    {
        var at = 0;
        return Selector(selector, ref at);

        static (int, int, int) Selector(string text, ref int at)
        {
            int ids = 0, classes = 0, types = 0;
            var startOfCompound = true;
            while (at < text.Length && text[at] != ')' && text[at] != ',')
            {
                var c = text[at];
                if (c is ' ' or '>' or '+' or '~') { at++; startOfCompound = true; continue; }
                if (c == '#') { ids++; at++; Ident(text, ref at); }
                else if (c == '.') { classes++; at++; Ident(text, ref at); }
                else if (c == '[') { classes++; at = text.IndexOf(']', at) + 1; }
                else if (c == '*') at++;
                else if (c == ':' && at + 1 < text.Length && text[at + 1] == ':') { types++; at += 2; Ident(text, ref at); }
                else if (c == ':')
                {
                    at++;
                    var name = Ident(text, ref at);
                    if (at < text.Length && text[at] == '(')
                    {
                        at++;
                        var heaviest = (0, 0, 0);
                        while (true)
                        {
                            var argument = Selector(text, ref at);
                            if (Compare(argument, heaviest) > 0) heaviest = argument;
                            if (text[at] == ',') { at++; continue; }
                            at++; // ')'
                            break;
                        }
                        if (name is "not" or "is" or "has")
                        { ids += heaviest.Item1; classes += heaviest.Item2; types += heaviest.Item3; }
                        else if (name != "where") classes++;
                    }
                    else classes++;
                }
                else { if (startOfCompound) types++; Ident(text, ref at); }
                startOfCompound = false;
            }
            return (ids, classes, types);
        }

        static string Ident(string text, ref int at)
        {
            var from = at;
            while (at < text.Length && (char.IsLetterOrDigit(text[at]) || text[at] is '-' or '_')) at++;
            return text[from..at];
        }
    }

    [Theory]
    [InlineData(".eq-x", 0, 1, 0)]
    [InlineData(".eq-x:hover", 0, 2, 0)]
    [InlineData(":where(.eq-pressable:hover) .eq-x.eq-x:not(:where(.eq-pressable:not(:hover) .eq-x))", 0, 2, 0)]
    [InlineData(".eq-pressable:focus-visible .eq-x:not(:where(.eq-pressable:focus-visible .eq-pressable .eq-x))", 0, 3, 0)]
    [InlineData(".eq-pressable.eq-pressable:active:not(:where(:has(.eq-pressable:active))) .eq-x:not(:where(.eq-pressable:not(:active) .eq-x))", 0, 4, 0)]
    [InlineData(".a:not(.b, #c) li", 1, 1, 1)]
    [InlineData(":where(.eq-pressable > :first-child)", 0, 0, 0)]
    public void TheSpecificityReader_CountsAsTheBrowserDoes(string selector, int ids, int classes, int types) =>
        Specificity(selector).Should().Be((ids, classes, types));

    private static int Compare((int, int, int) a, (int, int, int) b) =>
        a.Item1 != b.Item1 ? a.Item1.CompareTo(b.Item1)
        : a.Item2 != b.Item2 ? a.Item2.CompareTo(b.Item2)
        : a.Item3.CompareTo(b.Item3);

    [Fact]
    public void APressedButton_FillsItsSurface_AndTheControlCarriesNoFillOfItsOwn()
    {
        var (button, css) = Lower(new SharedButton("Save", onPressed: () => { }));

        var marks = (button.ClassName ?? "").Split(' ');
        marks.Should().Contain("eq-pressable").And.NotContain("eq-press-fill",
            "the control is marked as one, and for nothing of the fill's");
        (button.Render().Attributes.GetValueOrDefault("style") ?? "").Should().NotContain("--eq-pressed-bg",
            "no custom property carries a fill to a rule of the control's");

        var surface = (HtmlElement)button.Children[0];
        var pressed = TokenCss.Value(Theme.Colors(Variant.Primary).Pressed);
        RuleWriting(css, surface, $@"background-color:[^{{}}]*{Regex.Escape(pressed)}[^{{}}]*")
            .Selector.Should().StartWith(".eq-pressable.eq-pressable:active:not(:where(:has(.eq-pressable:active))) .",
                "the surface's own Pressed diff, in the pressed family");
    }

    /// <summary>
    /// What stood in for the <c>!important</c>, measured on the box that has all four: the pressed
    /// fill's selector outranks the box's own fill, its hover's and its control's focus's, so it wins
    /// whatever order the rules reach the sheet in. Should a state family ever weaken, this fails
    /// before anyone is tempted to bring an <c>!important</c> back.
    /// </summary>
    [Fact]
    public void APressedFill_OutranksEveryOtherFillOfItsBox_BySpecificityAlone()
    {
        static ColorToken Solid(byte r) => new(Color.FromRgb(r, 0x20, 0x30));
        var (element, css) = Lower(new Box(new BoxStyle
        {
            Width = 10, Height = 10,
            Background = Solid(0x01),
            Hover = new StyleDiff { Background = Solid(0x02) },
            Focus = new StyleDiff { Background = Solid(0x03) },
            Pressed = new StyleDiff { Background = Solid(0x04) },
        }));

        (string, (int, int, int)) Fill(byte r) =>
            RuleWriting(css, element, $"background-color:{Regex.Escape(TokenCss.Value(Solid(r)))}");
        var (_, own) = Fill(0x01);
        var (_, hover) = Fill(0x02);
        var (_, focus) = Fill(0x03);
        var (pressedSelector, pressed) = Fill(0x04);

        Compare(pressed, own).Should().BePositive($"`{pressedSelector}` beats the box's own fill");
        Compare(pressed, hover).Should().BePositive($"`{pressedSelector}` beats the hover's fill");
        Compare(pressed, focus).Should().BePositive($"`{pressedSelector}` beats the focus's fill");
    }

    /// <summary>
    /// The same order for a box INSIDE a control, whose hover is the control's since #614: the
    /// control's hover family stands where the box's own :hover stood, over the box's fill and under
    /// the control's focus and press, by its selector alone.
    /// </summary>
    [Fact]
    public void InsideAControl_TheStatesKeepTheirOrder_BySpecificityAlone()
    {
        static ColorToken Solid(byte r) => new(Color.FromRgb(r, 0x30, 0x40));
        var (control, css) = Lower(new Pressable(new Box(new BoxStyle
        {
            Width = 10, Height = 10,
            Background = Solid(0x01),
            Hover = new StyleDiff { Background = Solid(0x02) },
            Focus = new StyleDiff { Background = Solid(0x03) },
            Pressed = new StyleDiff { Background = Solid(0x04) },
        }), () => { }));
        var element = (HtmlElement)control.Children[0];

        (string, (int, int, int)) Fill(byte r) =>
            RuleWriting(css, element, $"background-color:{Regex.Escape(TokenCss.Value(Solid(r)))}");
        var (_, own) = Fill(0x01);
        var (hoverSelector, hover) = Fill(0x02);
        var (_, focus) = Fill(0x03);
        var (_, pressed) = Fill(0x04);

        hoverSelector.Should().StartWith(":where(.eq-pressable:hover) .", "the hover is the control's");
        Compare(hover, own).Should().BePositive($"`{hoverSelector}` beats the box's own fill");
        Compare(focus, hover).Should().BePositive($"the focus beats `{hoverSelector}`");
        Compare(pressed, hover).Should().BePositive($"the press beats `{hoverSelector}`");
    }

    [Fact]
    public void NoGeneratedRule_WritesAFillWithImportant()
    {
        var (_, atomic) = Lower(new SharedButton("Save", onPressed: () => { }));
        var css = ThemeStylesheet.Generate(Theme) + atomic;

        Regex.Matches(css, @"background(?:-color)?\s*:[^;{}]*!important").Should().BeEmpty(
            "an !important fill outranks every state of every box under it, which is the defect two "
            + "mechanisms for one fact produced; a pressed fill wins by its selector (#616)");
        css.Should().NotContain("--eq-pressed-bg").And.NotContain("eq-press-fill");
    }

    /// <summary>
    /// #508's defect, which lived only in the mechanism #616 removed: a control with no pressed fill
    /// went transparent while pressed, because the swap was a var() with nothing to resolve. A box
    /// that declares no pressed background now takes no pressed rule at all.
    /// </summary>
    [Fact]
    public void AControlWithNoPressedFill_TakesNoPressedFillRule()
    {
        var (_, css) = Lower(new Pressable(new Box(new BoxStyle { Background = Theme.Surface }), () => { }));

        css.Should().NotMatchRegex(@"\.eq-pressable\.eq-pressable:active[^{}]*\{background");
    }

    [Fact]
    public void DisabledPressable_HasNoPressedMechanics()
    {
        var node = WebRealizer.Lower(new SharedButton("Save", onPressed: () => { }) { Disabled = true }, Theme).Render();
        node.Attributes.Should().NotContainKey("class");
    }

    [Fact]
    public void GeneratedCss_KeepsTheSurfacesOwnTransition()
    {
        var css = ThemeStylesheet.Generate(Theme);
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
        var css = ThemeStylesheet.Generate(Theme);
        css.Should().Contain(
            ".eq-pressable [disabled], .eq-pressable [aria-disabled=\"true\"] { pointer-events: none; }");
    }
}
