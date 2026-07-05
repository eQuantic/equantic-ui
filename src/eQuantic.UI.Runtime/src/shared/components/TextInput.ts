import { $eq, Box, BoxStyle, BuildContext, ColorToken, Column, Component, ComponentContext, CornerRadii, EdgeInsets, Flexible, HtmlElement, Icon, IconSize, Radius, Row, SharedStatefulComponent, SizeValue, Text, TextEntry, UiComponent, VisualNode } from "../runtime-exports";

export class TextInput extends SharedStatefulComponent {
    _focused: boolean = false;
    constructor(value?: any, onChanged: any = null, label: any = '', placeholder: any = null, helper: any = null, error: any = null, leading: any = null, size: any = 'large', props?: any) {
        super(props);
        if (value !== undefined) this.value = value;
        if (onChanged !== undefined) this.onChanged = onChanged;
        if (label !== undefined) this.label = label;
        if (placeholder !== undefined) this.placeholder = placeholder;
        if (helper !== undefined) this.helper = helper;
        if (error !== undefined) this.error = error;
        if (leading !== undefined) this.leading = leading;
        if (size !== undefined) this.size = size;
        if (size === 'small') throw new Error('TextInput has no Small size — text + padding can\'t fit 32dp (spec B9).');this.value = value;this.onChanged = onChanged;this.label = label;this.placeholder = placeholder;this.helper = helper;this.error = error;this.leading = leading;this.size = size;
    }

    build(context: BuildContext) {
        let theme = context.theme;let height = (() => { const _s = this.size; if (_s === 'medium') return 40; if (_s === 'xLarge') return 56; return 48; })();let hasError = !!this.error;let borderColor = hasError ? theme.colors('destructive').base : this._focused ? theme.colors('primary').base : theme.borderStrong;let borderWidth = this._focused ? 2 : 1;let paddingX = this._focused ? 13 : 14;let row = new Row(10, { height: SizeValue.fill, cross: 'center' });let leading; if (((this.leading != null) && (leading = this.leading, true))) {row.add(new Icon(leading, IconSize.dense, theme.textMuted));}row.add(new Flexible(new TextEntry(this.value, this.onChanged, { placeholder: this.placeholder, disabled: this.disabled, obscure: this.obscure, onFocusChanged: (focused) => this.setState(() => this._focused = focused) }), 1));let container = new Box(new BoxStyle({ width: SizeValue.fill, height: height, background: this.disabled ? theme.surfaceSubtle : theme.surface, cornerRadius: new CornerRadii(Radius.md), borderWidth: borderWidth, borderColor: borderColor, padding: EdgeInsets.symmetric(paddingX, 0) }), row);let top = new Column(6, { width: SizeValue.fill });if (this.label.length > 0) top.add(new Text(this.label, 'label', theme.textSecondary));top.add(container);let caption = hasError ? this.error! : this.helper ?? '';let captionColor = hasError ? theme.colors('destructive').base : theme.textMuted;let column = new Column(5, { width: SizeValue.fill });column.add(top);column.add(new Text(caption, 'caption', captionColor, 1));return column;
    }

    adoptConfig(next: UiComponent) {
        let fresh; if (!((next instanceof TextInput && (fresh = next, true)))) return;this.value = fresh.value;this.onChanged = fresh.onChanged;this.label = fresh.label;this.placeholder = fresh.placeholder;this.helper = fresh.helper;this.error = fresh.error;this.leading = fresh.leading;this.size = fresh.size;
    }

}

