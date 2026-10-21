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
/// Goldens for the table viewport's paint: the pinned row and column over the cells that scroll under
/// them, a mark drawn in each band it reaches, and both scrollbars in their strips, in each mode. Each
/// image ships with an assertion beside it, because a golden proves geometry and not state.
/// </summary>
public class TableViewportGoldenTests
{
    private static PhotonHost Open(ThemeMode mode)
    {
        var rows = new UniformTableSpans(200, 28);
        var columns = new UniformTableSpans(30, 96);
        var outline = new TableViewCell(0, 1, new Box(new BoxStyle
        {
            BorderWidth = 2,
            BorderColor = PhotonTheme.Instance.FocusRing,
            Width = SizeValue.Fill,
            Height = SizeValue.Fill,
        }), mark: true)
        {
            Rows = 5,
            Columns = 2,
        };
        var table = new TableView(rows, columns, (row, column) =>
        {
            var pinned = row == 0 || column == 0;
            var label = row == 0 ? Spreadsheet.ColumnName(column) : column == 0 ? $"{row + 1}" : $"{row},{column}";
            return new Box(new BoxStyle
            {
                Width = SizeValue.Fill,
                Height = SizeValue.Fill,
                Background = pinned ? PhotonTheme.Instance.SurfaceSubtle : PhotonTheme.Instance.Surface,
                BorderColor = PhotonTheme.Instance.Border,
                BorderWidth = 0.5f,
                Padding = new EdgeInsets(0, 6, 0, 6),
            }, new Text(label, TypeRole.Caption, maxLines: 1));
        })
        {
            PinnedRows = 1,
            PinnedColumns = 1,
            Marks = [outline],
            Request = new TableScrollRequest(1) { Offset = new TableOffset(40, 42) },
        };
        var host = new PhotonHost(table, PhotonTheme.Instance, mode, 460, 260)
        {
            SmoothScroll = false,
            ReducedMotion = true,
        };
        for (var t = 0; t < 4; t++) host.RenderFrame(new DisplayListBuilder(), t * 16);
        return host;
    }

    private static void Render(PhotonHost host, string golden)
    {
        using var backend = new ReferenceBackend();
        using var surface = backend.CreateSurface((int)host.Width, (int)host.Height);
        var builder = new DisplayListBuilder();
        host.RenderFrame(builder, 100);
        backend.Render(builder.Build(), surface);
        GoldenImage.Match(surface, golden);
    }

    [Theory]
    [InlineData(ThemeMode.Light, "table-viewport-light")]
    [InlineData(ThemeMode.Dark, "table-viewport-dark")]
    public void TheBands_TheMarkAndTheScrollbars(ThemeMode mode, string golden)
    {
        var host = Open(mode);

        // The state the picture shows: scrolled 40 across and 42 down, both scrollbars drawn.
        var scroll = host.LastFrame!.TableRegions.Single().Viewport.Scroll;
        scroll.Offset.Should().Be(new TableOffset(40, 42));
        scroll.TrackOf(ScrollAxis.Vertical).Should().NotBeNull();
        scroll.TrackOf(ScrollAxis.Horizontal).Should().NotBeNull();

        Render(host, golden);
    }
}
