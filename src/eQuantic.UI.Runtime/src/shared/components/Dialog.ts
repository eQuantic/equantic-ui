import { $eq, Box, BoxStyle, BuildContext, Button, Column, Component, ComponentContext, CornerRadii, DialogAction, EdgeInsets, HtmlElement, Overlay, Pressable, Radius, Row, SizeValue, Space, Stack, StatelessComponent, Text, VisualNode } from "../runtime-exports";

export class Dialog extends StatelessComponent {
    constructor(title?: any, body?: any, actions?: any, dismissible: any = false, onDismiss: any = null, props?: any) {
        super(props);
        if (title !== undefined) this.title = title;
        if (body !== undefined) this.body = body;
        if (actions !== undefined) this.actions = actions;
        if (dismissible !== undefined) this.dismissible = dismissible;
        if (onDismiss !== undefined) this.onDismiss = onDismiss;
        if ((actions.length === 0 || actions.length > 2)) throw new Error('A Dialog carries 1-2 actions — a third means an ActionSheet or a screen (spec C2).');this.title = title;this.body = body;this.actions = actions;this.dismissible = dismissible;this.onDismiss = onDismiss;
    }

    build(context: BuildContext) {
        let theme = context.theme;let content = new Column(8);content.add(new Text(this.title, 'title'));content.add(new Text(this.body, 'bodyM', theme.textSecondary, 6));let actions = new Row(8, { main: 'end' });for (const action of this.actions) {actions.add(new Button(action.label, action.variant, 'medium', action.onPressed));}let body = new Column(20, { width: SizeValue.fill });body.add(content);body.add(actions);let scrim = new Pressable(new Box(new BoxStyle({ width: SizeValue.fill, height: SizeValue.fill, background: theme.scrim })), this.dismissible ? this.onDismiss : null, { disabled: !this.dismissible, label: 'dismiss' });let elevated = new Box(new BoxStyle({ width: SizeValue.fill, maxWidth: 480, background: theme.surface, cornerRadius: new CornerRadii(Radius.xl), elevation: 5, padding: EdgeInsets.all(Space.s5) }), body);let centering = new Column(0, { width: SizeValue.fill, height: SizeValue.fill, main: 'center', cross: 'center', padding: EdgeInsets.symmetric(Space.s6, 0) });centering.add(elevated);let layers = new Stack('topStart', { width: SizeValue.fill, height: SizeValue.fill });layers.add(scrim);layers.add(centering);return new Overlay(layers);
    }

}

