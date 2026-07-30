using System.Runtime.CompilerServices;
using System.Text;
using eQuantic.UI.Core;
using eQuantic.UI.Primitives;
using FluentAssertions;

namespace eQuantic.UI.Web.Tests;

/// <summary>
/// The atomic style pipeline's C# half (docs/STYLE-SEMANTICS-PLAN.md §2). The FIXTURE test is the
/// load-bearing one: it pins declaration→class for a canonical set into a shared JSON the vitest
/// twin replays — if either side's hash, var-rewrite or class format drifts, one of the two suites
/// fails and hydration-by-class-identity would have broken. Refresh with EQ_UPDATE_ATOMIC_FIXTURE=1.
/// </summary>
public class StyleAtomizerTests
{
    private static string FixturePath([CallerFilePath] string sourcePath = "")
    {
        var repoRoot = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(sourcePath)!, "..", ".."));
        return Path.Combine(repoRoot, "src", "eQuantic.UI.Runtime", "src", "shared", "style-atomizer.fixture.json");
    }

    // The canonical declaration set: plain values, theme-var rewrites, composites (border/gradient).
    private static readonly (string Prop, string Value)[] Canonical =
    [
        ("padding", "0 16px 0 16px"),
        ("border-radius", "10px"),
        ("box-sizing", "border-box"),
        ("width", "100%"),
        ("flex", "1 1 0%"),
        ("background-color", TokenCss.Value(PhotonTheme.Instance.Surface)),
        ("color", TokenCss.Value(PhotonTheme.Instance.TextPrimary)),
        ("border", $"1px solid {TokenCss.Value(PhotonTheme.Instance.BorderStrong)}"),
        ("background-color", TokenCss.Value(PhotonTheme.Instance.Colors(Variant.Primary).Base)),
    ];

    [Fact]
    public void CanonicalDeclarations_MatchTheSharedFixture()
    {
        var vars = ThemeVarMap.For(PhotonTheme.Instance);
        var sink = new StyleSink();
        var json = new StringBuilder("[\n");
        var first = true;
        foreach (var (prop, value) in Canonical)
        {
            var rewritten = vars.Rewrite(value);
            var className = sink.ClassFor(prop, rewritten);
            if (!first) json.Append(",\n");
            first = false;
            json.Append($"  {{\"prop\":\"{prop}\",\"value\":{System.Text.Json.JsonSerializer.Serialize(value)}," +
                        $"\"rewritten\":{System.Text.Json.JsonSerializer.Serialize(rewritten)},\"class\":\"{className}\"}}");
        }
        json.Append("\n]\n");

        var path = FixturePath();
        if (Environment.GetEnvironmentVariable("EQ_UPDATE_ATOMIC_FIXTURE") == "1")
        {
            File.WriteAllText(path, json.ToString());
            return;
        }

        File.Exists(path).Should().BeTrue($"run once with EQ_UPDATE_ATOMIC_FIXTURE=1 to create {path}");
        File.ReadAllText(path).Should().Be(json.ToString(),
            "the C# atomizer and the TS twin must hash identically — regenerate with EQ_UPDATE_ATOMIC_FIXTURE=1 and re-run vitest");
    }

    [Fact]
    public void ThemeColors_RewriteToVariables_WithResolvedFallbacks()
    {
        var vars = ThemeVarMap.For(PhotonTheme.Instance);
        var surface = TokenCss.Value(PhotonTheme.Instance.Surface);

        vars.Rewrite(surface).Should().Be($"var(--eq-color-surface, {surface})");
        // Composite values rewrite inside (the border shorthand keeps its structure).
        vars.Rewrite($"1px solid {surface}").Should().Be($"1px solid var(--eq-color-surface, {surface})");
        // Unknown colors pass through untouched.
        vars.Rewrite("#123456").Should().Be("#123456");
    }

    [Fact]
    public void Sink_Deduplicates_AndEmitsSortedRules()
    {
        var sink = new StyleSink();
        var a = sink.ClassFor("padding", "16px");
        var b = sink.ClassFor("padding", "16px");
        var c = sink.ClassFor("gap", "8px");

        a.Should().Be(b, "identical declarations are the same class");
        sink.Css.Count(ch => ch == '{').Should().Be(2, "two DISTINCT declarations, two rules");
        sink.Css.Should().Contain($".{a}{{padding:16px}}").And.Contain($".{c}{{gap:8px}}");
    }

    [Fact]
    public void AtomizedTree_CarriesSortedClasses_AndOnlyCustomPropsInline()
    {
        var style = new HtmlStyle
        {
            Padding = "16px",
            BackgroundColor = TokenCss.Value(PhotonTheme.Instance.Surface),
            CustomProperties = new Dictionary<string, string> { ["--eq-x"] = "42px" },
        };
        var sink = new StyleSink();
        var classes = StyleAtomizer.Atomize(style, ThemeVarMap.For(PhotonTheme.Instance), sink);

        classes.Split(' ').Should().HaveCount(2).And.BeInAscendingOrder(
            "the class attribute is order-independent (sorted) so both producers emit the same string");
        style.CustomProperties.Should().ContainKey("--eq-x", "tier-3 inputs are not atomized");
    }
}
