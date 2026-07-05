import { $eq, Box, BoxStyle, BuildContext, Column, Component, ComponentContext, EdgeInsets, Flexible, HtmlElement, Pressable, Row, SizeValue, Space, StatelessComponent, Text, TypeStyle, VisualNode } from "@equantic/runtime";

export class ListItem extends StatelessComponent {
    constructor(title?: any, subtitle: any = null, onPressed: any = null, props?: any) {
        super(props);
        if (title !== undefined) this.title = title;
        if (subtitle !== undefined) this.subtitle = subtitle;
        if (onPressed !== undefined) this.onPressed = onPressed;
        this.title = title;this.subtitle = subtitle;this.onPressed = onPressed;
    }

    build(context: BuildContext) {
        let theme = context.theme;let content = new Column(2);content.add(new Text(this.title, 'bodyM', theme.textPrimary, 1, { styleOverride: new TypeStyle(15, 20, 'medium', 0, 1.3) }));let subtitle; if (((this.subtitle != null) && (subtitle = this.subtitle, true))) content.add(new Text(subtitle, 'caption', theme.textSecondary, 1));let row = new Row(Space.s3, { width: SizeValue.fill, cross: 'center', padding: EdgeInsets.symmetric(Space.s4, Space.s2) });let leading; if (((this.leading != null) && (leading = this.leading, true))) row.add(leading);row.add(new Flexible(content));let trailing; if (((this.trailing != null) && (trailing = this.trailing, true))) row.add(trailing);let body = new Box(new BoxStyle({ width: SizeValue.fill, minHeight: this.subtitle === null ? 52 : 68 }), row);return this.onPressed === null ? body : new Pressable(body, this.disabled ? null : this.onPressed, { disabled: this.disabled, label: this.title, pressedBackground: theme.surfaceSubtle });
    }

}

