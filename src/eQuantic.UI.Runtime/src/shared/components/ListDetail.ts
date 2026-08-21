import { AdaptiveNode, AppBar, Box, BoxStyle, BuildContext, Column, Divider, EmptyState, Flexible, IconButton, Row, SdkStrings, SizeValue, StatelessComponent, VisualNode } from "../runtime-exports";

export class ListDetail extends StatelessComponent {
    declare list: VisualNode;
    declare detail: any;
    declare onBack: (() => void) | null;
    declare title: any;
    declare listTitle: any;
    declare listWidth: number;
    declare placeholder: any;
    declare twoPaneFrom: number;
    constructor(list?: any, detail: any = null, onBack: any = null, props?: any) {
        super();
        if (list !== undefined) this.list = list;
        if (detail !== undefined) this.detail = detail;
        if (onBack !== undefined) this.onBack = onBack;
        if (this.listWidth === undefined) this.listWidth = 360;
        if (this.twoPaneFrom === undefined) this.twoPaneFrom = 840;
        this.list = list;
        this.detail = detail;
        this.onBack = onBack;
        if (props && typeof props === 'object') Object.assign(this, props);
    }

    build(_context: BuildContext) {
        const pane = (title: string | null, body: VisualNode, leading?: IconButton | null) => {
            let column = new Column(0, 'start', 'stretch', false, null, null, { width: SizeValue.fill, height: SizeValue.fill });
            if (!(title == null)) column.add(new AppBar(title, { leading: leading }));
            column.add(new Flexible(body));
            return column;
        };
        const back = () => { return this.onBack == null ? null : new IconButton('chevronLeft', SdkStrings.back, 'standard', 'medium', this.onBack); };
        let chosen: any; 
        let compact = (chosen = this.detail) != null ? pane(this.title, chosen, back()) : pane(this.listTitle, this.list);
        let wide = new Row(0, 'start', 'center', false, null, null, { width: SizeValue.fill, height: SizeValue.fill });
        wide.add(new Box(new BoxStyle({ width: this.listWidth, height: SizeValue.fill }), pane(this.listTitle, this.list)));
        wide.add(new Divider('none', 'vertical'));
        wide.add(new Flexible(pane(this.title, this.detail ?? (this.placeholder ?? ListDetail.nothing()))));
        return new AdaptiveNode(compact, null, wide, { expandedFrom: this.twoPaneFrom });
    }

    static nothing() {
        return new EmptyState('info', SdkStrings.nothingSelected);
    }

}

