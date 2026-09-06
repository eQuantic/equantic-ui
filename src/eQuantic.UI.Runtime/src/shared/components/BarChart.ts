import { BarChartLayout, Box, BoxStyle, BuildContext, Button, Canvas, CanvasPointer, CategoryAxis, ChartSeries, Column, CornerRadii, DataColumn, DataRow, DataTable, EdgeInsets, Flexible, GridTrack, Positioned, Pressable, Row, SdkStrings, SizeValue, Stack, StatefulComponent, Text, UiComponent, ValueAxis, ValueTicks, VisualNode } from "../runtime-exports";

export class BarChart extends StatefulComponent {
    static minValueAxisWidth: number = 48;
    static captionCharWidth: number = 7;
    static categoryAxisWidth: number = 96;
    static defaultPlotHeight: number = 240;
    _series: ChartSeries[];
    _categories: CategoryAxis;
    _values: ValueAxis;
    _layout: string = 'grouped';
    _orientation: string = 'vertical';
    _title: any;
    _subtitle: any;
    _plotHeight: number = 0;
    _hidden: boolean[];
    _hover: number = -1;
    _pointerX: number = 0;
    _pointerY: number = 0;
    _table: boolean = false;
    _geometry: any;
    _theme: any;

    constructor(series?: any, categories?: any, values: any = null, layout: any = 'grouped', orientation: any = 'vertical', title: any = null, subtitle: any = null, plotHeight: any = BarChart.defaultPlotHeight, props?: any) {
        super();
        this._series = series;
        this._categories = categories;
        this._values = values ?? new ValueAxis();
        this._layout = layout;
        this._orientation = orientation;
        this._title = title;
        this._subtitle = subtitle;
        this._plotHeight = plotHeight;
        this._hidden = BarChart.hidden(series.length);
        if (props && typeof props === 'object') Object.assign(this, props);
    }

    build(context: BuildContext) {
        let theme = context.theme;
        this._theme = theme;
        let root = new Column(8, 'start', 'stretch', false, null, null, { width: SizeValue.fill });
        if (this._title != null) root.add(new Text(this._title, 'title', theme.textPrimary, 1));
        if (this._subtitle != null) root.add(new Text(this._subtitle, 'caption', theme.textMuted, 2));
        if (this._series.length > 1) root.add(this.legend(theme));
        root.add(this._table ? BarChart.table(this._series, this._categories, this._values, theme) : this.plot(context));
        root.add(this.footer());
        return root;
    }

    adoptConfig(next: UiComponent) {
        let fresh: any; 
        if (!((next instanceof BarChart && (fresh = next, true)))) return;
        let reshaped = fresh._series.length !== this._series.length || fresh._categories.categories.length !== this._categories.categories.length;
        this._series = fresh._series;
        this._categories = fresh._categories;
        this._values = fresh._values;
        this._layout = fresh._layout;
        this._orientation = fresh._orientation;
        this._title = fresh._title;
        this._subtitle = fresh._subtitle;
        this._plotHeight = fresh._plotHeight;
        if (this._hidden.length !== this._series.length) this._hidden = BarChart.hidden(this._series.length);
        if (reshaped) this._hover = -1;
    }

    static table(series: ChartSeries[], categories: CategoryAxis, values: ValueAxis, theme: any) {
        let columns: DataColumn[] = [];
        columns.push(new DataColumn(categories.title ?? '', GridTrack.flex(2)));
        for (const s of series) columns.push(new DataColumn(s.name, GridTrack.flex(1), 'end'));
        let rows: DataRow[] = [];
        for (let c = 0; c < categories.categories.length; c++) {
            let cells: VisualNode[] = [];
            cells.push(new Text(categories.categories[c], 'bodyM', theme.textPrimary, 1));
            for (const s of series) {
                cells.push(new Text(values.label(s.at(c)), 'bodyM', theme.textPrimary, 1, 'end', false, true));
            }
            rows.push(new DataRow(categories.categories[c], cells));
        }
        return new DataTable(columns, rows);
    }

    static hidden(count: number) {
        let hidden: boolean[] = [];
        for (let i = 0; i < count; i++) hidden.push(false);
        return hidden;
    }

    visible() {
        let visible: boolean[] = [];
        for (let i = 0; i < this._hidden.length; i++) visible.push(!this._hidden[i]);
        return visible;
    }

    seriesColor(theme: any, index: number) {
        return theme.data.seriesColor(this._series[index].slotAt(index));
    }

    legend(theme: any) {
        let row = new Row(12, 'start', 'center', true, 4);
        for (let i = 0; i < this._series.length; i++) {
            let index = i;
            let hidden = this._hidden[i];
            let swatch = new Box(new BoxStyle({ width: SizeValue.fixed(12), height: SizeValue.fixed(12), background: hidden ? theme.border : this.seriesColor(theme, i), cornerRadius: new CornerRadii(2) }));
            let entry = new Row(4, 'start', 'center');
            entry.add(swatch);
            entry.add(new Text(this._series[i].name, 'labelSmall', hidden ? theme.textMuted : theme.textSecondary, 1));
            row.add(new Pressable(entry, () => this.setState(() => this.isolate(index)), { label: this._series[i].name }));
        }
        return row;
    }

    isolate(index: number) {
        let isolated = !this._hidden[index];
        for (let i = 0; i < this._hidden.length; i++) {
            if (i !== index && !this._hidden[i]) isolated = false;
        }
        for (let i = 0; i < this._hidden.length; i++) this._hidden[i] = !isolated && i !== index;
        this._hover = -1;
    }

    static valueBandWidth(axis: ValueAxis, ticks: ValueTicks) {
        let longest = 0;
        for (let i = 0; i < ticks.count; i++) {
            let length = axis.label(ticks.at(i)).length;
            if (length > longest) longest = length;
        }
        return Math.max(BarChart.minValueAxisWidth, longest * BarChart.captionCharWidth + 8);
    }

    plot(context: any) {
        let theme = context.theme;
        let vertical = this._orientation === 'vertical';
        let canvas = new Canvas(this.draw.bind(this), SizeValue.fill, SizeValue.fill, { label: this._title, onPointerMove: this.pointer.bind(this), onPointerLeave: () => {
            if (this._hover >= 0) this.setState(() => this._hover = -1);
        } });
        let stack = new Stack('topStart', { width: SizeValue.fill, height: SizeValue.fixed(this._plotHeight) });
        stack.add(canvas);
        let tooltip = this.tooltip(theme);
        if (tooltip != null) stack.add(tooltip);
        let column = new Column(4, 'start', 'stretch', false, null, null, { width: SizeValue.fill });
        let plotRow = new Row(0, 'start', 'stretch');
        let axisRow = new Row(0, 'start', 'start');
        if (vertical) {
            let ticks = BarChartLayout.ticks(this._series, this.visible(), this._categories.categories.length, this._layout, this._values);
            let band = BarChart.valueBandWidth(this._values, ticks);
            plotRow.add(this.valueLabelsBeside(context, ticks, band));
            plotRow.add(new Flexible(stack));
            axisRow.add(new Box(new BoxStyle({ width: SizeValue.fixed(band) })));
            axisRow.add(new Flexible(this.categoryLabelsBelow(theme)));
        } else {
            plotRow.add(this.categoryLabelsBeside(context));
            plotRow.add(new Flexible(stack));
            axisRow.add(new Box(new BoxStyle({ width: SizeValue.fixed(BarChart.categoryAxisWidth) })));
            axisRow.add(new Flexible(this.valueLabelsBelow(theme)));
        }
        column.add(plotRow);
        column.add(axisRow);
        if (this._values.title != null || this._categories.title != null) column.add(this.axisTitles(theme, vertical));
        return column;
    }

    valueLabelsBeside(context: any, ticks: ValueTicks, band: number) {
        let theme = context.theme;
        let lineHeight = Math.fround(theme.type('caption').lineHeight * context.typeScale);
        let stack = new Stack('topStart', { width: SizeValue.fixed(band), height: SizeValue.fixed(this._plotHeight) });
        for (let i = 0; i < ticks.count; i++) {
            let y = Math.fround(this._plotHeight - BarChartLayout.tickOffset(ticks, i, this._plotHeight));
            let label = new Box(new BoxStyle({ width: SizeValue.fixed(band - 4) }), new Text(this._values.label(ticks.at(i)), 'caption', theme.textMuted, 1, 'end', false, true));
            stack.add(new Positioned(label, y - lineHeight / 2, null, null, 0));
        }
        return stack;
    }

    categoryLabelsBelow(theme: any) {
        let row = new Row();
        for (const name of this._categories.categories) {
            row.add(new Flexible(new Text(name, 'caption', theme.textMuted, 1, 'center')));
        }
        return row;
    }

    categoryLabelsBeside(context: any) {
        let theme = context.theme;
        let count = this._categories.categories.length;
        let slot = Math.fround(count === 0 ? this._plotHeight : this._plotHeight / count);
        let lineHeight = Math.fround(theme.type('caption').lineHeight * context.typeScale);
        let stack = new Stack('topStart', { width: SizeValue.fixed(BarChart.categoryAxisWidth), height: SizeValue.fixed(this._plotHeight) });
        for (let c = 0; c < count; c++) {
            let label = new Box(new BoxStyle({ width: SizeValue.fixed(BarChart.categoryAxisWidth - 8) }), new Text(this._categories.categories[c], 'caption', theme.textMuted, 1, 'end'));
            stack.add(new Positioned(label, c * slot + slot / 2 - lineHeight / 2, null, null, 0));
        }
        return stack;
    }

    valueLabelsBelow(theme: any) {
        let ticks = BarChartLayout.ticks(this._series, this.visible(), this._categories.categories.length, this._layout, this._values);
        let row = new Row(0, 'spaceBetween');
        for (let i = 0; i < ticks.count; i++) {
            row.add(new Text(this._values.label(ticks.at(i)), 'caption', theme.textMuted, 1, 'start', false, true));
        }
        return row;
    }

    axisTitles(theme: any, vertical: boolean) {
        let row = new Row(0, 'spaceBetween', 'center');
        let start = vertical ? this._values.title : this._categories.title;
        let end = vertical ? this._categories.title : this._values.title;
        row.add(new Text(start ?? '', 'labelSmall', theme.textSecondary, 1));
        row.add(new Text(end ?? '', 'labelSmall', theme.textSecondary, 1));
        return row;
    }

    draw(p: any) {
        let theme = this._theme;
        if (theme == null) return;
        let vertical = this._orientation === 'vertical';
        let geometry = BarChartLayout.solve(this._series, this.visible(), this._categories.categories.length, this._layout, this._orientation, this._values, p.width, p.height);
        this._geometry = geometry;
        for (let i = 0; i < geometry.ticks.count; i++) {
            let at = geometry.tickPosition(i);
            if (vertical) p.line(0, at, p.width, at, theme.border, 1); else p.line(at, 0, at, p.height, theme.border, 1);
        }
        if (vertical) p.line(0, geometry.baseline, p.width, geometry.baseline, theme.borderStrong, 1); else p.line(geometry.baseline, 0, geometry.baseline, p.height, theme.borderStrong, 1);
        for (let i = 0; i < geometry.bars.length; i++) {
            let b = geometry.bars[i];
            if (b.width <= 0 || b.height <= 0) continue;
            let color = this.seriesColor(theme, b.series);
            if (i === this._hover) color = color.withOpacity(Math.fround(0.8));
            if (!b.dataEnd) {
                p.fillRect(b.x, b.y, b.width, b.height, color);
                continue;
            }
            let radius = Math.min(4, Math.min(b.width, b.height) / 2);
            p.fillRect(b.x, b.y, b.width, b.height, color, radius);
            if (vertical) {
                let half = Math.fround(b.height / 2);
                if (b.negative) p.fillRect(b.x, b.y, b.width, half, color); else p.fillRect(b.x, b.y + half, b.width, half, color);
            } else {
                let half = Math.fround(b.width / 2);
                if (b.negative) p.fillRect(b.x + half, b.y, half, b.height, color); else p.fillRect(b.x, b.y, half, b.height, color);
            }
        }
    }

    pointer(pointer: CanvasPointer) {
        let geometry = this._geometry;
        if (geometry == null) return;
        let hit = BarChartLayout.hitTest(geometry, pointer.x, pointer.y);
        if (hit < 0 && this._hover < 0) return;
        this.setState(() => {
            this._hover = hit;
            this._pointerX = pointer.x;
            this._pointerY = pointer.y;
        });
    }

    tooltip(theme: any) {
        let geometry = this._geometry;
        if (this._hover < 0 || geometry == null || this._hover >= geometry.bars.length) return null;
        let bar = geometry.bars[this._hover];
        let series = this._series[bar.series];
        let card = new Column(4);
        card.add(new Text(this._categories.categories[bar.category], 'caption', theme.textMuted, 1));
        let line = new Row(8, 'start', 'center');
        line.add(new Box(new BoxStyle({ width: SizeValue.fixed(8), height: SizeValue.fixed(8), background: this.seriesColor(theme, bar.series), cornerRadius: new CornerRadii(2) })));
        line.add(new Text(this._values.label(series.at(bar.category)), 'label', theme.textPrimary, 1, 'start', false, true));
        line.add(new Text(series.name, 'caption', theme.textSecondary, 1));
        card.add(line);
        let box = new Box(new BoxStyle({ background: theme.surface, borderWidth: 1, borderColor: theme.border, cornerRadius: new CornerRadii(theme.shape('small')), padding: EdgeInsets.symmetric(8, 4) }), card);
        let left = this._pointerX < Math.fround(geometry.width / 2);
        let above = this._pointerY < Math.fround(geometry.height / 2);
        return new Positioned(box, above ? this._pointerY + 12 : null, left ? null : geometry.width - this._pointerX + 12, above ? null : geometry.height - this._pointerY + 12, left ? this._pointerX + 12 : null);
    }

    footer() {
        let row = new Row(0, 'end');
        row.add(new Button(this._table ? SdkStrings.showAsChart : SdkStrings.showAsTable, 'ghost', 'small', () => this.setState(() => this._table = !this._table)));
        return row;
    }
}

