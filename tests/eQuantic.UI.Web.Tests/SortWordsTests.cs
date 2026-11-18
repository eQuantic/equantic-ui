using System.Globalization;
using eQuantic.UI.Components;
using eQuantic.UI.Primitives;
using eQuantic.UI.Web;
using FluentAssertions;

namespace eQuantic.UI.Web.Tests;

/// <summary>
/// The words a sortable list says while the keyboard sorts (#1564): the holes of a step filled by the rule both targets
/// run (<see cref="SortWords.Fill"/>, tested through its transpiled twin on the same cases in <c>sortable.spec.ts</c>),
/// and the SDK's words in the reader's culture on every list <c>UI.Sortable</c> builds unless the app gives its own.
/// </summary>
public class SortWordsTests
{
    [Theory]
    [InlineData("Picked up {0}, position {1} of {2} in {3}.", new[] { "a", "1", "3", "To do" }, "Picked up a, position 1 of 3 in To do.")]
    [InlineData("{1} before {0}", new[] { "a", "b" }, "b before a")]
    [InlineData("Left out: {0}.", new[] { "a", "b", "c" }, "Left out: a.")]
    [InlineData("Past the holes: {4}.", new[] { "a" }, "Past the holes: {4}.")]
    [InlineData("A stray { brace } and {x}.", new[] { "a" }, "A stray { brace } and {x}.")]
    [InlineData("Unclosed {0", new[] { "a" }, "Unclosed {0")]
    [InlineData("{0} is {1}", new[] { "{1}.txt", "here" }, "{1}.txt is here")]
    [InlineData("{}{0}{10}", new[] { "a" }, "{}a{10}")]
    public void AStepsHolesAreFilledInOnePass_AndNothingAnAppWritesFailsIt(string text, string[] holes, string expected) =>
        SortWords.Fill(text, holes).Should().Be(expected);

    [Fact]
    public void AListTheFactoryBuilds_SaysTheSdksWordsInTheReadersCulture_UnlessTheAppGivesItsOwn()
    {
        var before = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentUICulture = new CultureInfo("pt-BR");
            var list = Components.UI.Sortable(new Column(gap: 0), "todo", _ => { });
            list.Announcements.Should().Be(SdkStrings.Sorting);
            list.Announcements!.Instructions.Should()
                .Be("Pressione Espaço para pegá-lo, as setas para movê-lo e Espaço de novo para soltá-lo.");
            list.Announcements.PutBack.Should().Be("{0} voltou para onde estava.");

            var own = new SortAnnouncements("Use Space.", "Took {0}.", "{0} now {1}/{2}.", "{0} in {1}.", "Not there.",
                "Left {0} at {1}.", "Left {0} in {1}.", "Back.");
            Components.UI.Sortable(new Column(gap: 0), "todo", _ => { }, announcements: own).Announcements.Should().BeSameAs(own);
        }
        finally
        {
            CultureInfo.CurrentUICulture = before;
        }
    }

    [Fact]
    public void ATreeThatMovesItsNodes_SaysTheSdksWords()
    {
        var tree = new TreeView([new TreeNode("a", "a") { IsLeaf = true }]) { OnMoved = _ => Task.CompletedTask };
        var list = Find(WebRealizer.Lower(tree, PhotonTheme.Instance).Render(),
            node => node.Attributes.TryGetValue("data-eq-sort-id", out var id) && id == "tree");

        list.Should().NotBeNull("a tree with OnMoved is a nesting sortable around its composite");
        list!.Attributes["data-eq-sort-say-picked-up"].Should().Be(SdkStrings.Sorting.PickedUp);
        list.Attributes["data-eq-sort-say-instructions"].Should().Be(SdkStrings.Sorting.Instructions);
    }

    private static HtmlNode? Find(HtmlNode node, Func<HtmlNode, bool> match) =>
        match(node) ? node : node.Children.Select(child => Find(child, match)).FirstOrDefault(found => found is not null);
}
