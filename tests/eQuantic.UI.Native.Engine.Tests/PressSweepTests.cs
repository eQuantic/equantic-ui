using eQuantic.UI.Components;
using eQuantic.UI.Native.Components;
using eQuantic.UI.Native.Engine;
using eQuantic.UI.Primitives;
using FluentAssertions;
using Xunit.Abstractions;

namespace eQuantic.UI.Native.Engine.Tests;

/// <summary>
/// Does a press REACH every component that offers one? A component that lights up but never fires
/// is worse than one that is visibly disabled, and it is invisible to a golden — the pixels are
/// right. This presses each one at the centre of the region the host would route a finger to.
/// </summary>
public class PressSweepTests(ITestOutputHelper output)
{
    private sealed class Sweep : Primitives.StatefulComponent
    {
        public readonly List<string> Fired = [];
        public readonly List<string> Offered = [];

        public override VisualNode Build(ComponentContext context)
        {
            var column = new Column(gap: Space.S2) { Width = SizeValue.Fill };

            void Row(string name, VisualNode node)
            {
                Offered.Add(name);
                column.Add(node);
            }

            Row("Button", new Button("Save", onPressed: () => Fired.Add("Button")));
            Row("IconButton", new IconButton(Icons.Search, "Search", onPressed: () => Fired.Add("IconButton")));
            Row("Checkbox", new Checkbox(false, () => Fired.Add("Checkbox"), "Remember me"));
            Row("Switch", new Switch(false, () => Fired.Add("Switch")));
            Row("Chip", new Chip("Income", selected: false, onPressed: () => Fired.Add("Chip")));
            Row("ListItem", new ListItem("Marcos", onPressed: () => Fired.Add("ListItem")));
            Row("SegmentedControl", new SegmentedControl(["All", "Income"], 0, _ => Fired.Add("SegmentedControl")));
            Row("Stepper", new Stepper(2, _ => Fired.Add("Stepper")));
            Row("Slider", new Slider(0.5f, _ => Fired.Add("Slider")));
            Row("Tabs", new Tabs(["One", "Two"], 0, _ => Fired.Add("Tabs")));
            Row("RadioGroup", new RadioGroup(["A", "B"], 0, _ => Fired.Add("RadioGroup")));
            // (pageCount, currentPage) — in that order, which is worth getting right in a test
            // whose whole job is to notice a component that never fires.
            Row("Pagination", new Pagination(5, 1, _ => Fired.Add("Pagination")));
            Row("BottomNavigation", new BottomNavigation(
                [new NavItem(Icons.CheckCircle, "Home"), new NavItem(Icons.Mail, "Cards"),
                 new NavItem(Icons.Person, "Profile")],
                0, _ => Fired.Add("BottomNavigation")));

            return column;
        }
    }

    [Fact]
    public void EveryComponentThatOffersAPress_FiresOne()
    {
        var page = new Sweep();
        var host = new PhotonHost(page, PhotonTheme.Instance, ThemeMode.Light, 390, 2000);
        host.RenderFrame(new DisplayListBuilder());
        var frame = host.RenderFrame(new DisplayListBuilder(), 1000);

        // Every hit region the host would route to, pressed where a finger would land.
        foreach (var region in frame.HitRegions.ToArray())
        {
            var centre = region.Bounds.Center;
            host.PressDown(centre.X, centre.Y);
            host.PressUp(centre.X, centre.Y);
            host.RenderFrame(new DisplayListBuilder(), 1100);
        }

        var silent = page.Offered.Except(page.Fired).ToArray();
        foreach (var name in silent) output.WriteLine($"SILENT: {name}");
        output.WriteLine($"regions: {frame.HitRegions.Count}, fired: {string.Join(", ", page.Fired.Distinct())}");

        silent.Should().BeEmpty("a component that offers a press has to receive one");
    }
}
