import { $eq, Box, BoxStyle, BuildContext, Column, ControlMetrics, CornerRadii, EdgeInsets, Flexible, Icon, IconGlyph, Row, SizeValue, SizeVariantValue, StatefulComponent, Text, TextEntry, UiComponent } from "../runtime-exports";

export class TextInput extends StatefulComponent {
    static $typeId = 'eQuantic.UI.Components.TextInput';
    _focused: boolean = false;
    _disabled: boolean = false;
    _autofocus: boolean = false;
    _obscure: boolean = false;
    _onFocusChanged: ((bool: boolean) => void) | null = null;
    declare value: string;
    declare onChanged: ((string: string) => void) | null;
    declare label: string;
    declare placeholder: any;
    declare helper: any;
    declare error: any;
    declare leading: any;
    declare trailing: any;
    declare size: SizeVariantValue;

    get disabled() {
        return this._disabled;
    }

    set disabled(value) {
        this._disabled = value;
    }

    get autofocus() {
        return this._autofocus;
    }

    set autofocus(value) {
        this._autofocus = value;
    }

    get obscure() {
        return this._obscure;
    }

    set obscure(value) {
        this._obscure = value;
    }

    get onFocusChanged() {
        return this._onFocusChanged;
    }

    set onFocusChanged(value) {
        this._onFocusChanged = value;
    }

    constructor(value?: any, onChanged: any = null, label: any = '', placeholder: any = null, helper: any = null, error: any = null, leading: any = null, size: any = 'large', trailing: any = null, props?: any) {
        super();
        if (value !== undefined) this.value = value;
        if (onChanged !== undefined) this.onChanged = onChanged;
        if (label !== undefined) this.label = label;
        if (placeholder !== undefined) this.placeholder = placeholder;
        if (helper !== undefined) this.helper = helper;
        if (error !== undefined) this.error = error;
        if (leading !== undefined) this.leading = leading;
        if (size !== undefined) this.size = size;
        if (trailing !== undefined) this.trailing = trailing;
        if (this.size === undefined) this.size = 'small';
        if (size === 'small') throw $eq.exceptions.create(['System.ArgumentOutOfRangeException', 'System.ArgumentException', 'System.SystemException', 'System.Exception'], 'TextInput has no Small size — text + padding can\'t fit 32dp (spec B9).', { paramName: 'size' });
        this.value = value;
        this.onChanged = onChanged;
        this.label = label;
        this.placeholder = placeholder;
        this.helper = helper;
        this.error = error;
        this.leading = leading;
        this.trailing = trailing;
        this.size = size;
        if (props && typeof props === 'object') Object.assign(this, props);
    }

    build(context: BuildContext) {
        let theme = context.theme;
        let slot = theme.input;
        let height = ControlMetrics.height(context, this.size, (slot?.controls ?? null));
        let hasError = !!this.error;
        let borderColor = hasError ? theme.colors('destructive').base : this._focused ? theme.colors('primary').base : slot?.borderColor ?? theme.borderStrong;
        let restBorder = slot?.borderWidth ?? 1;
        let focusBorder = slot?.focusBorderWidth ?? 2;
        let borderWidth = this._focused ? focusBorder : restBorder;
        let restPadding = slot?.paddingX ?? 14;
        let paddingX = this._focused ? Math.fround(restPadding - Math.fround(focusBorder - restBorder)) : restPadding;
        let caption = hasError ? this.error! : this.helper ?? '';
        let row = new Row(slot?.gap ?? 10, 'start', 'center', false, null, null, { height: SizeValue.fill, cross: 'center' });
        let leading: any; 
        if ((leading = this.leading) != null) {
            row.add(new Icon(IconGlyph.fromIcons(leading), slot?.iconSize ?? 20, theme.textMuted));
        }
        row.add(new Flexible(new TextEntry(this.value, this.onChanged == null ? null : (value: string) => {
            this.value = value;
            this.onChanged?.(value);
        }, { placeholder: this.placeholder, label: this.label.length > 0 ? this.label : null, description: caption.length > 0 ? caption : null, invalid: hasError, disabled: this.disabled, obscure: this.obscure, autofocus: this.autofocus, onFocusChanged: (focused: boolean) => {
            this.setState(() => this._focused = focused);
            this.onFocusChanged?.(focused);
        } }), 1));
        let trailing: any; 
        if ((trailing = this.trailing) != null) {
            row.add(trailing);
        }
        let container = new Box(new BoxStyle({ width: SizeValue.fill, height: height, background: this.disabled ? slot?.disabledFill ?? theme.surfaceSubtle : slot?.fill ?? theme.surface, cornerRadius: new CornerRadii(slot?.radius ?? theme.shape('medium')), borderWidth: borderWidth, borderColor: borderColor, padding: EdgeInsets.symmetric(paddingX, 0) }), row);
        let top = new Column(slot?.labelGap ?? 6, 'start', 'stretch', false, null, null, { width: SizeValue.fill });
        if (this.label.length > 0) top.add(new Text(this.label, slot?.labelRole ?? 'label', theme.textSecondary));
        top.add(container);
        let captionColor = hasError ? theme.colors('destructive').base : theme.textMuted;
        let column = new Column(slot?.captionGap ?? 5, 'start', 'stretch', false, null, null, { width: SizeValue.fill });
        column.add(top);
        column.add(new Text(caption, slot?.captionRole ?? 'caption', captionColor, 1));
        return column;
    }

    adoptConfig(next: UiComponent) {
        let fresh: any; 
        if (!((next instanceof TextInput && (fresh = next, true)))) return;
        this.value = fresh.value;
        this.onChanged = fresh.onChanged;
        this.label = fresh.label;
        this.placeholder = fresh.placeholder;
        this.helper = fresh.helper;
        this.error = fresh.error;
        this.leading = fresh.leading;
        this.trailing = fresh.trailing;
        this.size = fresh.size;
        this._disabled = fresh.disabled;
        this._autofocus = fresh.autofocus;
        this._obscure = fresh.obscure;
        this._onFocusChanged = fresh.onFocusChanged;
    }
}

