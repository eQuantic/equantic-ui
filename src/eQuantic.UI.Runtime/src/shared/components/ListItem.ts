import { Avatar, Box, BoxStyle, BuildContext, Column, EdgeInsets, Flexible, Icon, Pressable, Row, SizeValue, Sizing, StatelessComponent, Text, TypeStyle, VisualNode } from "../runtime-exports";

export class ListItem extends StatelessComponent {
    declare title: string;
    declare subtitle: any;
    declare onPressed: (() => void) | null;
    declare disabled: boolean;
    declare leading: any;
    declare leadingWidth: number;
    declare trailing: any;
    declare selected: boolean;
    declare subtitleLines: number;
    get minHeight() { return this.subtitle == null ? 52 : this.subtitleLines >= 2 ? 88 : 68; }
    get contentInset() { return this.leading == null ? 16 : 16 + ListItem.slotWidth(this.leading, this.leadingWidth) + 12; }
    constructor(title?: any, subtitle: any = null, onPressed: any = null, props?: any) {
        super();
        if (title !== undefined) this.title = title;
        if (subtitle !== undefined) this.subtitle = subtitle;
        if (onPressed !== undefined) this.onPressed = onPressed;
        if (this.disabled === undefined) this.disabled = false;
        if (this.leadingWidth === undefined) this.leadingWidth = 24;
        if (this.selected === undefined) this.selected = false;
        if (this.subtitleLines === undefined) this.subtitleLines = 1;
        this.title = title;this.subtitle = subtitle;this.onPressed = onPressed;
        if (props && typeof props === 'object') Object.assign(this, props);
    }

    build(context: BuildContext) {
        let theme = context.theme;let content = new Column(2);content.add(new Text(this.title, 'bodyM', theme.textPrimary, 1, 'start', false, false, null, { styleOverride: new TypeStyle(15, 20, 'medium', 0, 1.3) }));let subtitle: any; if ((subtitle = this.subtitle) != null) content.add(new Text(subtitle, 'caption', theme.textSecondary, this.subtitleLines >= 2 ? 2 : 1));let row = new Row(12, 'start', 'center', false, null, null, { width: SizeValue.fill, cross: 'center', padding: EdgeInsets.symmetric(16, 8) });let leading: any; if ((leading = this.leading) != null) row.add(leading);row.add(new Flexible(content));let trailing: any; if ((trailing = this.trailing) != null) row.add(trailing);let body = new Box(new BoxStyle({ width: SizeValue.fill, minHeight: this.minHeight, background: this.selected ? theme.colors('primary').subtle : null }), row);return this.onPressed == null ? body : new Pressable(body, this.disabled ? null : this.onPressed, { disabled: this.disabled, label: this.title, pressedBackground: theme.surfaceSubtle, role: this.selected ? 'destination' : 'button', selected: this.selected ? true : null });
    }

    static slotWidth(leading: VisualNode, declared: number) {
        return (() => { const _s = leading; if (_s instanceof Icon) { const icon = _s; return icon.size; } if (_s instanceof Avatar) { const avatar = _s; return Sizing.avatar(avatar.size); } return declared; })();
    }

}

