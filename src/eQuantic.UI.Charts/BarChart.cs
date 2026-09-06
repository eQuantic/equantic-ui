using eQuantic.UI.Components;
using eQuantic.UI.Primitives;

namespace eQuantic.UI.Charts;

/// <summary>
/// Bars — grouped or stacked, vertical or horizontal — written once and drawn by both realizers.
/// <para>
/// The CHROME is nodes: title, legend, tick and category labels, the tooltip and the table view are
/// <see cref="Text"/> and boxes the realizer measures and the theme inks, so they are themable,
/// accessible and localizable for free. The MARKS are a <see cref="Canvas"/>: the bars are
/// arithmetic over the plot's box (<see cref="BarChartLayout"/>), drawn with the engine's own
/// shapes. The two agree on where a category sits by construction — the category labels are a row
/// of equal <see cref="Flexible"/> cells and the bars use the same slot arithmetic — so the chart
/// fills whatever width it is given without knowing it at build time.
/// </para>
/// <para>
/// Colour comes from the theme's data palette (<see cref="IAppTheme.Data"/>): a series keeps its
/// slot whatever the legend hides, a ninth series is refused by name, and text never wears a series
/// colour — identity rides the swatch beside it. Two or more series draw a legend (a press isolates
/// one, a second press restores all); a single series needs none, the title names it. Hovering a
/// bar lifts it and shows one tooltip; every value is also in the table view, so the tooltip
/// enhances and never gates.
/// </para>
/// </summary>
public sealed class BarChart : StatefulComponent
{
    /// <summary>The narrowest band a vertical chart reserves for its value labels; a longer label
    /// widens it (<see cref="ValueBandWidth"/>).</summary>
    public const float MinValueAxisWidth = 48f;

    /// <summary>What one character of a Caption label may take, in dp. Tabular figures are uniform,
    /// and this is the widest a 12sp digit or separator gets on either target — so the band is
    /// decided from the label TEXT with the same arithmetic in both compilations, no measurement,
    /// and the plot beside it starts at the same x on the server, in the browser and on Photon.</summary>
    private const float CaptionCharWidth = 7f;

    /// <summary>
    /// The band a horizontal chart reserves for its category labels. FIXED, where the value band
    /// grows: tick labels are tabular figures, so a per-character estimate is tight and uniform,
    /// while a category is arbitrary text where the same estimate is wrong in both directions —
    /// it would steal plot width from "Accessibility" and still not cover "MMM". A name longer
    /// than the band ellipsises, which is visible, and the table view has the full text.
    /// </summary>
    public const float CategoryAxisWidth = 96f;
    public const float DefaultPlotHeight = 240f;

    private IReadOnlyList<ChartSeries> _series;
    private CategoryAxis _categories;
    private ValueAxis _values;
    private BarLayout _layout;
    private ChartOrientation _orientation;
    private string? _title;
    private string? _subtitle;
    private float _plotHeight;

    // State: what the legend hides, what the pointer is over, whether the table view is up.
    private List<bool> _hidden;
    private int _hover = -1;
    private float _pointerX;
    private float _pointerY;
    private bool _table;
    // What the last frame drew, and the theme it drew with — the pointer is answered against this
    // geometry, and the draw callback runs after Build on the web (a filling canvas is measured first).
    private BarChartGeometry? _geometry;
    private IAppTheme? _theme;

    public BarChart(IReadOnlyList<ChartSeries> series, CategoryAxis categories, ValueAxis? values = null,
        BarLayout layout = BarLayout.Grouped, ChartOrientation orientation = ChartOrientation.Vertical,
        string? title = null, string? subtitle = null, float plotHeight = DefaultPlotHeight)
    {
        _series = series;
        _categories = categories;
        _values = values ?? new ValueAxis();
        _layout = layout;
        _orientation = orientation;
        _title = title;
        _subtitle = subtitle;
        _plotHeight = plotHeight;
        _hidden = Hidden(series.Count);
    }

    /// <summary>The retained instance renders, and learns fresh arguments only here: configuration
    /// is copied, state (what the legend hides, the table view) stays.</summary>
    public override void AdoptConfig(UiComponent next)
    {
        if (next is not BarChart fresh) return;
        // Read BEFORE the copy, because the copy is what erases the old shape: an index only MEANS
        // a bar while the plot has the same one. New series or new categories renumber every bar,
        // and a remembered index would then name a different bar or none at all.
        var reshaped = fresh._series.Count != _series.Count
            || fresh._categories.Categories.Count != _categories.Categories.Count;
        _series = fresh._series;
        _categories = fresh._categories;
        _values = fresh._values;
        _layout = fresh._layout;
        _orientation = fresh._orientation;
        _title = fresh._title;
        _subtitle = fresh._subtitle;
        _plotHeight = fresh._plotHeight;
        if (_hidden.Count != _series.Count) _hidden = Hidden(_series.Count);
        // What the pointer is over is STATE, and the pointer has not moved — so it survives, like
        // the hidden series and the table view. Clearing it unconditionally here cost the tooltip
        // everywhere it matters: the hover's own SetState rebuilds the page, the page hands this
        // instance a fresh configuration, and the hover died before Build could read it. It only
        // ever "worked" with the chart as a bare root, which nothing rebuilds.
        if (reshaped) _hover = -1;
    }

    public override VisualNode Build(ComponentContext context)
    {
        var theme = context.Theme;
        _theme = theme;
        var root = new Column(gap: Space.S2) { Width = SizeValue.Fill };
        if (_title != null) root.Add(new Text(_title, TypeRole.Title, theme.TextPrimary, maxLines: 1));
        if (_subtitle != null) root.Add(new Text(_subtitle, TypeRole.Caption, theme.TextMuted, maxLines: 2));
        if (_series.Count > 1) root.Add(Legend(theme));
        root.Add(_table ? Table(_series, _categories, _values, theme) : Plot(context));
        root.Add(Footer());
        return root;
    }

    /// <summary>
    /// The WCAG twin of the chart: the same series as a table, so every value is reachable without a
    /// pointer. Public so an app can place it itself; the chart also offers it behind its footer.
    /// </summary>
    public static DataTable Table(IReadOnlyList<ChartSeries> series, CategoryAxis categories, ValueAxis values,
        IAppTheme theme)
    {
        var columns = new List<DataColumn>();
        columns.Add(new DataColumn(categories.Title ?? string.Empty, GridTrack.Flex(2)));
        foreach (var s in series) columns.Add(new DataColumn(s.Name, GridTrack.Flex(1), TextAlignment.End));
        var rows = new List<DataRow>();
        for (var c = 0; c < categories.Categories.Count; c++)
        {
            var cells = new List<VisualNode>();
            cells.Add(new Text(categories.Categories[c], TypeRole.BodyM, theme.TextPrimary, maxLines: 1));
            foreach (var s in series)
            {
                cells.Add(new Text(values.Label(s.At(c)), TypeRole.BodyM, theme.TextPrimary, maxLines: 1,
                    align: TextAlignment.End, tabular: true));
            }

            rows.Add(new DataRow(categories.Categories[c], cells));
        }

        return new DataTable(columns, rows);
    }

    private static List<bool> Hidden(int count)
    {
        var hidden = new List<bool>();
        for (var i = 0; i < count; i++) hidden.Add(false);
        return hidden;
    }

    private List<bool> Visible()
    {
        var visible = new List<bool>();
        for (var i = 0; i < _hidden.Count; i++) visible.Add(!_hidden[i]);
        return visible;
    }

    private ColorToken SeriesColor(IAppTheme theme, int index) =>
        theme.Data.SeriesColor(_series[index].SlotAt(index));

    // ---- Legend --------------------------------------------------------------------------------

    private VisualNode Legend(IAppTheme theme)
    {
        var row = new Row(gap: Space.S3, wrap: true, runGap: Space.S1);
        for (var i = 0; i < _series.Count; i++)
        {
            var index = i;
            var hidden = _hidden[i];
            var swatch = new Box(new BoxStyle
            {
                Width = SizeValue.Fixed(12),
                Height = SizeValue.Fixed(12),
                Background = hidden ? theme.Border : SeriesColor(theme, i),
                CornerRadius = new CornerRadii(2),
            });
            var entry = new Row(gap: Space.S1, cross: CrossAlign.Center);
            entry.Add(swatch);
            entry.Add(new Text(_series[i].Name, TypeRole.LabelSmall, hidden ? theme.TextMuted : theme.TextSecondary,
                maxLines: 1));
            row.Add(new Pressable(entry, onPressed: () => SetState(() => Isolate(index))) { Label = _series[i].Name });
        }

        return row;
    }

    /// <summary>A press isolates the series — only it stays visible; pressing the isolated one restores
    /// all. Survivors keep their colour: the slot follows the entity, never the rank.</summary>
    private void Isolate(int index)
    {
        var isolated = !_hidden[index];
        for (var i = 0; i < _hidden.Count; i++)
        {
            if (i != index && !_hidden[i]) isolated = false;
        }

        for (var i = 0; i < _hidden.Count; i++) _hidden[i] = !isolated && i != index;
        _hover = -1;
    }

    /// <summary>The band a vertical chart reserves for its value labels: room for the longest tick
    /// label, never narrower than <see cref="MinValueAxisWidth"/>. "$200,000" needs more than "200"
    /// — a band that ignored that clipped every currency axis to "$200,…".</summary>
    public static float ValueBandWidth(ValueAxis axis, ValueTicks ticks)
    {
        var longest = 0;
        for (var i = 0; i < ticks.Count; i++)
        {
            var length = axis.Label(ticks.At(i)).Length;
            if (length > longest) longest = length;
        }

        return Math.Max(MinValueAxisWidth, longest * CaptionCharWidth + Space.S2);
    }

    // ---- Plot ----------------------------------------------------------------------------------

    private VisualNode Plot(ComponentContext context)
    {
        var theme = context.Theme;
        var vertical = _orientation == ChartOrientation.Vertical;
        var canvas = new Canvas(Draw, SizeValue.Fill, SizeValue.Fill)
        {
            Label = _title,
            OnPointerMove = Pointer,
            OnPointerLeave = () =>
            {
                if (_hover >= 0) SetState(() => _hover = -1);
            },
        };
        var stack = new Stack { Width = SizeValue.Fill, Height = SizeValue.Fixed(_plotHeight) };
        stack.Add(canvas);
        var tooltip = Tooltip(theme);
        if (tooltip != null) stack.Add(tooltip);

        var column = new Column(gap: Space.S1) { Width = SizeValue.Fill };
        var plotRow = new Row(cross: CrossAlign.Stretch);
        var axisRow = new Row(cross: CrossAlign.Start);
        if (vertical)
        {
            var ticks = BarChartLayout.Ticks(_series, Visible(), _categories.Categories.Count, _layout, _values);
            var band = ValueBandWidth(_values, ticks);
            plotRow.Add(ValueLabelsBeside(context, ticks, band));
            plotRow.Add(new Flexible(stack));
            axisRow.Add(new Box(new BoxStyle { Width = SizeValue.Fixed(band) }));
            axisRow.Add(new Flexible(CategoryLabelsBelow(theme)));
        }
        else
        {
            plotRow.Add(CategoryLabelsBeside(context));
            plotRow.Add(new Flexible(stack));
            axisRow.Add(new Box(new BoxStyle { Width = SizeValue.Fixed(CategoryAxisWidth) }));
            axisRow.Add(new Flexible(ValueLabelsBelow(theme)));
        }

        column.Add(plotRow);
        column.Add(axisRow);
        if (_values.Title != null || _categories.Title != null) column.Add(AxisTitles(theme, vertical));
        return column;
    }

    /// <summary>Vertical chart: tick labels in a fixed band left of the plot, each centred on its
    /// gridline — the plot height is known, so the positions are exact.</summary>
    private VisualNode ValueLabelsBeside(ComponentContext context, ValueTicks ticks, float band)
    {
        var theme = context.Theme;
        var lineHeight = theme.Type(TypeRole.Caption).LineHeight * context.TypeScale;
        var stack = new Stack { Width = SizeValue.Fixed(band), Height = SizeValue.Fixed(_plotHeight) };
        for (var i = 0; i < ticks.Count; i++)
        {
            var y = _plotHeight - BarChartLayout.TickOffset(ticks, i, _plotHeight);
            var label = new Box(new BoxStyle { Width = SizeValue.Fixed(band - Space.S1) },
                new Text(_values.Label(ticks.At(i)), TypeRole.Caption, theme.TextMuted, maxLines: 1,
                    align: TextAlignment.End, tabular: true));
            stack.Add(new Positioned(label, top: y - (lineHeight / 2), start: 0));
        }

        return stack;
    }

    /// <summary>Vertical chart: one equal cell per category under the plot — the same division the
    /// bars use, so a label sits under its bars at any width.</summary>
    private VisualNode CategoryLabelsBelow(IAppTheme theme)
    {
        var row = new Row();
        foreach (var name in _categories.Categories)
        {
            row.Add(new Flexible(new Text(name, TypeRole.Caption, theme.TextMuted, maxLines: 1,
                align: TextAlignment.Center)));
        }

        return row;
    }

    /// <summary>Horizontal chart: category labels in a fixed band left of the plot, each centred on
    /// its slot.</summary>
    private VisualNode CategoryLabelsBeside(ComponentContext context)
    {
        var theme = context.Theme;
        var count = _categories.Categories.Count;
        var slot = count == 0 ? _plotHeight : _plotHeight / count;
        var lineHeight = theme.Type(TypeRole.Caption).LineHeight * context.TypeScale;
        var stack = new Stack { Width = SizeValue.Fixed(CategoryAxisWidth), Height = SizeValue.Fixed(_plotHeight) };
        for (var c = 0; c < count; c++)
        {
            var label = new Box(new BoxStyle { Width = SizeValue.Fixed(CategoryAxisWidth - Space.S2) },
                new Text(_categories.Categories[c], TypeRole.Caption, theme.TextMuted, maxLines: 1,
                    align: TextAlignment.End));
            stack.Add(new Positioned(label, top: (c * slot) + (slot / 2) - (lineHeight / 2), start: 0));
        }

        return stack;
    }

    /// <summary>Horizontal chart: tick labels spread under the plot, first and last at its edges.</summary>
    private VisualNode ValueLabelsBelow(IAppTheme theme)
    {
        var ticks = BarChartLayout.Ticks(_series, Visible(), _categories.Categories.Count, _layout, _values);
        var row = new Row(main: MainAlign.SpaceBetween);
        for (var i = 0; i < ticks.Count; i++)
        {
            row.Add(new Text(_values.Label(ticks.At(i)), TypeRole.Caption, theme.TextMuted, maxLines: 1,
                tabular: true));
        }

        return row;
    }

    private VisualNode AxisTitles(IAppTheme theme, bool vertical)
    {
        var row = new Row(main: MainAlign.SpaceBetween, cross: CrossAlign.Center);
        var start = vertical ? _values.Title : _categories.Title;
        var end = vertical ? _categories.Title : _values.Title;
        row.Add(new Text(start ?? string.Empty, TypeRole.LabelSmall, theme.TextSecondary, maxLines: 1));
        row.Add(new Text(end ?? string.Empty, TypeRole.LabelSmall, theme.TextSecondary, maxLines: 1));
        return row;
    }

    // ---- Marks ---------------------------------------------------------------------------------

    private void Draw(ICanvasPainter p)
    {
        var theme = _theme;
        if (theme == null) return;
        var vertical = _orientation == ChartOrientation.Vertical;
        var geometry = BarChartLayout.Solve(_series, Visible(), _categories.Categories.Count, _layout, _orientation,
            _values, p.Width, p.Height);
        _geometry = geometry;

        // Hairline gridlines one step off the surface, the baseline a step stronger — recessive chrome.
        for (var i = 0; i < geometry.Ticks.Count; i++)
        {
            var at = geometry.TickPosition(i);
            if (vertical) p.Line(0, at, p.Width, at, theme.Border, 1);
            else p.Line(at, 0, at, p.Height, theme.Border, 1);
        }

        if (vertical) p.Line(0, geometry.Baseline, p.Width, geometry.Baseline, theme.BorderStrong, 1);
        else p.Line(geometry.Baseline, 0, geometry.Baseline, p.Height, theme.BorderStrong, 1);

        for (var i = 0; i < geometry.Bars.Count; i++)
        {
            var b = geometry.Bars[i];
            if (b.Width <= 0 || b.Height <= 0) continue;
            var color = SeriesColor(theme, b.Series);
            if (i == _hover) color = color.WithOpacity(0.8f);
            if (!b.DataEnd)
            {
                p.FillRect(b.X, b.Y, b.Width, b.Height, color);
                continue;
            }

            // The data end is rounded; the baseline end is square: a rounded rect, then a plain one
            // over the half nearest the baseline.
            var radius = Math.Min(BarChartLayout.DataEndRadius, Math.Min(b.Width, b.Height) / 2);
            p.FillRect(b.X, b.Y, b.Width, b.Height, color, radius);
            if (vertical)
            {
                var half = b.Height / 2;
                if (b.Negative) p.FillRect(b.X, b.Y, b.Width, half, color);
                else p.FillRect(b.X, b.Y + half, b.Width, half, color);
            }
            else
            {
                var half = b.Width / 2;
                if (b.Negative) p.FillRect(b.X + half, b.Y, half, b.Height, color);
                else p.FillRect(b.X, b.Y, half, b.Height, color);
            }
        }
    }

    // ---- Pointer and tooltip -------------------------------------------------------------------

    private void Pointer(CanvasPointer pointer)
    {
        var geometry = _geometry;
        if (geometry == null) return;
        var hit = BarChartLayout.HitTest(geometry, pointer.X, pointer.Y);
        if (hit < 0 && _hover < 0) return;
        SetState(() =>
        {
            _hover = hit;
            _pointerX = pointer.X;
            _pointerY = pointer.Y;
        });
    }

    /// <summary>One tooltip beside the pointer: the value first, in the strong tier, the series
    /// keyed by its swatch. It flips to the other side of the pointer past the plot's middle so it
    /// never leaves the plot.</summary>
    private VisualNode? Tooltip(IAppTheme theme)
    {
        var geometry = _geometry;
        if (_hover < 0 || geometry == null || _hover >= geometry.Bars.Count) return null;
        var bar = geometry.Bars[_hover];
        var series = _series[bar.Series];

        var card = new Column(gap: Space.S1);
        card.Add(new Text(_categories.Categories[bar.Category], TypeRole.Caption, theme.TextMuted, maxLines: 1));
        var line = new Row(gap: Space.S2, cross: CrossAlign.Center);
        line.Add(new Box(new BoxStyle
        {
            Width = SizeValue.Fixed(8),
            Height = SizeValue.Fixed(8),
            Background = SeriesColor(theme, bar.Series),
            CornerRadius = new CornerRadii(2),
        }));
        line.Add(new Text(_values.Label(series.At(bar.Category)), TypeRole.Label, theme.TextPrimary, maxLines: 1,
            tabular: true));
        line.Add(new Text(series.Name, TypeRole.Caption, theme.TextSecondary, maxLines: 1));
        card.Add(line);

        var box = new Box(new BoxStyle
        {
            Background = theme.Surface,
            BorderWidth = 1,
            BorderColor = theme.Border,
            CornerRadius = new CornerRadii(theme.Shape(ShapeScale.Small)),
            Padding = EdgeInsets.Symmetric(Space.S2, Space.S1),
        }, card);

        var left = _pointerX < geometry.Width / 2;
        var above = _pointerY < geometry.Height / 2;
        return new Positioned(box,
            top: above ? _pointerY + Space.S3 : null,
            end: left ? null : geometry.Width - _pointerX + Space.S3,
            bottom: above ? null : geometry.Height - _pointerY + Space.S3,
            start: left ? _pointerX + Space.S3 : null);
    }

    // ---- Footer --------------------------------------------------------------------------------

    private VisualNode Footer()
    {
        var row = new Row(main: MainAlign.End);
        row.Add(new Button(_table ? SdkStrings.ShowAsChart : SdkStrings.ShowAsTable, Variant.Ghost, SizeVariant.Small,
            onPressed: () => SetState(() => _table = !_table)));
        return row;
    }
}
