using eQuantic.UI.Components;
using eQuantic.UI.Native.Components;
using eQuantic.UI.Native.Engine;
using eQuantic.UI.Native.Framework;
using eQuantic.UI.Primitives;
using FluentAssertions;
using Xunit;

namespace eQuantic.UI.Native.Engine.Tests;

/// <summary>
/// Photon's half of the surface, menu, notice, progress and card slots (E6c, #1243;
/// <c>docs/ELEMENT-TREE-PLAN.md</c> §3.10): a value a theme puts in a family's slot reaches the layout of
/// a component of that family, measured by the boxes the layout placed. Under Photon's own theme no box
/// has that size.
/// </summary>
public class SurfaceSlotsTests
{
    private static readonly PhotonTheme Photon = PhotonTheme.Instance;

    private const string Long =
        "The file was saved, and a copy went to the shared folder, where everyone on the team can open it.";

    /// <summary>One component per family, a theme whose slot names one value, and the size that value lays out.</summary>
    public sealed record Case(string Family, Func<VisualNode> Build, IAppTheme Theme, float? Width, float? Height)
    {
        public override string ToString() => Family;
    }

    public static TheoryData<Case> Cases => new()
    {
        new Case("Modal", () => new Dialog("Delete the file?", Long, [new DialogAction("Delete", () => { })]),
            Photon.With(modal: new ModalTheme { DialogMaxWidth = 312 }), 312, null),
        new Case("Menu", () => new Popover(new Text("Open"), new Box(new BoxStyle { Width = 40, Height = 40 }), open: true),
            Photon.With(menu: new MenuTheme { PopoverPadding = EdgeInsets.All(23) }), 86, 86),
        new Case("Notice", () => new Toast(Long),
            Photon.With(notice: new NoticeTheme { ToastMaxWidth = 311 }), 311, null),
        new Case("Progress", () => new ProgressBar(0.4f),
            Photon.With(progress: new ProgressTheme { BarHeight = 13 }), null, 13),
        new Case("Card", () => new Card(new Box(new BoxStyle { Width = 40, Height = 40 }), CardKind.Outlined),
            Photon.With(card: new CardTheme { BorderWidth = 9 }), null, null),
    };

    [Theory]
    [MemberData(nameof(Cases))]
    public void AValueInAFamilysSlot_ReachesPhotonsLayout(Case c)
    {
        var slotted = Boxes(c.Build(), c.Theme);
        var plain = Boxes(c.Build(), Photon);
        if (c.Width is null && c.Height is null)
        {
            slotted.Should().NotEqual(plain, $"the {c.Family} slot changes what the layout places");
            return;
        }

        bool Fits(Rect r) => (c.Width is not { } w || Near(r.Width, w)) && (c.Height is not { } h || Near(r.Height, h));
        slotted.Should().Contain(r => Fits(r), $"the {c.Family} slot names a box of {c.Width}x{c.Height}");
        plain.Should().NotContain(r => Fits(r), $"no {c.Family} box is {c.Width}x{c.Height} under Photon");
    }

    private static bool Near(float a, float b) => MathF.Abs(a - b) < 0.5f;

    private static List<Rect> Boxes(VisualNode node, IAppTheme theme)
    {
        var result = PhotonRealizer.Realize(node, 800, 800, theme, ThemeMode.Light, new DisplayListBuilder());
        var found = new List<Rect>();
        Walk(result.Root, found);
        foreach (var layer in result.OverlayRoots) Walk(layer, found);
        return found;
    }

    private static void Walk(LayoutNode node, List<Rect> found)
    {
        found.Add(node.Bounds);
        foreach (var child in node.Children) Walk(child, found);
    }
}
