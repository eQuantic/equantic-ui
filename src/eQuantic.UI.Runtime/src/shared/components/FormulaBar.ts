import { $eq, Box, BoxStyle, BuildContext, CodeDocument, CodeGrid, CodeSurface, Column, EdgeInsets, EntryLines, Point, ScrollView, SdkStrings, SheetController, SheetPalette, Size, SizeValue, StatefulComponent, Text, TypeRoleValue, TypeStyle, UiComponent } from "../runtime-exports";

export class FormulaBar extends StatefulComponent {
    static $typeId = 'eQuantic.UI.Spreadsheets.FormulaBar';
    static barType: TypeRoleValue = 'bodyM';
    _config: FormulaBar;
    _rebuild: () => void;
    _listening: any = null;
    _viewportWidth: number = 0;

    static get padding() {
        return EdgeInsets.symmetric(8, 5);
    }

    declare controller: SheetController;
    declare width: SizeValue;

    constructor(controller?: any, props?: any) {
        super();
        if (controller !== undefined) this.controller = controller;
        if (this.width === undefined) this.width = SizeValue.fill;
        this.controller = controller;
        this._config = this;
        this._rebuild = () => this.setState(() => {});
        if (props && typeof props === 'object') Object.assign(this, props);
    }

    build(context: BuildContext) {
        let theme = context.theme;
        let palette = SheetPalette.of(theme);
        let type = theme.type(FormulaBar.barType);
        let padding = FormulaBar.padding;
        let measure = (text: string) => context.measureText(text, type);
        let bar = this.controller.formulaBar;
        bar.grid = new CodeGrid(new Point(padding.start, padding.top), new Size(Math.max(measure(' '), 4), type.lineHeight), undefined, measure);
        let editing = this.controller.editing;
        let lines = editing ? FormulaBar.lines(this.controller.entry.document) : [...$eq.text.split(this.controller.document.getCell(this.controller.activeCell), '\n')];
        let widest = 0;
        for (const line of lines) widest = Math.max(widest, measure(line));
        let height = Math.fround(Math.fround(Math.fround(Math.fround(Math.max(1, lines.length)) * type.lineHeight) + padding.top) + padding.bottom);
        let text = editing ? new EntryLines(this.controller.entry, bar.grid, Point.zero, FormulaBar.barType, palette, bar.holdsTheCaret) : FormulaBar.atRest(lines, padding, type, palette);
        let content = new Box(new BoxStyle({ width: SizeValue.fixed(Math.max(this._viewportWidth, Math.fround(Math.fround(Math.fround(Math.ceil(widest) + padding.start) + padding.end) + 2))), minWidth: 0, height: SizeValue.fixed(height) }), text);
        let surface = new CodeSurface(content, bar, { onChanged: () => this.controller.refresh(), label: SdkStrings.formulaBar, caretColor: palette.cellText });
        return new Box(new BoxStyle({ width: this._config.width, height: SizeValue.fixed(Math.fround(height + 2)), background: palette.cellBackground, borderColor: palette.gridline, borderWidth: 1, clip: true }), new ScrollView(surface, 'horizontal', { width: SizeValue.fill, onViewportChanged: (width: number) => {
            if (Math.abs(Math.fround(width - this._viewportWidth)) < 1) return;
            this.setState(() => this._viewportWidth = width);
        } }));
    }

    adoptConfig(next: UiComponent) {
        let fresh: any; 
        if (!((next instanceof FormulaBar && (fresh = next, true)))) return;
        if (!(this._listening == null)) this.listen(fresh.controller);
        this.controller = fresh.controller;
        this._config = fresh;
    }

    onMount() {
        return this.listen(this.controller);
    }

    onUnmount() {
        return this.listen(null);
    }

    listen(controller: any) {
        if ((this._listening === controller)) return;
        if (!(this._listening == null)) this._listening.refreshed = $eq.delegates.remove(this._listening.refreshed, this._rebuild);
        this._listening = controller;
        if (!(controller == null)) controller.refreshed = $eq.delegates.combine(controller.refreshed, this._rebuild);
    }

    static lines(document: CodeDocument) {
        let lines: string[] = ($eq.collections.listCapacity(document.lineCount), []);
        for (let line = 0; line < document.lineCount; line++) lines.push(document.line(line));
        return lines;
    }

    static atRest(lines: string[], padding: EdgeInsets, type: TypeStyle, palette: SheetPalette) {
        let column = new Column(0);
        for (const line of lines) {
            column.add(new Box(new BoxStyle({ height: SizeValue.fixed(type.lineHeight) }), line.length === 0 ? null : new Text(line, FormulaBar.barType, palette.cellText, 1)));
        }
        return new Box(new BoxStyle({ padding: padding }), column);
    }
}

