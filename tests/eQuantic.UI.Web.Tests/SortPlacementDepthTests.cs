using eQuantic.UI.Primitives;
using FluentAssertions;

namespace eQuantic.UI.Web.Tests;

/// <summary>
/// The depth a carried item takes in a list whose items have depths (#1594), the arithmetic both targets run
/// (<see cref="SortPlacement.Wanted"/>, <see cref="SortPlacement.Depths"/>, <see cref="SortPlacement.KeyDepth"/> and
/// <see cref="SortPlacement.Siblings"/>): what the pointer's travel across asks for, the depths a place between two items
/// allows and the order the guard is asked in, a cross arrow's step, and where a reader hears the item is. The browser
/// runs the transpiled twin, driven in <c>sortable.spec.ts</c>, and Photon this, in <c>SortableHostTests</c>.
/// </summary>
public class SortPlacementDepthTests
{
    [Theory]
    [InlineData(1, 0, 16, 1)]     // no travel: the item's own depth
    [InlineData(0, 16, 16, 1)]    // one indent toward the end: one deeper
    [InlineData(0, 7.9f, 16, 0)]  // under half an indent: still its own
    [InlineData(0, 8, 16, 1)]     // half an indent rounds to the nearest, up
    [InlineData(2, -24, 16, 1)]   // one and a half back: a half rounds toward the end, so one back
    [InlineData(1, -40, 16, -1)]  // past the top: what is asked for, which Depths keeps in range
    [InlineData(1, 64, 0, 1)]     // a list with no indent asks for the item's own depth
    public void ThePointer_AsksForOneDepthPerIndentItTravelledAcross(int depth, float travelled, float indent, int expected) =>
        SortPlacement.Wanted(depth, travelled, indent).Should().Be(expected);

    [Fact]
    public void APlace_AllowsFromTheDepthBelowToOneDeeperThanTheItemAbove_TheWantedOneFirst()
    {
        // After the last child (depth 1) of an open folder, before the folder's next sibling (depth 0).
        SortPlacement.Depths(0, above: 1, below: 0).Should().Equal(0, 1, 2);
        SortPlacement.Depths(1, above: 1, below: 0).Should().Equal([1, 0, 2], "the wanted depth, then shallower, then deeper");
        SortPlacement.Depths(2, above: 1, below: 0).Should().Equal(2, 1, 0);
        SortPlacement.Depths(5, above: 1, below: 0).Should().Equal([2, 1, 0], "a depth past the deepest is kept at it");
        SortPlacement.Depths(-1, above: 1, below: 0).Should().Equal([0, 1, 2], "and one past the shallowest at it");
    }

    [Fact]
    public void BeforeAChild_OnlyTheChildsDepth()
    {
        // A folder (depth 0) above its first child (depth 1): the place is inside the folder, nowhere else.
        SortPlacement.Depths(0, above: 0, below: 1).Should().Equal(1);
    }

    [Fact]
    public void AtTheEnds_TheTopIsAlwaysAllowed()
    {
        SortPlacement.Depths(3, above: null, below: 0).Should().Equal([0], "before the first item, its depth only");
        SortPlacement.Depths(0, above: 2, below: null).Should().Equal([0, 1, 2, 3], "after the last, the top or as deep as one past it");
        SortPlacement.Depths(1, above: null, below: null).Should().Equal([0], "an empty list holds it at the top");
    }

    [Theory]
    [InlineData(0, 1, 1)]
    [InlineData(1, 1, 2)]
    [InlineData(2, 1, 2)]   // the deepest: no further that way
    [InlineData(1, -1, 0)]
    [InlineData(0, -1, 0)]  // the shallowest
    public void ACrossArrow_StepsOneDepth_KeptInThePlacesRange(int depth, int step, int expected) =>
        SortPlacement.KeyDepth(depth, step, above: 1, below: 0).Should().Be(expected);

    [Fact]
    public void AReader_HearsThePositionAmongTheItemsAtItsDepthUnderTheSameHolder()
    {
        // src (0), App.cs (1), Program.cs (1), docs (0), intro.md (1), README.md (0): the carried item left out.
        int[] depths = [0, 1, 1, 0, 1, 0];

        SortPlacement.Siblings(depths, 3, 1).Should().Be((0, 3, 3), "after Program.cs as src's last child: 3 of 3 in src");
        SortPlacement.Siblings(depths, 3, 0).Should().Be((-1, 2, 4), "before docs at the top: 2 of 4 in the list");
        SortPlacement.Siblings(depths, 5, 1).Should().Be((3, 2, 2), "after intro.md in docs: 2 of 2 in docs");
        SortPlacement.Siblings(depths, 1, 1).Should().Be((0, 1, 3), "before App.cs: the first of src's 3");
        SortPlacement.Siblings(depths, 6, 0).Should().Be((-1, 4, 4), "at the end, the last of the top's 4");
    }

    [Fact]
    public void AListWithoutDepths_SaysWhatItSaid()
    {
        int[] flat = [0, 0, 0];
        SortPlacement.Siblings(flat, 1, 0).Should().Be((-1, 2, 4), "the index plus one, and every item plus the carried one");
    }
}
