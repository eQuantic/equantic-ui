using eQuantic.UI.Components;
using eQuantic.UI.Primitives;
using eQuantic.UI.Sheet;

namespace eQuantic.UI.Native.Engine.Tests;

/// <summary>
/// A sheet's viewport for the engine's tests, without a component: a <see cref="TableScroll"/> laid out
/// over the document's axes behind pinned headers, at an offset, and the geometry the engine reads it
/// through. A window 900 points wide holds eight default columns with no horizontal bar, so its height
/// is the height the cells and the header row share; a narrower one scrolls across too.
/// </summary>
internal static class SheetScrolls
{
    public const float HeaderWidth = 44f;
    public const float HeaderHeight = 26f;

    public static TableScroll Place(SheetController sheet, double across = 0, double down = 0,
        float width = 900, float height = 586)
    {
        var scroll = new TableScroll();
        var document = sheet.Document;
        scroll.Arrange(new Size(width, height), HeaderWidth + document.ColumnSpans.Total,
            HeaderHeight + document.RowSpans.Total, HeaderWidth, HeaderHeight, reduceMotion: true);
        if (across != 0 || down != 0)
            scroll.Apply(new TableScrollRequest(1) { Offset = new TableOffset(across, down) },
                document.RowSpans, document.ColumnSpans, 0, 0);
        sheet.Grid = new SheetGrid(scroll, HeaderWidth, HeaderHeight);
        return scroll;
    }
}
