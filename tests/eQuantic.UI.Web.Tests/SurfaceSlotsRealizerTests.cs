using eQuantic.UI.Components;
using eQuantic.UI.Primitives;
using eQuantic.UI.Web;
using FluentAssertions;

namespace eQuantic.UI.Web.Tests;

/// <summary>
/// The web's half of the surface, menu, notice, progress and card slots (E6c, #1243;
/// <c>docs/ELEMENT-TREE-PLAN.md</c> §3.10): a value a theme puts in a family's slot reaches the web's
/// lowering of a component of that family, read from the atomic stylesheet the lowering writes. Under
/// Photon's own theme the same component writes none of it.
/// </summary>
public class SurfaceSlotsRealizerTests
{
    private static readonly PhotonTheme Photon = PhotonTheme.Instance;

    /// <summary>One component per family, a theme whose slot names one value, and the declaration it writes.</summary>
    public sealed record Case(string Family, Func<VisualNode> Build, IAppTheme Theme, string Declaration)
    {
        public override string ToString() => Family;
    }

    public static TheoryData<Case> Cases => new()
    {
        new Case("Modal", () => new Dialog("Delete the file?", "It cannot be brought back.", [new DialogAction("Delete", () => { })]),
            Photon.With(modal: new ModalTheme { DialogMaxWidth = 612 }), "max-width:612px"),
        new Case("Menu", () => new Popover(new Text("Open"), new Text("Inside"), open: true),
            Photon.With(menu: new MenuTheme { PopoverPadding = EdgeInsets.All(23) }), "padding:23px"),
        new Case("Notice", () => new Toast("The file was saved."),
            Photon.With(notice: new NoticeTheme { ToastMaxWidth = 611 }), "max-width:611px"),
        new Case("Progress", () => new ProgressBar(0.4f),
            Photon.With(progress: new ProgressTheme { BarHeight = 13 }), "height:13px"),
        new Case("Card", () => new Card(new Text("Hi"), CardKind.Outlined),
            Photon.With(card: new CardTheme { BorderWidth = 3 }), "3px"),
    };

    [Theory]
    [MemberData(nameof(Cases))]
    public void AValueInAFamilysSlot_ReachesTheWebsLowering(Case c)
    {
        Css(c.Build(), c.Theme).Should().Contain(c.Declaration, $"the {c.Family} slot writes {c.Declaration}");
        Css(c.Build(), Photon).Should().NotContain(c.Declaration, $"no {c.Family} box writes {c.Declaration} under Photon");
    }

    private static string Css(VisualNode node, IAppTheme theme)
    {
        var sink = new StyleSink();
        var html = WebRealizer.Lower(node, theme, 1f, sink, Density.Comfortable).Render();
        return sink.Css + html;
    }
}
