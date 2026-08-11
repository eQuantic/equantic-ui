import { Box, BoxStyle, BuildContext, Button, Column, CornerRadii, DialogAction, EdgeInsets, KeyChord, Overlay, Presence, Pressable, Row, Shortcut, SizeValue, Stack, StatelessComponent, Text } from "@equantic/runtime";

export class Dialog extends StatelessComponent {
    declare title: string;
    declare body: string;
    declare actions: DialogAction[];
    declare dismissible: boolean;
    declare onDismiss: (() => void) | null;
    constructor(title?: any, body?: any, actions?: any, dismissible: any = false, onDismiss: any = null, props?: any) {
        super();
        if (title !== undefined) this.title = title;
        if (body !== undefined) this.body = body;
        if (actions !== undefined) this.actions = actions;
        if (dismissible !== undefined) this.dismissible = dismissible;
        if (onDismiss !== undefined) this.onDismiss = onDismiss;
        if (this.dismissible === undefined) this.dismissible = false;
        if ((actions.length === 0 || actions.length > 2)) throw new Error('A Dialog carries 1-2 actions — a third means an ActionSheet or a screen (spec C2).');this.title = title;this.body = body;this.actions = actions;this.dismissible = dismissible;this.onDismiss = onDismiss;
        if (props && typeof props === 'object') Object.assign(this, props);
    }

    build(context: BuildContext) {
        let theme = context.theme;let content = new Column(8);content.add(new Text(this.title, 'title'));content.add(new Text(this.body, 'bodyM', theme.textSecondary, 6));let actions = new Row(8, 'start', 'center', false, null, null, { main: 'end' });for (const action of this.actions) {actions.add(new Button(action.label, action.variant, 'medium', action.onPressed));}let body = new Column(20, 'start', 'stretch', false, null, null, { width: SizeValue.fill });body.add(content);body.add(actions);let scrim = new Pressable(new Box(new BoxStyle({ width: SizeValue.fill, height: SizeValue.fill, background: theme.scrim })), this.dismissible ? this.onDismiss : null, { disabled: !this.dismissible, label: 'dismiss' });let elevated = new Box(new BoxStyle({ width: SizeValue.fill, maxWidth: 480, background: theme.surface, cornerRadius: new CornerRadii(theme.shape('extraLarge')), elevation: 5, padding: EdgeInsets.all(20) }), body);if (context.inFlow) return elevated;let centering = new Column(0, 'start', 'stretch', false, null, null, { width: SizeValue.fill, height: SizeValue.fill, main: 'center', cross: 'center', padding: EdgeInsets.symmetric(24, 0) });centering.add(elevated);let layers = new Stack('topStart', { width: SizeValue.fill, height: SizeValue.fill });layers.add(scrim);layers.add(centering);let layer = new Overlay(new Presence(layers));let escape: any; return this.dismissible && (escape = this.onDismiss) != null ? new Shortcut(layer, KeyChord.escape, escape) : layer;
    }

}

