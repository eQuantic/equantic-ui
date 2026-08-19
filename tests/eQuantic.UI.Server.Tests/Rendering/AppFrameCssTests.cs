using System.Text.RegularExpressions;
using eQuantic.UI.Server;
using FluentAssertions;
using Xunit;

namespace eQuantic.UI.Server.Tests.Rendering;

/// <summary>
/// The framework invariants the app shell states before any app style: <c>#app</c> is the web mirror
/// of the native window, and the page root is its single child.
/// <para>
/// This exists because of one bug, and the bug was invisible to every other kind of check. The root
/// was STRETCHED to a definite one-viewport frame, and <c>#app &gt; * { min-height: 0 }</c> let it
/// stay at exactly that while its content was ten times taller. A flex child then had to absorb the
/// overflow, and a child whose <c>overflow</c> is not <c>visible</c> — anything the vocabulary marks
/// <c>Clip = true</c> — has an automatic minimum size of ZERO in CSS, so it was crushed to a 0px-tall
/// box. The consumer site lost its entire hero section: server-rendered, correct in the HTML, present
/// in the DOM, drawn at zero height. Reading the markup could not see it; only a laid-out page could.
/// </para>
/// <para>
/// So the shape is pinned here: the row is a definite viewport (a percentage height inside must
/// resolve against the VIEWPORT, not against whatever the content came to), the root is start-aligned
/// rather than stretched, and its floor is the frame rather than zero — "at least the frame, and free
/// to be taller". Native has no equivalent trap: clipping a node there has never changed its size.
/// </para>
/// </summary>
public class AppFrameCssTests
{
    /// <summary>The template's own text: the frame rules are a constant of the shell, not something
    /// a request decides, so there is nothing to fill in first.</summary>
    private static string Shell() =>
        HtmlTemplateEngine.FromResource("eQuantic.UI.Server.Templates.app-shell.html")
            .Render((Action<TemplateContext>)(_ => { }));

    private static string AppRules()
    {
        var shell = Shell();
        var match = Regex.Match(shell, @"#app\s*\{(?<frame>[^}]*)\}\s*#app\s*>\s*\*\s*\{(?<root>[^}]*)\}",
            RegexOptions.Singleline);
        match.Success.Should().BeTrue("the shell must still state the #app frame and its root child");
        return match.Groups["frame"].Value + "||" + match.Groups["root"].Value;
    }

    [Fact]
    public void TheRootChild_IsNeverAllowedToShrinkBelowTheFrame()
    {
        // min-height:0 is the exact permission that crushed the hero. Its floor is the frame.
        var root = AppRules().Split("||")[1];

        root.Should().Contain("min-height: 100%");
        root.Should().NotMatch("*min-height: 0*");
    }

    [Fact]
    public void TheRowIsADefiniteViewport_SoAPercentageHeightInsideResolvesAgainstIt()
    {
        // An APP page's root asks Height=Fill → height:100%. Against an auto row that is the
        // content's height, which is not a viewport and not what Fill means.
        var frame = AppRules().Split("||")[0];

        frame.Should().Contain("grid-template-rows: 100dvh");
        frame.Should().Contain("height: 100dvh");
    }

    [Fact]
    public void TheRootIsStartAligned_SoADocumentPageCanBeTallerThanTheFrame()
    {
        AppRules().Split("||")[0].Should().Contain("align-items: start");
    }

    [Fact]
    public void EveryViewportUnit_IsStatedTwice_SoAMobileBrowserWithoutDvhStillGetsAFrame()
    {
        // dvh is the correct unit and vh is the fallback: order matters, the later one wins where
        // it parses. A single dvh declaration silently gives older mobile Safari no frame at all.
        var frame = AppRules().Split("||")[0];

        frame.IndexOf("height: 100vh", StringComparison.Ordinal)
            .Should().BeLessThan(frame.IndexOf("height: 100dvh", StringComparison.Ordinal));
        frame.Should().Contain("grid-template-rows: 100vh");
    }
}
