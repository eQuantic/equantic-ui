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
            // The 40-point box, the slot's 23 on each side and the panel's border of 1.
            Photon.With(menu: new MenuTheme { PopoverPadding = EdgeInsets.All(23) }), 88, 88),
        new Case("Notice", () => new Toast(Long),
            Photon.With(notice: new NoticeTheme { ToastDot = 9 }), 9, 9),
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

        var width = c.Width ?? float.NaN;
        var height = c.Height ?? float.NaN;
        slotted.Should().Contain(r => Fits(r, width, height), $"the {c.Family} slot names a box of {c.Width}x{c.Height}");
        plain.Should().NotContain(r => Fits(r, width, height), $"no {c.Family} box is {c.Width}x{c.Height} under Photon");
    }

    private static bool Near(float a, float b) => MathF.Abs(a - b) < 0.5f;

    /// <summary>A box of the size a case names, a side it leaves out (NaN) matching any.</summary>
    private static bool Fits(Rect r, float width, float height) =>
        (float.IsNaN(width) || Near(r.Width, width)) && (float.IsNaN(height) || Near(r.Height, height));

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
