import { $eq, $SizeVariantValue, BuildContext, SdkStrings, StatefulComponent, TextInput, UiComponent } from "../runtime-exports";

export class NumberInput extends StatefulComponent {
    static $typeId = 'eQuantic.UI.Components.NumberInput';
    _typing: any = null;
    _focused: boolean = false;
    _disabled: boolean = false;
    declare value: any;
    declare onChanged: ((int: number | null) => void) | null;
    declare label: string;
    declare unit: string;
    declare min: any;
    declare max: any;
    declare placeholder: any;
    declare helper: any;
    declare error: any;
    declare size: $SizeVariantValue;

    get disabled() {
        return this._disabled;
    }

    set disabled(value) {
        this._disabled = value;
    }

    constructor(value?: any, onChanged?: any, label?: any, unit: any = '', min: any = null, max: any = null, placeholder: any = null, helper: any = null, error: any = null, size: any = 'large', props?: any) {
        super();
        if (value !== undefined) this.value = value;
        if (onChanged !== undefined) this.onChanged = onChanged;
        if (label !== undefined) this.label = label;
        if (unit !== undefined) this.unit = unit;
        if (min !== undefined) this.min = min;
        if (max !== undefined) this.max = max;
        if (placeholder !== undefined) this.placeholder = placeholder;
        if (helper !== undefined) this.helper = helper;
        if (error !== undefined) this.error = error;
        if (size !== undefined) this.size = size;
        if (this.size === undefined) this.size = 'small';
        this.value = value;
        this.onChanged = onChanged;
        this.label = label;
        this.unit = unit;
        this.min = min;
        this.max = max;
        this.placeholder = placeholder;
        this.helper = helper;
        this.error = error;
        this.size = size;
        if (props && typeof props === 'object') Object.assign(this, props);
    }

    build(_context: BuildContext) {
        let value: any; 
        let shown = this._typing ?? ((value = this.value) != null ? NumberInput.write(value, this._focused) : '');
        let typed: any; 
        let error = this.error ?? ((typed = this._typing) != null ? this.complaint(typed) : null);
        return new TextInput(shown, this.onChanged == null ? null : $eq.delegates.bound(this, this.type), this.label, this.placeholder, this.helper, error, null, this.size, null, 'number', null, this.unit.length > 0 ? this.unit : null, { disabled: this.disabled, onFocusChanged: $eq.delegates.bound(this, this.focus).bind(this) });
    }

    adoptConfig(next: UiComponent) {
        let fresh: any; 
        if (!((next instanceof NumberInput && (fresh = next, true)))) return;
        let buffer: any; 
        if ((buffer = this._typing) != null && fresh.value !== this.value && !NumberInput.says(buffer, fresh.value)) this._typing = null;
        this.value = fresh.value;
        this.onChanged = fresh.onChanged;
        this.label = fresh.label;
        this.unit = fresh.unit;
        this.min = fresh.min;
        this.max = fresh.max;
        this.placeholder = fresh.placeholder;
        this.helper = fresh.helper;
        this.error = fresh.error;
        this.size = fresh.size;
        this._disabled = fresh.disabled;
    }

    type(text: string) {
        if ($eq.text.trim(text).length === 0) this.onChanged?.(null); else { let number: any; if ((number = NumberInput.read(text)) != null && this.inRange(number)) this.onChanged?.(number); }
        this.setState(() => this._typing = text);
    }

    focus(focused: boolean) {
        return this.setState(() => {
            this._focused = focused;
            let typed: any; 
            if (!focused && (typed = this._typing) != null && this.complaint(typed) == null) this._typing = null;
        });
    }

    complaint(typed: string) {
        if ($eq.text.trim(typed).length === 0) return null;
        let number: any; 
        if (!((number = NumberInput.read(typed)) != null)) return SdkStrings.wholeNumberExpected(NumberInput.write(12, true));
        if (this.inRange(number)) return null;
        let min: any; let max: any; 
        if ((min = this.min) != null && (max = this.max) != null) return SdkStrings.valueBetween(this.withUnit(min), this.withUnit(max));
        let least: any; 
        return (least = this.min) != null ? SdkStrings.valueAtLeast(this.withUnit(least)) : SdkStrings.valueAtMost(this.withUnit($eq.nullable.value(this.max!)));
    }

    inRange(number: number) {
        let min: any; let max: any; 
        return (!((min = this.min) != null) || number >= min) && (!((max = this.max) != null) || number <= max);
    }

    static write(number: number, plain: boolean) {
        return $eq.text.format(number, plain ? 'D' : 'N0', undefined, undefined, 'int32');
    }

    withUnit(number: number) {
        return this.unit.length > 0 ? `${$eq.text.format(number, 'N0', undefined, undefined, 'int32')} ${this.unit}` : `${$eq.text.format(number, 'N0', undefined, undefined, 'int32')}`;
    }

    static says(typed: string, number: any) {
        let read: any; 
        return $eq.text.trim(typed).length === 0 ? number == null : (read = NumberInput.read(typed)) != null && read === number;
    }

    static read(typed: string) {
        let number: any; 
        return ((number = $eq.num.intTryParse($eq.text.trim(typed), 'int', 7)) !== undefined || ((number = 0), false)) ? number : null;
    }
}

