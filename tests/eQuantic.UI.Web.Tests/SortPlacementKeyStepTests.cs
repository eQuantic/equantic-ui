using eQuantic.UI.Primitives;
using FluentAssertions;

namespace eQuantic.UI.Web.Tests;

/// <summary>
/// Where a key moves a carried item (#1533), the arithmetic both targets run (<see cref="SortPlacement.KeyStep"/>):
/// in a list that nests, before each other item, inside it, and after the last; in one that does not, one index at a
/// time; kept inside the list either way. The browser runs its transpiled twin, tested the same way in
/// <c>sortable.spec.ts</c>.
/// </summary>
public class SortPlacementKeyStepTests
{
    [Theory]
    // Two other items (a, b): the places are before a, inside a, before b, inside b, after b.
    [InlineData(0, -1, 1, 0, 0)]   // before a -> inside a
    [InlineData(0, 0, 1, 1, -1)]   // inside a -> before b
    [InlineData(1, -1, 1, 1, 1)]   // before b -> inside b
    [InlineData(1, 1, 1, 2, -1)]   // inside b -> after b
    [InlineData(2, -1, 1, 2, -1)]  // after b stays: the end of the list
    [InlineData(2, -1, -1, 1, 1)]  // after b -> inside b
    [InlineData(0, -1, -1, 0, -1)] // before a stays: the start of the list
    public void InAListThatNests_TheKeysStepThroughThePlacesInsideTheItems(
        int index, int into, int step, int expectedIndex, int expectedInto) =>
        SortPlacement.KeyStep(index, into, step, others: 2, nests: true).Should().Be((expectedIndex, expectedInto));

    [Theory]
    [InlineData(0, 1, 1)]
    [InlineData(2, 1, 2)]
    [InlineData(0, -1, 0)]
    [InlineData(2, -1, 1)]
    public void InAListThatDoesNotNest_AKeyMovesOneIndex(int index, int step, int expected) =>
        SortPlacement.KeyStep(index, -1, step, others: 2, nests: false).Should().Be((expected, -1));
}
