import { Box, BoxStyle, BuildContext, CodeEditorController, CodeGrid, ColorToken, Point, Positioned, Rect, SheetPalette, SizeValue, Stack, StatelessComponent, Text, TypeRoleValue } from "../runtime-exports";

export class EntryLines extends StatelessComponent {
    static $typeId = 'eQuantic.UI.Spreadsheets.EntryLines';
    _entry: CodeEditorController;
    _grid: CodeGrid = CodeGrid.$zero();
    _corner: Point = new Point();
    _role: TypeRoleValue = 'display';
    _palette: SheetPalette = SheetPalette.$zero();
    _active: boolean = false;

    constructor(entry?: any, grid?: any, corner?: any, role?: any, palette?: any, active?: any, props?: any) {
        super();
        this._entry = entry;
        this._grid = grid;
        this._corner = corner;
        this._role = role;
        this._palette = palette;
        this._active = active;
        if (props && typeof props === 'object') Object.assign(this, props);
    }

    build(_context: BuildContext) {
        let document = this._entry.document;
        let lineHeight = this._grid.cell.height;
        let layers = new Stack();
        layers.add(new Box(new BoxStyle({ width: SizeValue.fill, height: SizeValue.fill })));
        if (this._active) this._entry.grid = this._grid;
        if (this._active) {
            for (const band of this._entry.selectionBandsIn(0, document.lineCount - 1)) layers.add(this.rectangle(band, this._palette.entrySelection));
        }
        for (let line = 0; line < document.lineCount; line++) {
            let text = document.line(line);
            if (text.length === 0) continue;
            let measure: any; 
            let width = (measure = this._grid.measure) != null ? measure(text) : Math.fround(Math.fround(text.length) * this._grid.cell.width);
            layers.add(new Positioned(new Box(new BoxStyle({ width: SizeValue.fixed(Math.fround(Math.ceil(width) + 2)), height: SizeValue.fixed(lineHeight) }), new Text(text, this._role, this._palette.cellText, 1)), Math.fround(Math.fround(this._grid.origin.y - this._corner.y) + Math.fround(Math.fround(line) * lineHeight)), null, null, Math.fround(this._grid.origin.x - this._corner.x)));
        }
        let composing: any; 
        if (this._active && (composing = this._entry.composition) != null && !composing.isEmpty) {
            let from = this._entry.caretRect(composing.start);
            let to = this._entry.caretRect(composing.end);
            if (from.y === to.y && to.x > from.x) layers.add(this.rectangle(new Rect(from.x, Math.fround(from.bottom - 1.5), Math.fround(to.x - from.x), 1.5), this._palette.cellText));
        }
        return layers;
    }

    rectangle(rect: Rect, colour: ColorToken) {
        return new Positioned(new Box(new BoxStyle({ width: SizeValue.fixed(rect.width), height: SizeValue.fixed(rect.height), background: colour })), Math.fround(rect.y - this._corner.y), null, null, Math.fround(rect.x - this._corner.x));
    }
}

