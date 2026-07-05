import { $eq, Box, BoxStyle, BuildContext, ColorToken, Component, ComponentContext, CornerRadii, HtmlElement, Icon, IconSize, Pressable, Radius, Row, SizeValue, Space, StatelessComponent, Text, VariantColors, VisualNode } from "@equantic/runtime";

export class Checkbox extends StatelessComponent {
    constructor(checked?: any, onChanged: any = null, label: any = null, props?: any) {
        super(props);
        if (checked !== undefined) this.checked = checked;
        if (onChanged !== undefined) this.onChanged = onChanged;
        if (label !== undefined) this.label = label;
        this.checked = checked;this.onChanged = onChanged;this.label = label;
    }

    build(context: BuildContext) {
        let theme = context.theme;let primary = theme.colors('primary');let borderColor = this.error ? theme.colors('destructive').base : theme.borderStrong;let boxContent = new Row(0, { main: 'center', height: SizeValue.fill });if (this.checked) boxContent.add(new Icon('check', IconSize.sm, primary.onBase));let box = new Box(new BoxStyle({ width: 22, height: 22, background: this.checked ? primary.base : null, cornerRadius: new CornerRadii(Radius.xs), borderWidth: this.checked ? 0 : 2, borderColor: borderColor }), boxContent);let row = new Row(Space.s3, { cross: 'center' });row.add(box);{ let label; if (((this.label != null) && (label = this.label, true))) row.add(new Text(label, 'bodyM', this.disabled ? theme.textMuted : theme.textPrimary, 2)); }return new Pressable(row, this.disabled ? null : this.onChanged, { disabled: this.disabled, label: this.label ?? (this.checked ? 'Checked' : 'Unchecked') });
    }

}

