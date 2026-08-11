import { Box, BoxStyle, BuildContext, Column, CornerRadii, EdgeInsets, Flexible, Icon, Row, SharedStatefulComponent, SizeValue, Sizing, Text, TextEntry, UiComponent } from "../runtime-exports";

export class TextInput extends SharedStatefulComponent {
    _focused: boolean = false;
    declare value: string;
    declare onChanged: ((string: string) => void) | null;
    declare label: string;
    declare placeholder: any;
    declare helper: any;
    declare error: any;
    declare leading: any;
    declare size: string;
    declare disabled: boolean;
    declare autofocus: boolean;
    declare obscure: boolean;
    constructor(value?: any, onChanged: any = null, label: any = '', placeholder: any = null, helper: any = null, error: any = null, leading: any = null, size: any = 'large', props?: any) {
        super();
        if (value !== undefined) this.value = value;
        if (onChanged !== undefined) this.onChanged = onChanged;
        if (label !== undefined) this.label = label;
        if (placeholder !== undefined) this.placeholder = placeholder;
        if (helper !== undefined) this.helper = helper;
        if (error !== undefined) this.error = error;
        if (leading !== undefined) this.leading = leading;
        if (size !== undefined) this.size = size;
        if (this.size === undefined) this.size = 'small';
        if (this.disabled === undefined) this.disabled = false;
        if (this.autofocus === undefined) this.autofocus = false;
        if (this.obscure === undefined) this.obscure = false;
        if (size === 'small') throw new Error('TextInput has no Small size — text + padding can\'t fit 32dp (spec B9).');this.value = value;this.onChanged = onChanged;this.label = label;this.placeholder = placeholder;this.helper = helper;this.error = error;this.leading = leading;this.size = size;
        if (props && typeof props === 'object') Object.assign(this, props);
    }

    build(context: BuildContext) {
        let theme = context.theme;let height = Sizing.height(this.size, context.density);let hasError = !!this.error;let borderColor = hasError ? theme.colors('destructive').base : this._focused ? theme.colors('primary').base : theme.borderStrong;let borderWidth = this._focused ? 2 : 1;let paddingX = this._focused ? 13 : 14;let row = new Row(10, 'start', 'center', false, null, null, { height: SizeValue.fill, cross: 'center' });let leading: any; if ((leading = this.leading) != null) {row.add(new Icon(leading, 20, theme.textMuted));}row.add(new Flexible(new TextEntry(this.value, this.onChanged, { placeholder: this.placeholder, disabled: this.disabled, obscure: this.obscure, autofocus: this.autofocus, onFocusChanged: (focused: boolean) => this.setState(() => this._focused = focused) }), 1));let container = new Box(new BoxStyle({ width: SizeValue.fill, height: height, background: this.disabled ? theme.surfaceSubtle : theme.surface, cornerRadius: new CornerRadii(theme.shape('medium')), borderWidth: borderWidth, borderColor: borderColor, padding: EdgeInsets.symmetric(paddingX, 0) }), row);let top = new Column(6, 'start', 'stretch', false, null, null, { width: SizeValue.fill });if (this.label.length > 0) top.add(new Text(this.label, 'label', theme.textSecondary));top.add(container);let caption = hasError ? this.error! : this.helper ?? '';let captionColor = hasError ? theme.colors('destructive').base : theme.textMuted;let column = new Column(5, 'start', 'stretch', false, null, null, { width: SizeValue.fill });column.add(top);column.add(new Text(caption, 'caption', captionColor, 1));return column;
    }

    adoptConfig(next: UiComponent) {
        let fresh: any; if (!((next instanceof TextInput && (fresh = next, true)))) return;this.value = fresh.value;this.onChanged = fresh.onChanged;this.label = fresh.label;this.placeholder = fresh.placeholder;this.helper = fresh.helper;this.error = fresh.error;this.leading = fresh.leading;this.size = fresh.size;
    }

}

