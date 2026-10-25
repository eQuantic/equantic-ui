import { $eq, Box, BoxStyle, BuildContext, EdgeInsets, Row, SdkStrings, SheetController, SheetPalette, SizeValue, Spacer, StatefulComponent, Text, TypeRoleValue, UiComponent } from "../runtime-exports";

export class StatusBar extends StatefulComponent {
    static $typeId = 'eQuantic.UI.Spreadsheets.StatusBar';
    static barType: TypeRoleValue = 'caption';
    _config: StatusBar;
    _rebuild: () => void;
    _listening: any = null;

    static get padding() {
        return EdgeInsets.symmetric(8, 4);
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
        let type = theme.type(StatusBar.barType);
        let padding = StatusBar.padding;
        let line = new Row(16);
        if (this.controller.addingRanges) line.add(StatusBar.words(SdkStrings.addToSelection, palette));
        line.add(new Spacer());
        let summary = this.controller.summary;
        if (summary.count >= 2) {
            let average: any; 
            if ((average = summary.average) != null) line.add(StatusBar.words(SdkStrings.average($eq.text.format(average, null)), palette));
            line.add(StatusBar.words(SdkStrings.count(summary.count), palette));
            if (summary.numbers > 0) line.add(StatusBar.words(SdkStrings.sum($eq.text.format(summary.sum, null)), palette));
        }
        return new Box(new BoxStyle({ width: this._config.width, height: SizeValue.fixed(Math.fround(Math.fround(Math.fround(type.lineHeight + padding.top) + padding.bottom) + 2)), padding: padding, background: palette.headerBackground, borderColor: palette.gridline, borderWidth: 1 }), line);
    }

    adoptConfig(next: UiComponent) {
        let fresh: any; 
        if (!((next instanceof StatusBar && (fresh = next, true)))) return;
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

    static words(text: string, palette: SheetPalette) {
        return new Text(text, StatusBar.barType, palette.headerText, 1);
    }
}

