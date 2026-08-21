import { Box, BoxStyle, BuildContext, Column, CornerRadii, DragDismiss, EdgeInsets, KeyChord, Overlay, Presence, Pressable, Row, SdkStrings, Shortcut, SizeValue, Stack, StatelessComponent, VisualNode } from "@equantic/runtime";

export class BottomSheet extends StatelessComponent {
    declare content: VisualNode;
    declare onDismiss: (() => void) | null;
    declare dismissible: boolean;
    constructor(content?: any, onDismiss: any = null, dismissible: any = true, props?: any) {
        super();
        if (content !== undefined) this.content = content;
        if (onDismiss !== undefined) this.onDismiss = onDismiss;
        if (dismissible !== undefined) this.dismissible = dismissible;
        if (this.dismissible === undefined) this.dismissible = false;
        this.content = content;
        this.onDismiss = onDismiss;
        this.dismissible = dismissible;
        if (props && typeof props === 'object') Object.assign(this, props);
    }

    build(context: BuildContext) {
        let theme = context.theme;
        let scrim = new Pressable(new Box(new BoxStyle({ width: SizeValue.fill, height: SizeValue.fill, background: theme.scrim })), this.dismissible ? this.onDismiss : null, { disabled: !this.dismissible, label: SdkStrings.dismiss });
        let body = new Column(12, 'start', 'stretch', false, null, null, { width: SizeValue.fill, cross: 'stretch' });
        let handleRow = new Row(0, 'start', 'center', false, null, null, { width: SizeValue.fill, main: 'center' });
        handleRow.add(new Box(new BoxStyle({ width: 32, height: 4, background: theme.borderStrong, cornerRadius: new CornerRadii(999) })));
        body.add(handleRow);
        body.add(this.content);
        let radius = theme.shape('extraLarge');
        let sheet = new Box(new BoxStyle({ width: SizeValue.fill, background: theme.surface, cornerRadius: new CornerRadii(radius, radius, 0, 0), elevation: 4, padding: new EdgeInsets(20, 12, 20, 24) }), body);
        if (context.inFlow) return sheet;
        let anchor = new Column(0, 'start', 'stretch', false, null, null, { width: SizeValue.fill, height: SizeValue.fill, main: 'end' });
        let sheetNode = this.dismissible ? new DragDismiss(sheet, this.onDismiss) : sheet;
        anchor.add(new Presence(sheetNode, 'slideUp'));
        let layers = new Stack('topStart', { width: SizeValue.fill, height: SizeValue.fill });
        layers.add(new Presence(scrim));
        layers.add(anchor);
        let layer = new Overlay(layers);
        let escape: any; 
        return this.dismissible && (escape = this.onDismiss) != null ? new Shortcut(layer, KeyChord.escape, escape) : layer;
    }

}

