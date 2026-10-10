using eQuantic.UI.Web;
using eQuantic.UI.Primitives;
using FluentAssertions;

namespace eQuantic.UI.Web.Tests;

/// <summary>
/// A pressable that may not take the keyboard (<see cref="Pressable.CanRequestFocus"/>, Flutter's
/// <c>canRequestFocus</c>) leaves the Tab order in the server's HTML as in the client's (TS twin:
/// lowerPressable, whose spec also pins the focus move its press cancels, which only the runtime can
/// do). A row of a list is out of the Tab order by its role already, so the rule is measured here on
/// a plain button: a formatting toolbar's, pressed beside the text that keeps the keyboard.
/// </summary>
public class PressableFocusTests
{
    private static HtmlNode ButtonOf(Pressable pressable)
    {
        var root = WebRealizer.Lower(pressable, PhotonTheme.Instance)!.Render();
        return Walk(root).Single(node => node.Tag == "button");
    }

    private static IEnumerable<HtmlNode> Walk(HtmlNode node)
    {
        yield return node;
        foreach (var child in node.Children)
            foreach (var descendant in Walk(child))
                yield return descendant;
    }

    [Fact]
    public void APressableThatMayNotTakeTheKeyboard_LeavesTheTabOrder()
    {
        var bold = ButtonOf(new Pressable(new Text("Bold", TypeRole.Label), () => { }) { CanRequestFocus = false });

        bold.Attributes["tabindex"].Should().Be("-1");
    }

    [Fact]
    public void AnyOtherPressable_StaysInIt()
    {
        var bold = ButtonOf(new Pressable(new Text("Bold", TypeRole.Label), () => { }));

        bold.Attributes.Should().NotContainKey("tabindex");
    }
}
