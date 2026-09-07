using eQuantic.UI.Compiler.Services;
using FluentAssertions;
using Xunit;

namespace eQuantic.UI.Compiler.Tests.Services;

/// <summary>
/// The client's string catalogue is FLAT, so it is built by folding a culture's ancestors into it.
/// Which ancestors is the whole question, and folding only the neutral one was the same answer for
/// every culture this repository happens to ship.
/// <para>
/// `es` and `pt-BR` both have the neutral catalogue as their parent, so neutral-fold and chain-fold
/// agree — and those are exactly the variants our own resources and our own sample declare. A
/// consumer with `Strings.pt.resx` beside `Strings.pt-BR.resx` got the other answer: the server
/// rendered Portuguese (ResourceManager walks pt-BR → pt → neutral) while the client drew English,
/// because the key lived in the parent the fold skipped.
/// </para>
/// </summary>
public class ResourceFallbackChainTests
{
    [Theory]
    // A regional culture falls through its parent BEFORE the neutral catalogue.
    [InlineData("pt-BR", "pt,")]
    [InlineData("en-US", "en,")]
    [InlineData("es-419", "es,")]
    // A neutral culture has only the neutral catalogue behind it.
    [InlineData("pt", "")]
    [InlineData("es", "")]
    public void TheChainIsNearestFirst_AndEndsAtTheNeutralCatalogue(string culture, string expected)
    {
        // The empty entry IS the neutral catalogue, which is why the expectations end in a comma.
        string.Join(",", ResxFiles.FallbackChain(culture)).Should().Be(expected);
    }

    /// <summary>Scripts stack: zh-Hans-CN walks its script before its language.</summary>
    [Fact]
    public void AScriptedCulture_WalksEveryStepItHas()
    {
        ResxFiles.FallbackChain("zh-Hans-CN").Should().Equal("zh-Hans", "zh", "");
    }

    /// <summary>The neutral catalogue is the bottom of every chain, and has none of its own.</summary>
    [Fact]
    public void TheNeutralCatalogue_FallsBackToNothing()
    {
        ResxFiles.FallbackChain("").Should().BeEmpty();
    }

    /// <summary>
    /// A resx variant may be named anything — the file name is the culture, and nothing checks it
    /// against .NET first. One .NET cannot place still gets the neutral catalogue, rather than
    /// throwing out of a build that was only assembling strings.
    /// </summary>
    [Fact]
    public void ACultureDotNetDoesNotKnow_StillReachesTheNeutralCatalogue()
    {
        ResxFiles.FallbackChain("zz-Nowhere-QQ").Should().Equal("");
    }

    // ---- The fold ---------------------------------------------------------------------------------

    private static SortedDictionary<string, string> Catalog(params (string Key, string Value)[] entries)
    {
        var result = new SortedDictionary<string, string>(StringComparer.Ordinal);
        foreach (var (key, value) in entries) result[key] = value;
        return result;
    }

    /// <summary>The reported shape: a key translated once in the parent and not repeated in the child.</summary>
    [Fact]
    public void AChildInheritsItsParentsTranslation_RatherThanTheNeutralOne()
    {
        var neutral = Catalog(("Strings/BackToHome", "Back to home"), ("Strings/Search", "Search"));
        var cultures = new SortedDictionary<string, SortedDictionary<string, string>>(StringComparer.OrdinalIgnoreCase)
        {
            ["pt"] = Catalog(("Strings/BackToHome", "Voltar ao início"), ("Strings/Search", "Pesquisar")),
            ["pt-BR"] = Catalog(("Strings/Search", "Buscar")),
        };

        ResxFiles.FoldFallbackChains(neutral, cultures);

        cultures["pt-BR"]["Strings/Search"].Should().Be("Buscar", "the child's own value wins");
        cultures["pt-BR"]["Strings/BackToHome"].Should().Be("Voltar ao início",
            "the parent answers what the child does not, and English is two steps away");
        cultures["pt"]["Strings/BackToHome"].Should().Be("Voltar ao início");
    }

    /// <summary>A key no translation carries still reaches every culture, which is what flat means.</summary>
    [Fact]
    public void ANeutralOnlyKey_ReachesEveryCulture()
    {
        var neutral = Catalog(("Strings/Untranslated", "Only here"));
        var cultures = new SortedDictionary<string, SortedDictionary<string, string>>(StringComparer.OrdinalIgnoreCase)
        {
            ["pt"] = Catalog(),
            ["pt-BR"] = Catalog(),
        };

        ResxFiles.FoldFallbackChains(neutral, cultures);

        cultures["pt"]["Strings/Untranslated"].Should().Be("Only here");
        cultures["pt-BR"]["Strings/Untranslated"].Should().Be("Only here");
    }

    /// <summary>
    /// A child whose parent has no catalogue of its own still reaches the neutral one — the case
    /// every culture in this repository is in today, and the reason the defect stayed invisible.
    /// </summary>
    [Fact]
    public void AChildWithNoParentCatalogue_StillReachesTheNeutralOne()
    {
        var neutral = Catalog(("Strings/Search", "Search"));
        var cultures = new SortedDictionary<string, SortedDictionary<string, string>>(StringComparer.OrdinalIgnoreCase)
        {
            ["pt-BR"] = Catalog(),
        };

        ResxFiles.FoldFallbackChains(neutral, cultures);

        cultures["pt-BR"]["Strings/Search"].Should().Be("Search");
    }

    /// <summary>
    /// Each culture walks its WHOLE chain, so the answer cannot depend on which one was folded
    /// first. Asserted by folding a dictionary whose child sorts BEFORE its parent.
    /// </summary>
    [Fact]
    public void TheAnswerDoesNotDependOnTheOrderCulturesAreVisited()
    {
        var neutral = Catalog(("Strings/A", "neutral"), ("Strings/B", "neutral"));
        var cultures = new SortedDictionary<string, SortedDictionary<string, string>>(StringComparer.OrdinalIgnoreCase)
        {
            // "pt-BR" sorts before "pt" here, so the child is folded first.
            ["pt-BR"] = Catalog(("Strings/A", "child")),
            ["pt"] = Catalog(("Strings/B", "parent")),
        };

        ResxFiles.FoldFallbackChains(neutral, cultures);

        cultures["pt-BR"]["Strings/A"].Should().Be("child");
        cultures["pt-BR"]["Strings/B"].Should().Be("parent", "reached through pt even though pt came later");
    }
}
