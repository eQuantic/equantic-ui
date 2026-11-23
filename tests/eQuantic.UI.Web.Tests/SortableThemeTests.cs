using eQuantic.UI.Web;
using eQuantic.UI.Primitives;
using FluentAssertions;

namespace eQuantic.UI.Web.Tests;

/// <summary>
/// The look of a carried item (#1602): <see cref="SortableTheme"/>, the theme's with a list's own laid over it, and
/// <see cref="SortPlacement"/>'s defaults where neither says, written once in the arithmetic both targets run; the
/// list's own look crossing on its element for the browser's controller, which lays it over the browser's theme. The
/// browser's half is <c>sortable.spec.ts</c>, Photon's <c>SortableHostTests</c>.
/// </summary>
public class SortableThemeTests
{
    private static HtmlNode Render(Sortable node) => WebRealizer.Lower(node, PhotonTheme.Instance).Render();

    private static Sortable List(SortableTheme? style)
    {
        var column = new Column(gap: 0);
        column.Add(new SortableItem(new Text("a"), "a"));
        return new Sortable(column, "todo", _ => { }, style: style);
    }

    [Fact]
    public void TheLook_TakesTheDefaults_WhereNeitherTheThemeNorTheListSays()
    {
        SortPlacement.Look(null, null).Should().Be(new SortableTheme
        {
            LiftedOpacity = 0.4f,
            CopyOpacity = 0.92f,
            CopyElevation = 4,
            MaxLean = 12,
        });
    }

    [Fact]
    public void TheLook_IsTheThemes_WithTheListsOwnLaidOverIt_MemberByMember()
    {
        var theme = new SortableTheme { LiftedOpacity = 0.25f, CopyElevation = 5, MaxLean = 6 };
        var own = new SortableTheme { MaxLean = 0, CopyOpacity = 0.5f };

        SortPlacement.Look(theme, own).Should().Be(new SortableTheme
        {
            LiftedOpacity = 0.25f,
            CopyOpacity = 0.5f,
            CopyElevation = 5,
            MaxLean = 0,
        }, "a member the list sets wins, one it leaves is the theme's, and one neither sets is the default");
    }

    [Fact]
    public void TheLook_KeepsEachOpacityBetweenNoneAndAll()
    {
        var look = SortPlacement.Look(new SortableTheme { LiftedOpacity = -0.2f }, new SortableTheme { CopyOpacity = 1.5f });

        look.LiftedOpacity.Should().Be(0, "the browser clamps an opacity by itself, and Photon's layer would not");
        look.CopyOpacity.Should().Be(1);
    }

    [Fact]
    public void TheCopy_LeansNoFurtherThanItsMost_AndStaysUprightAtZero()
    {
        SortPlacement.Tilt(-40, 10, SortPlacement.TiltMax).Should().Be(12);
        SortPlacement.Tilt(-40, 10, 6).Should().Be(6);
        SortPlacement.Tilt(40, 10, 6).Should().Be(-6);
        SortPlacement.Tilt(10, 10, 6).Should().Be(-6, "8 degrees a pixel a millisecond, past the most");
        SortPlacement.Tilt(40, 10, 0).Should().Be(0);
        float.IsNegative(SortPlacement.Tilt(40, 10, 0)!.Value).Should().BeFalse("an upright copy is 0, not -0");
        SortPlacement.Tilt(5, 0, 6).Should().BeNull();
    }

    [Fact]
    public void AThemeTakesALookWithWith_LaidOverTheOneItHad()
    {
        var once = PhotonTheme.Instance.With(sortable: new SortableTheme { CopyElevation = 5 });
        var twice = once.With(sortable: new SortableTheme { MaxLean = 0 });

        ((IAppTheme)PhotonTheme.Instance).Sortable.Should().BeNull("a theme sets no look unless it says");
        once.Sortable.Should().Be(new SortableTheme { CopyElevation = 5 });
        twice.Sortable.Should().Be(new SortableTheme { CopyElevation = 5, MaxLean = 0 });
    }

    [Fact]
    public void AThemesLook_CrossesInTheThemesJson()
    {
        var theme = PhotonTheme.Instance.With(sortable: new SortableTheme { LiftedOpacity = 0.25f, CopyElevation = 5 });

        ThemeBridge.Slots(theme).Should().ContainSingle(slot => slot.Name == "sortable")
            .Which.Slot.Should().Be(new SortableTheme { LiftedOpacity = 0.25f, CopyElevation = 5 });
        ThemeBridge.SlotJson(theme.Sortable!).Should().Be("{\"liftedOpacity\":0.25,\"copyElevation\":5}");
    }

    [Fact]
    public void AListsOwnLook_CrossesOnItsElement_MemberByMember_AndAListWithNoneWritesNone()
    {
        var styled = Render(List(new SortableTheme { LiftedOpacity = 0.25f, CopyOpacity = 0.5f, CopyElevation = 5, MaxLean = 0 }));

        styled.Attributes["data-eq-sort-look-lifted"].Should().Be("0.25");
        styled.Attributes["data-eq-sort-look-copy-opacity"].Should().Be("0.5");
        styled.Attributes["data-eq-sort-look-copy-elevation"].Should().Be("5");
        styled.Attributes["data-eq-sort-look-lean"].Should().Be("0");

        var partly = Render(List(new SortableTheme { MaxLean = 6 }));
        partly.Attributes.Keys.Where(key => key.StartsWith("data-eq-sort-look", StringComparison.Ordinal))
            .Should().Equal(["data-eq-sort-look-lean"], "only what the list sets crosses; the rest is the theme's");

        Render(List(null)).Attributes.Keys.Should().NotContain(key => key.StartsWith("data-eq-sort-look", StringComparison.Ordinal),
            "a list with no look of its own takes the theme's in the browser");
    }
}
