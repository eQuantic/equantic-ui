using eQuantic.UI.Native.Components;
using eQuantic.UI.Native.Engine;
using eQuantic.UI.Primitives;
using FluentAssertions;
using Xunit;

namespace eQuantic.UI.Native.Engine.Tests;

/// <summary>
/// A text field and a spreadsheet that run past the scroll view showing them take no press outside
/// it (#635). They registered their whole bounds, which place the caret and the cells, and took the
/// presses aimed at what stands below the view, where they are not drawn, as a code surface did until
/// #297. Each now carries the part on screen, and only that part takes a press.
/// </summary>
public class SurfacesPastTheirViewTests
{
    /// <summary>
    /// A scroll view 100 tall over a box that takes no press, with <paramref name="surface"/> 90 down
    /// its content: whatever of it is past 100 runs under the box.
    /// </summary>
    private static PhotonHost Mount(VisualNode surface)
    {
        var content = new Column(gap: 0) { Width = SizeValue.Fill };
        content.Add(new Box(new BoxStyle { Width = SizeValue.Fill, Height = 90 }));
        content.Add(surface);
        var page = new Column(gap: 0) { Width = SizeValue.Fill };
        page.Add(new ScrollView(content) { Width = SizeValue.Fill, Height = SizeValue.Fixed(100) });
        page.Add(new Box(new BoxStyle { Width = SizeValue.Fill, Height = 200 }, new Text("below", TypeRole.BodyM)));
        var host = new PhotonHost(page, PhotonTheme.Instance, ThemeMode.Light, 400, 400);
        host.RenderFrame(new DisplayListBuilder());
        return host;
    }

    [Fact]
    public void AFieldPastItsView_TakesNoPressOnWhatIsBelowIt()
    {
        var host = Mount(new TextEntry("past the view", _ => { }));
        var field = host.LastFrame!.TextRegions.Single();
        field.Bounds.Bottom.Should().BeGreaterThan(105, "the field runs past the view, under the box below");

        host.PressDown(field.Bounds.X + 10, 104);
        host.PressUp(field.Bounds.X + 10, 104);

        host.FocusedPath.Should().BeNull("the press was on the box below the view, where the field is not drawn");
    }

    [Fact]
    public void ASheetPastItsView_TakesNoPressOnWhatIsBelowIt()
    {
        var host = Mount(new SheetSurface(new Box(new BoxStyle { Width = SizeValue.Fill, Height = 300 }),
            new SheetController(rows: 20, cols: 4)));
        var sheet = host.LastFrame!.SheetRegions.Single();
        sheet.Bounds.Bottom.Should().BeGreaterThan(150, "the sheet runs past the view, under the box below");

        host.PressDown(sheet.Bounds.X + 10, 150);
        host.PressUp(sheet.Bounds.X + 10, 150);

        host.FocusedPath.Should().BeNull("the press was on the box below the view, where the sheet is not drawn");
    }

    [Fact]
    public void TheirPartInTheView_StillTakesAPress()
    {
        var host = Mount(new TextEntry("in the view", _ => { }));
        var field = host.LastFrame!.TextRegions.Single();

        host.PressDown(field.Bounds.X + 10, 95);
        host.PressUp(field.Bounds.X + 10, 95);

        host.FocusedPath.Should().Be(field.Path, "the field is drawn there, inside the view");
    }
}
