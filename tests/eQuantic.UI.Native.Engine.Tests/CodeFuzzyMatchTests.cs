using eQuantic.UI.Code;
using FluentAssertions;

namespace eQuantic.UI.Native.Engine.Tests;

/// <summary>
/// The filter a completion list ranks by (<see cref="CodeFuzzyMatch"/>): the pattern's characters in
/// order, the first where a part of the word starts, and the best of every way to lay one over the
/// other. The scores are pinned by hand from the rule its documentation states, so a change to the
/// rule is a change to these numbers.
/// </summary>
public class CodeFuzzyMatchTests
{
    [Fact]
    public void APrefix_MatchesItsFirstCharacters()
    {
        var match = CodeFuzzyMatch.Of("col", "Column");

        match.Should().NotBeNull();
        match!.Positions.Should().Equal(0, 1, 2);
        // c on C: 1, no case, 8 at the start. o and l: 1, the case typed, 5 after the one before.
        match.Score.Should().Be(9 + 7 + 7);
    }

    [Fact]
    public void TheCaseTyped_ScoresOneMorePerCharacter()
    {
        CodeFuzzyMatch.Of("Col", "Column")!.Score.Should().Be(24);
        CodeFuzzyMatch.Of("col", "Column")!.Score.Should().Be(23);
    }

    [Fact]
    public void TheFirstCharacter_MustLandWhereAPartStarts()
    {
        CodeFuzzyMatch.Of("lum", "Column").Should().BeNull("an l inside a word starts no part of it");
        CodeFuzzyMatch.Of("bc", "BarChart")!.Positions.Should().Equal(0, 3);
        CodeFuzzyMatch.Of("ch", "BarChart")!.Positions.Should().Equal(3, 4);
        CodeFuzzyMatch.Of("xh", "XMLHttp")!.Positions.Should().Equal(0, 3);
        CodeFuzzyMatch.Of("ml", "XMLHttp").Should().BeNull("the M of XML starts nothing, the run goes on");
        CodeFuzzyMatch.Of("name", "first_name")!.Positions.Should().Equal(6, 7, 8, 9);
        CodeFuzzyMatch.Of("r", "a.r")!.Positions.Should().Equal(2);
    }

    [Fact]
    public void AnywhereLetsTheFirstCharacterLandAnywhere()
    {
        CodeFuzzyMatch.Of("lum", "Column", anywhere: true)!.Positions.Should().Equal(2, 3, 4);
    }

    [Fact]
    public void TheBestWayCounts_NotTheFirstOneFound()
    {
        // The first o after C is the second character, and taking it leaves the u eight characters
        // away. The O that starts "Output" keeps the u next to it, and scores more: one gap either
        // way, and a part's start where the other has none.
        var match = CodeFuzzyMatch.Of("cou", "ConsoleOutput");

        match!.Positions.Should().Equal(0, 7, 8);
        match.Score.Should().Be(9 + (1 + 0 + 6 - 3) + (1 + 1 + 5));
    }

    [Fact]
    public void TheCharactersMustAllBeThere_InOrder()
    {
        CodeFuzzyMatch.Of("cx", "Column").Should().BeNull();
        CodeFuzzyMatch.Of("nl", "Column").Should().BeNull();
        CodeFuzzyMatch.Of("columns", "Column").Should().BeNull();
    }

    [Fact]
    public void AnEmptyPattern_MatchesEverythingAndScoresNothing()
    {
        var match = CodeFuzzyMatch.Of("", "Column");

        match!.Score.Should().Be(0);
        match.Positions.Should().BeEmpty();
    }

    [Fact]
    public void AWordsStart_RanksAboveAPartsStart()
    {
        CodeFuzzyMatch.Of("bar", "BarChart")!.Score.Should()
            .BeGreaterThan(CodeFuzzyMatch.Of("bar", "FooBar")!.Score);
    }

    [Fact]
    public void ARunOfCharacters_RanksAboveTheSameCharactersApart()
    {
        // The L of CopyLine starts a part, which is worth what following the o would have been, and
        // the gap before it is what tells the two apart.
        CodeFuzzyMatch.Of("col", "CopyLine")!.Score.Should().Be(9 + 7 + (1 + 0 + 6 - 3));
        CodeFuzzyMatch.Of("col", "Column")!.Score.Should()
            .BeGreaterThan(CodeFuzzyMatch.Of("col", "CopyLine")!.Score);
    }

    [Fact]
    public void PartsTyped_RankAboveTheSameLettersInARun()
    {
        CodeFuzzyMatch.Of("cl", "CopyLine")!.Score.Should()
            .BeGreaterThan(CodeFuzzyMatch.Of("cl", "Column")!.Score);
    }

    [Fact]
    public void TheSearch_KeepsItsTablesBetweenCalls_AndAllocatesOnlyTheMatch()
    {
        // A list runs the search over every entry on every keystroke. The first call grows the
        // tables; the calls after it allocate the match and its positions, about 70 bytes, where two
        // tables of 3 × 19 cells were another 500 or so on every call.
        CodeFuzzyMatch.Of("cou", "ConsoleOutputWriter");
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 100; i++) CodeFuzzyMatch.Of("cou", "ConsoleOutputWriter");
        var perCall = (GC.GetAllocatedBytesForCurrentThread() - before) / 100;

        perCall.Should().BeLessThan(200);

        // What a longer search left in the tables is never read by a shorter one.
        CodeFuzzyMatch.Of("cowf", "ConsoleOutputWriterFactory");
        CodeFuzzyMatch.Of("cou", "ConsoleOutput")!.Positions.Should().Equal(0, 7, 8);
    }
}
