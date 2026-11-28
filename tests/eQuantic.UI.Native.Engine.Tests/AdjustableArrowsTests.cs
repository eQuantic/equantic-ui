using eQuantic.UI.Components;
using eQuantic.UI.Native.Components;
using eQuantic.UI.Primitives;
using eQuantic.UI.Web;
using FluentAssertions;
using Xunit;

namespace eQuantic.UI.Native.Engine.Tests;

/// <summary>
/// The vertical arrows step an <see cref="Adjustable"/> by its role, the same on both targets (#1873): down is
/// the next item of a radio group and of a tab strip, as reading order goes, and up raises a slider's value.
/// Photon stepped up as +1 for every role, so a vertical radio group or tab strip went backwards there, where
/// the web went forwards. Both targets read <see cref="AdjustableKeys"/> now, and this presses the keys on each.
/// </summary>
public class AdjustableArrowsTests
{
    /// <summary>Each case: what it builds with a callback that records the index or value chosen, and its name.</summary>
    public static TheoryData<string> Cases => new() { "radio group", "tabs" };

    private static VisualNode Build(string name, List<int> chosen) => name switch
    {
        "radio group" => new RadioGroup(["Standard", "Express", "Overnight"], 1, chosen.Add, "Shipping"),
        "tabs" => new Tabs(["Overview", "Activity", "Settings"], 1, chosen.Add),
        _ => throw new ArgumentOutOfRangeException(nameof(name)),
    };

    /// <summary>What the arrow does on the web: the adjustable's host answers the key as the browser hands it.</summary>
    private static void PressOnTheWeb(VisualNode node, string key)
    {
        var host = Walk(WebRealizer.Lower(node, PhotonTheme.Instance)).First(element => element.OnKeyDown is not null);
        host.OnKeyDown!(new KeyboardEventArgs { Key = key });
    }

    private static IEnumerable<HtmlElement> Walk(HtmlElement element)
    {
        yield return element;
        foreach (var child in element.Children)
            if (child is HtmlElement inner)
                foreach (var found in Walk(inner))
                    yield return found;
    }

    /// <summary>What the arrow does on Photon: Tab reaches the adjustable's one stop, and the key goes to the host.</summary>
    private static void PressOnPhoton(VisualNode node, string key)
    {
        var host = new PhotonHost(node, PhotonTheme.Instance, ThemeMode.Light, 480, 320);
        host.RenderFrame(new DisplayListBuilder());
        host.KeyDown("Tab").Should().BeTrue("the group is one stop Tab reaches");
        host.RenderFrame(new DisplayListBuilder());
        host.KeyDown(key).Should().BeTrue("the group under focus answers the arrows");
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void ArrowDown_IsTheNextItem_AndArrowUpThePrevious_OnBothTargets(string name)
    {
        foreach (var (key, expected) in new[] { ("ArrowDown", 2), ("ArrowUp", 0), ("ArrowRight", 2), ("ArrowLeft", 0) })
        {
            var onTheWeb = new List<int>();
            PressOnTheWeb(Build(name, onTheWeb), key);
            onTheWeb.Should().Equal([expected], $"{key} on the web's {name}, the second item chosen");

            var onPhoton = new List<int>();
            PressOnPhoton(Build(name, onPhoton), key);
            onPhoton.Should().Equal([expected], $"{key} on Photon's {name}, as on the web");
        }
    }

    [Fact]
    public void ArrowUp_RaisesASlidersValue_OnBothTargets()
    {
        var onTheWeb = new List<float>();
        PressOnTheWeb(new Slider(0.5f, onTheWeb.Add), "ArrowUp");
        var onPhoton = new List<float>();
        PressOnPhoton(new Slider(0.5f, onPhoton.Add), "ArrowUp");

        onTheWeb.Should().ContainSingle().Which.Should().BeGreaterThan(0.5f, "up raises a slider's value on the web");
        onPhoton.Should().ContainSingle().Which.Should().BeGreaterThan(0.5f, "and on Photon");
    }

    [Fact]
    public void TheTable_StepsByRole_AndLetsEveryOtherKeyThrough()
    {
        foreach (var role in new[] { AdjustableRole.Tablist, AdjustableRole.Radiogroup })
            (AdjustableKeys.Step("ArrowDown", role, TextDirection.LeftToRight), AdjustableKeys.Step("ArrowUp", role, TextDirection.LeftToRight)).Should().Be((1, -1), $"{role}: down is next");
        foreach (var role in new[] { AdjustableRole.Slider, AdjustableRole.Separator })
            (AdjustableKeys.Step("ArrowDown", role, TextDirection.LeftToRight), AdjustableKeys.Step("ArrowUp", role, TextDirection.LeftToRight)).Should().Be((-1, 1), $"{role}: up raises the value");
        AdjustableKeys.Step("Tab", AdjustableRole.Radiogroup, TextDirection.LeftToRight).Should().BeNull("Tab leaves the group");
        AdjustableKeys.Step("Home", AdjustableRole.Slider, TextDirection.LeftToRight).Should().BeNull();
    }
}
