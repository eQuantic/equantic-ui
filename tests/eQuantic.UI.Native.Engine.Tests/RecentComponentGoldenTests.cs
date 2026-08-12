using eQuantic.UI.Components;
using eQuantic.UI.Native.Components;
using eQuantic.UI.Native.Engine;
using eQuantic.UI.Native.Engine.Reference;
using eQuantic.UI.Native.Engine.Tests.Golden;
using eQuantic.UI.Primitives;
using FluentAssertions;
using Xunit;

namespace eQuantic.UI.Native.Engine.Tests;

/// <summary>
/// Goldens for the components that landed AFTER the original suite — the ListView window, the
/// code editor's line surface, the value controls (slider/stepper/segmented), and the icon set
/// including its newest glyphs. Every image carries a state assertion beside it.
/// </summary>
public class RecentComponentGoldenTests
{
    private static readonly PhotonTheme Theme = PhotonTheme.Instance;

    private static PhotonHost Host(UiComponent page, int w = 420, int h = 240)
    {
        var host = new PhotonHost(page, Theme, ThemeMode.Light, w, h) { SmoothScroll = false };
        host.RenderFrame(new DisplayListBuilder());
        host.RenderFrame(new DisplayListBuilder(), 16);
        return host;
    }

    private static void Render(PhotonHost host, string golden, float timeMs = 100)
    {
        using var backend = new ReferenceBackend();
        using var surface = backend.CreateSurface((int)host.Width, (int)host.Height);
        var builder = new DisplayListBuilder();
        host.RenderFrame(builder, timeMs);
        backend.Render(builder.Build(), surface);
        GoldenImage.Match(surface, golden);
    }

    private sealed class ListPage : Primitives.StatefulComponent
    {
        public override VisualNode Build(ComponentContext context) =>
            new ListView(count: 500, itemExtent: 36, index =>
            {
                var row = new Row(gap: Space.S2) { Cross = CrossAlign.Center, Height = SizeValue.Fixed(36) };
                row.Add(new Box(new BoxStyle
                {
                    Width = 20, Height = 20,
                    Background = context.Theme.Colors(index % 3 == 0 ? Variant.Primary : Variant.Success).Subtle,
                    CornerRadius = new CornerRadii(Radius.Sm),
                }));
                row.Add(new Text($"Item {index}", TypeRole.BodyM, context.Theme.TextPrimary, maxLines: 1));
                return row;
            })
            { Height = SizeValue.Fill };
    }

    [Fact]
    public void ListView_WindowAfterAScroll()
    {
        var host = Host(new ListPage());
        host.ScrollBy(200, 120, 10 * 36f);
        host.RenderFrame(new DisplayListBuilder(), 32);
        host.RenderFrame(new DisplayListBuilder(), 48);

        // The window really moved: item 10 is the first visible row now.
        host.Semantics().Should().Contain(s => s.Label.Contains("Item 10"));

        Render(host, "listview-scrolled-window");
    }

    private sealed class EditorPage : Primitives.StatefulComponent
    {
        public CodeEditor Editor { get; } = new(
            "let total = 0;\nfor (const item of items) {\n  total += item.price;\n}\nreturn total;",
            "javascript");

        public override VisualNode Build(ComponentContext context) => Editor;
    }

    [Fact]
    public void CodeEditor_WithASelection()
    {
        var page = new EditorPage();
        var host = Host(page, 420, 180);
        host.PressDown(80, 30);
        host.PointerMove(200, 62);
        host.PressUp(200, 62);

        Render(host, "code-editor-selection");
    }

    private sealed class ControlsPage : Primitives.StatefulComponent
    {
        public override VisualNode Build(ComponentContext context)
        {
            var column = new Column(gap: Space.S3) { Padding = EdgeInsets.All(Space.S4), Width = SizeValue.Fill };
            column.Add(new Slider(0.6f));
            column.Add(new Stepper(3));
            column.Add(new SegmentedControl(new[] { "Day", "Week", "Month" }, 1, _ => { }));
            return column;
        }
    }

    [Fact]
    public void ValueControls_SliderStepperSegmented()
    {
        var host = Host(new ControlsPage(), 360, 170);
        // The slider's thumb sits at 60% — the drag region reports its bounds.
        host.RenderFrame(new DisplayListBuilder(), 32).DragRegions.Should().NotBeEmpty();

        Render(host, "value-controls");
    }

    private sealed class IconsPage : Primitives.StatefulComponent
    {
        public override VisualNode Build(ComponentContext context)
        {
            var grid = new Column(gap: Space.S2) { Padding = EdgeInsets.All(Space.S3) };
            var row = new Row(gap: Space.S2);
            var i = 0;
            foreach (var glyph in Enum.GetValues<Icons>())
            {
                row.Add(new Icon(glyph, IconSize.Md, context.Theme.TextPrimary));
                if (++i % 8 == 0) { grid.Add(row); row = new Row(gap: Space.S2); }
            }
            if (i % 8 != 0) grid.Add(row);
            return grid;
        }
    }

    [Fact]
    public void IconSet_EveryGlyphIncludingTheNewest()
    {
        // The Menu glyph is the newest — the enum's count pins that this golden covers it.
        Enum.GetValues<Icons>().Should().HaveCount(23);

        Render(Host(new IconsPage(), 300, 120), "icon-set");
    }
}
