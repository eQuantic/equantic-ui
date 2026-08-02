import { Box, BoxStyle, BuildContext, CornerRadii, Icon, Pressable, Row, StatelessComponent, Text } from "../runtime-exports";

export class Checkbox extends StatelessComponent {
    declare checked: boolean;
    declare onChanged: (() => void) | null;
    declare label: any;
    declare disabled: boolean;
    declare error: boolean;
    declare indeterminate: boolean;
    constructor(checked?: any, onChanged: any = null, label: any = null, props?: any) {
        super(props);
        if (checked !== undefined) this.checked = checked;
        if (onChanged !== undefined) this.onChanged = onChanged;
        if (label !== undefined) this.label = label;
        this.checked = checked;this.onChanged = onChanged;this.label = label;
    }

    build(context: BuildContext) {
        let theme = context.theme;let primary = theme.colors('primary');let borderColor = this.error ? theme.colors('destructive').base : theme.borderStrong;let filled = this.checked || this.indeterminate;let glyph = this.indeterminate ? new Icon('minus', 16, primary.onBase) : this.checked ? new Icon('check', 16, primary.onBase) : null;let boxContent = glyph?.centered();let box = new Box(new BoxStyle({ width: 22, height: 22, background: filled ? primary.base : null, cornerRadius: new CornerRadii(theme.shape('extraSmall')), borderWidth: filled ? 0 : 2, borderColor: borderColor }), boxContent);let row = new Row(12, { cross: 'center' });row.add(box);let label; if (((this.label != null) && (label = this.label, true))) row.add(new Text(label, 'bodyM', this.disabled ? theme.textMuted : theme.textPrimary, 2));return new Pressable(row, this.disabled ? null : this.onChanged, { disabled: this.disabled, label: this.label ?? (this.indeterminate ? 'Partly selected' : this.checked ? 'Checked' : 'Unchecked') });
    }

}

