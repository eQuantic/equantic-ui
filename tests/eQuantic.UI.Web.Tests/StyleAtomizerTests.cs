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
        // The mono stack, which the SERVER writes here and the CLIENT writes in lowering.ts. Its
        // atomic class is a hash of the declaration, so the two spellings have to be identical to
        // the character or a page mints two classes for one style and re-paints on hydration.
        // Pinning it through the shared fixture is what makes a drift on either side fail a build.
        ("font-family", TokenCss.MonoStack),
        // A VENDOR PAIR: both spellings are one declaration and must land on ONE class, whose rule
        // carries both names. Alone, the prefixed one is dropped whole by an engine that only takes
        // the standard name — the class was on the element and did nothing.
        ("backdrop-filter", "blur(8px)"),
        ("-webkit-backdrop-filter", "blur(8px)"),
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

    /// <summary>
    /// The pair is ONE class, and its rule says both names — with the standard one LAST, so an
    /// engine that understands both lands on it.
    /// </summary>
    [Fact]
    public void AVendorPrefix_SharesItsClassWithTheStandardProperty()
    {
        var sink = new StyleSink();

        var standard = sink.ClassFor("backdrop-filter", "blur(8px)");
        var prefixed = sink.ClassFor("-webkit-backdrop-filter", "blur(8px)");

        prefixed.Should().Be(standard, "a prefix is the same declaration written for another engine");
        sink.Css.Should().Contain("-webkit-backdrop-filter:blur(8px);backdrop-filter:blur(8px)");
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

    /// <summary>
    /// A token color that is a PREFIX of a longer hex literal must NOT be rewritten inside it.
    /// Hex colors are variable-length, so an opaque token (<c>#ffffff</c>) is a prefix of its own
    /// translucent form (<c>#ffffff0a</c>); rewriting the prefix strands the alpha outside the var
    /// (<c>var(--x, #ffffff)0a</c>) — invalid CSS the browser drops entirely, so the declaration
    /// silently disappears. Found by the site dogfood: a hero grid line of white-at-4% vanished.
    /// </summary>
    [Fact]
    public void TranslucentVariantOfATokenColor_IsNotCorruptedByPrefixRewriting()
    {
        var theme = PhotonTheme.Instance;
        var vars = ThemeVarMap.For(theme);
        var surface = TokenCss.Value(theme.Surface);

        // Sanity: the token itself still rewrites.
        vars.Rewrite(surface).Should().Be($"var(--eq-color-surface, {surface})");

        // A LONGER hex literal that merely STARTS with the token must pass through untouched.
        // The real-world trigger (found by the site dogfood) is a same-in-both-modes token that
        // serializes as a bare hex — an opaque `#ffffff` next to a hero grid line of `#ffffff0a`.
        var extended = surface + "0a";
        vars.Rewrite(extended).Should().Be(extended,
            "the trailing hex digits extend the literal into a DIFFERENT color; rewriting the "
            + "prefix would strand them outside the var() and produce CSS the browser drops");

        // Same rule inside a composite value (the grid pattern's gradient layers).
        var gradient = $"linear-gradient(to right, {extended} 1px, transparent 1px)";
        vars.Rewrite(gradient).Should().Be(gradient);

        // And a genuine occurrence inside a composite still rewrites.
        vars.Rewrite($"1px solid {surface}").Should().Be($"1px solid var(--eq-color-surface, {surface})");
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
