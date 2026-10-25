import { $eq, Box, BoxStyle, BuildContext, Decimal, EdgeInsets, Row, SdkStrings, SheetController, SheetPalette, SizeValue, Spacer, StatefulComponent, Text, TypeRoleValue, UiComponent } from "../runtime-exports";

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
            if ((average = summary.average) != null) line.add(StatusBar.words(SdkStrings.average(StatusBar.general(average)), palette));
            line.add(StatusBar.words(SdkStrings.count(summary.count), palette));
            let sum: any; 
            if (summary.numbers > 0 && (sum = summary.sum) != null) line.add(StatusBar.words(SdkStrings.sum(StatusBar.general(sum)), palette));
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

    static general(value: Decimal) {
        let negative = (value.compareTo($eq.num.dec(0)) < 0);
        let scientific = negative ? '0.####E+00' : '0.#####E+00';
        let whole = $eq.text.format((negative ? value.neg() : value), '0', undefined, true).length;
        let room = 11 - (negative ? 1 : 0) - whole;
        if (room < 0) return $eq.text.format(value, scientific);
        let places = Math.max(0, room - 1);
        let fixedText = $eq.text.format(value, places === 0 ? '0' : '0.' + '#'.repeat(places));
        if (value.equals($eq.num.dec(0))) return fixedText;
        let scientificText = $eq.text.format(value, scientific);
        return (StatusBar.lost(value, scientificText).compareTo(StatusBar.lost(value, fixedText)) < 0) ? scientificText : fixedText;
    }

    static lost(value: Decimal, text: string) {
        let lost = value.sub($eq.num.decParse(text, 167));
        return (lost.compareTo($eq.num.dec(0)) < 0) ? lost.neg() : lost;
    }

    static words(text: string, palette: SheetPalette) {
        return new Text(text, StatusBar.barType, palette.headerText, 1);
    }
}

