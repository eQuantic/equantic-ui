using eQuantic.UI.Primitives;

namespace eQuantic.UI.Spreadsheets;

/// <summary>
/// The colours a spreadsheet draws in: each the app theme's sheet slot names, or its default from the
/// theme's foundation colours where the slot is silent, so a theme that never thought about sheets draws
/// one and a theme that names one gridline changes that alone. A record struct, as the code editor's
/// metrics are: a set of values read once per build.
/// </summary>
internal readonly record struct SheetPalette(
    ColorToken Gridline,
    ColorToken HeaderBackground,
    ColorToken HeaderText,
    ColorToken HeaderSelectedBackground,
    ColorToken HeaderSelectedText,
    ColorToken CellBackground,
    ColorToken CellText,
    ColorToken RangeFill,
    ColorToken ActiveOutline,
    ColorToken FillPreview,
    ColorToken FillHandle,
    ColorToken EntrySelection,
    ColorToken RangeOutline,
    ColorToken HeaderWholeBackground,
    ColorToken HeaderWholeText,
    ColorToken DimmedRangeFill,
    ColorToken DimmedOutline)
{
    public static SheetPalette Of(IAppTheme theme)
    {
        var slot = theme.Sheet;
        return new SheetPalette(
            slot?.Gridline ?? theme.Border,
            slot?.HeaderBackground ?? theme.SurfaceSubtle,
            slot?.HeaderText ?? theme.TextMuted,
            slot?.HeaderSelectedBackground ?? theme.SurfaceHighlight,
            slot?.HeaderSelectedText ?? theme.TextPrimary,
            slot?.CellBackground ?? theme.Surface,
            slot?.CellText ?? theme.TextPrimary,
            // Drawn ABOVE the cells, so the default lets their values show through.
            slot?.RangeFill ?? theme.SurfaceHighlight.WithOpacity(0.5f),
            slot?.ActiveOutline ?? theme.FocusRing,
            slot?.FillPreview ?? theme.FocusRing,
            slot?.FillHandle ?? theme.FocusRing,
            slot?.EntrySelection ?? theme.Selection,
            slot?.RangeOutline ?? theme.FocusRing,
            slot?.HeaderWholeBackground ?? theme.FocusRing,
            slot?.HeaderWholeText ?? theme.TextInverse,
            slot?.DimmedRangeFill ?? theme.SurfaceHighlight.WithOpacity(0.3f),
            slot?.DimmedOutline ?? theme.BorderStrong);
    }
}
