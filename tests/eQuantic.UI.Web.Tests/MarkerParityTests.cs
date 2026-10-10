using System.Text.RegularExpressions;
using eQuantic.UI.Primitives;
using eQuantic.UI.Web;
using FluentAssertions;
using Xunit;

namespace eQuantic.UI.Web.Tests;

/// <summary>
/// A DOM marker is written by one half of the runtime and read by another, and NOTHING type-checks
/// that pairing across the C#/TypeScript line.
/// <para>
/// The `Sticky` → `Pinned` rename moved both emitters — this realizer and the client lowering — and
/// missed the READER in <c>sticky-offset.ts</c>. Every fragment link on a live site landed behind
/// the header for a whole release, and both suites stayed green: the reader's spec set the old
/// attribute on a fixture it built itself, so it tested the reader against markup nothing emits.
/// </para>
/// <para>
/// The TypeScript halves now share <c>markers.ts</c>, which makes them unable to disagree. This is
/// the other seam: the marker THIS realizer emits, pinned against that file. Same shape as the
/// atomizer and icon fixtures — the file on disk is the single source, and a rename that reaches
/// one language and not the other fails here.
/// </para>
/// </summary>
public class MarkerParityTests
{
    private static string MarkerFromTypeScript(string constant)
    {
        var here = new DirectoryInfo(AppContext.BaseDirectory);
        while (here is not null && !Directory.Exists(Path.Combine(here.FullName, "src", "eQuantic.UI.Runtime")))
            here = here.Parent;
        here.Should().NotBeNull("the test has to find the repository root to read markers.ts");

        var source = File.ReadAllText(Path.Combine(
            here!.FullName, "src", "eQuantic.UI.Runtime", "src", "shared", "markers.ts"));
        // Tolerant of formatting, strict about the VALUE. A pin that breaks when prettier changes
        // a quote style reports drift that did not happen, and a guard people learn to re-run
        // instead of read is worse than none.
        var match = Regex.Match(
            source, $@"export\s+const\s+{Regex.Escape(constant)}\s*=\s*['""]([^'""]+)['""]");
        match.Success.Should().BeTrue(
            $"markers.ts must declare {constant} as a string literal — this test reads it as the "
            + "single source both languages answer to");
        return match.Groups[1].Value;
    }

    [Fact]
    public void ThePinnedMarker_IsTheOneTheRuntimeReads()
    {
        // DataAttributes are emitted with the `data-` prefix, which the TypeScript side spells out.
        var emitted = "data-" + Marker(new Pinned(new Box(new BoxStyle())));
        emitted.Should().Be(MarkerFromTypeScript("PINNED_MARKER"),
            "the realizer writes what sticky-offset.ts queries, or the anchor offset silently "
            + "publishes 0px and every Bookmark lands behind the chrome");
    }

    /// <summary>The attribute the runtime SETS on a scrolled header, and the one this realizer's
    /// scrolled rules select on (#506): a rename that reached one side only would leave every
    /// header unfrosted, and both suites green.</summary>
    [Fact]
    public void TheScrolledMarker_IsTheOneTheRuntimeSets()
    {
        var sink = new StyleSink();
        WebRealizer.Lower(new Pinned(new Box(new BoxStyle()))
        {
            ScrolledStyle = new StyleDiff { Opacity = 0.5f },
        }, PhotonTheme.Instance, styles: sink);

        sink.Css.Should().Contain($"[{MarkerFromTypeScript("SCROLLED_MARKER")}]{{",
            "the realizer's scrolled rules select on what scrolled-pinned.ts sets");
    }

    private static string Marker(VisualNode node)
    {
        var lowered = WebRealizer.Lower(node, PhotonTheme.Instance);
        var found = Walk(lowered).FirstOrDefault(e => e.DataAttributes is { Count: > 0 });
        found.Should().NotBeNull("a Pinned must carry a marker for the runtime to measure it");
        return found!.DataAttributes!.Keys.Single();
    }

    private static IEnumerable<HtmlElement> Walk(HtmlElement element)
    {
        yield return element;
        foreach (var child in element.Children.OfType<HtmlElement>())
            foreach (var nested in Walk(child)) yield return nested;
    }
}
