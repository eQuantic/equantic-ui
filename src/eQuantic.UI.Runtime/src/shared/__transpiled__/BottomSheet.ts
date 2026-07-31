import { Box, BoxStyle, BuildContext, Column, CornerRadii, DragDismiss, EdgeInsets, Overlay, Presence, Pressable, Radius, Row, SizeValue, Space, Stack, StatelessComponent, VisualNode } from "@equantic/runtime";

export class BottomSheet extends StatelessComponent {
    declare content: VisualNode;
    declare onDismiss: () => void;
    declare dismissible: boolean;
    constructor(content?: any, onDismiss: any = null, dismissible: any = true, props?: any) {
        super(props);
        if (content !== undefined) this.content = content;
        if (onDismiss !== undefined) this.onDismiss = onDismiss;
        if (dismissible !== undefined) this.dismissible = dismissible;
        this.content = content;this.onDismiss = onDismiss;this.dismissible = dismissible;
    }

    build(context: BuildContext) {
        let theme = context.theme;let scrim = new Pressable(new Box(new BoxStyle({ width: SizeValue.fill, height: SizeValue.fill, background: theme.scrim })), this.dismissible ? this.onDismiss : null, { disabled: !this.dismissible, label: 'dismiss' });let body = new Column(Space.s3, { width: SizeValue.fill, cross: 'stretch' });let handleRow = new Row(0, { width: SizeValue.fill, main: 'center' });handleRow.add(new Box(new BoxStyle({ width: 32, height: 4, background: theme.borderStrong, cornerRadius: new CornerRadii(Radius.full) })));body.add(handleRow);body.add(this.content);let radius = theme.shape('extraLarge');let sheet = new Box(new BoxStyle({ width: SizeValue.fill, background: theme.surface, cornerRadius: new CornerRadii(radius, radius, 0, 0), elevation: 4, padding: new EdgeInsets(Space.s5, Space.s3, Space.s5, Space.s6) }), body);let anchor = new Column(0, { width: SizeValue.fill, height: SizeValue.fill, main: 'end' });let sheetNode = this.dismissible ? new DragDismiss(sheet, this.onDismiss) : sheet;anchor.add(new Presence(sheetNode, 'slideUp'));let layers = new Stack('topStart', { width: SizeValue.fill, height: SizeValue.fill });layers.add(new Presence(scrim));layers.add(anchor);return new Overlay(layers);
    }

}

