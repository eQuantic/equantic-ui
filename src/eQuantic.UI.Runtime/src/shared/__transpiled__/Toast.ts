import { Box, BoxStyle, BuildContext, Column, CornerRadii, EdgeInsets, Overlay, Presence, Pressable, Row, SizeValue, StatelessComponent, Text, VariantValue } from "@equantic/runtime";

export class Toast extends StatelessComponent {
    declare message: string;
    declare status: VariantValue;
    declare actionLabel: any;
    declare onAction: (() => void) | null;
    constructor(message?: any, status: any = 'info', actionLabel: any = null, onAction: any = null, props?: any) {
        super();
        if (message !== undefined) this.message = message;
        if (status !== undefined) this.status = status;
        if (actionLabel !== undefined) this.actionLabel = actionLabel;
        if (onAction !== undefined) this.onAction = onAction;
        if (this.status === undefined) this.status = 'primary';
        this.message = message;this.status = status;this.actionLabel = actionLabel;this.onAction = onAction;
        if (props && typeof props === 'object') Object.assign(this, props);
    }

    build(context: BuildContext) {
        let theme = context.theme;let row = new Row(12, 'start', 'center', false, null, null, { cross: 'center' });row.add(new Box(new BoxStyle({ width: 8, height: 8, background: theme.colors(this.status).base, cornerRadius: new CornerRadii(999) })));row.add(new Text(this.message, 'bodyM', theme.textInverse, 2));let label: any; if ((label = this.actionLabel) != null) {row.add(new Pressable(new Box(new BoxStyle({ padding: EdgeInsets.symmetric(8, 4) }), new Text(label, 'label', theme.textInverse)), this.onAction, { label: label }));}let pill = new Box(new BoxStyle({ background: theme.textPrimary, cornerRadius: new CornerRadii(theme.shape('full')), elevation: 3, padding: EdgeInsets.symmetric(16, 12), maxWidth: 480 }), row);let anchor = new Column(0, 'start', 'stretch', false, null, null, { width: SizeValue.fill, height: SizeValue.fill, main: 'end', cross: 'center', padding: EdgeInsets.symmetric(16, 24) });anchor.add(new Presence(pill, 'slideUp'));return new Overlay(anchor, { modal: false });
    }

}

