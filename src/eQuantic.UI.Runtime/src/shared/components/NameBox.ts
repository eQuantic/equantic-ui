import { $eq, Box, BoxStyle, BuildContext, CodeGrid, CodeSurface, EdgeInsets, EntryLines, Point, SdkStrings, SheetController, SheetPalette, Size, SizeValue, StatefulComponent, Text, TypeRoleValue, UiComponent } from "../runtime-exports";

export class NameBox extends StatefulComponent {
    static $typeId = 'eQuantic.UI.Spreadsheets.NameBox';
    static defaultWidth: number = 96;
    static boxType: TypeRoleValue = 'bodyM';
    _config: NameBox;
    _counted: any = null;
    _rebuild: () => void;

    static get padding() {
        return EdgeInsets.symmetric(8, 5);
    }

    declare controller: SheetController;
    declare width: SizeValue;

    constructor(controller?: any, props?: any) {
        super();
        if (controller !== undefined) this.controller = controller;
        if (this.width === undefined) this.width = SizeValue.fixed(NameBox.defaultWidth);
        this.controller = controller;
        this._config = this;
        this._rebuild = () => this.setState(() => {});
        if (props && typeof props === 'object') Object.assign(this, props);
    }

    build(context: BuildContext) {
        let theme = context.theme;
        let palette = SheetPalette.of(theme);
        let type = theme.type(NameBox.boxType);
        let padding = NameBox.padding;
        let box = this.controller.nameBox;
        box.grid = new CodeGrid(new Point(padding.start, padding.top), new Size(4, type.lineHeight), undefined, (text: string) => context.measureText(text, type));
        let text = box.typing ? new EntryLines(box.entry, box.grid, Point.zero, NameBox.boxType, palette, true) : new Box(new BoxStyle({ padding: padding }), new Text(this.label(), NameBox.boxType, palette.cellText, 1));
        let height = Math.fround(Math.fround(type.lineHeight + padding.top) + padding.bottom);
        let surface = new CodeSurface(new Box(new BoxStyle({ width: SizeValue.fill, height: SizeValue.fixed(height) }), text), box, { onChanged: () => this.controller.refresh(), label: SdkStrings.nameBox, caretColor: palette.cellText });
        return new Box(new BoxStyle({ width: this._config.width, height: SizeValue.fixed(Math.fround(height + 2)), background: palette.cellBackground, borderColor: box.invalid ? theme.colors('destructive').base : palette.gridline, borderWidth: box.invalid ? 2 : 1, clip: true }), surface);
    }

    adoptConfig(next: UiComponent) {
        let fresh: any; 
        if (!((next instanceof NameBox && (fresh = next, true)))) return;
        if (!(this._counted == null)) this.place(fresh.controller);
        this.controller = fresh.controller;
        this._config = fresh;
    }

    onMount() {
        return this.place(this.controller);
    }

    onUnmount() {
        return this.place(null);
    }

    place(controller: any) {
        if ((this._counted === controller)) return;
        if (!(this._counted == null)) {
            this._counted.nameBox.removed();
            this._counted.refreshed = $eq.delegates.remove(this._counted.refreshed, this._rebuild);
        }
        this._counted = controller;
        if (controller == null) return;
        controller.nameBox.placed();
        controller.refreshed = $eq.delegates.combine(controller.refreshed, this._rebuild);
    }

    label() {
        let selection = this.controller.selection;
        return this.controller.selecting ? SdkStrings.rangeSize(selection.rowCount, selection.colCount) : this.controller.activeCell.toA1();
    }
}

