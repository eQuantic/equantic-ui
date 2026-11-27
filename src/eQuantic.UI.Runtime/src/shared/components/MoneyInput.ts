import { $eq, $SizeVariantValue, BuildContext, Currency, SdkStrings, StatefulComponent, TextInput, UiComponent } from "../runtime-exports";

export class MoneyInput extends StatefulComponent {
    static $typeId = 'eQuantic.UI.Components.MoneyInput';
    _typing: any = null;
    _disabled: boolean = false;
    declare amount: any;
    declare onChanged: ((long: bigint | null) => void) | null;
    declare label: string;
    declare currency: Currency;
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

    constructor(amount?: any, onChanged?: any, label?: any, currency?: any, min: any = null, max: any = null, placeholder: any = null, helper: any = null, error: any = null, size: any = 'large', props?: any) {
        super();
        if (amount !== undefined) this.amount = amount;
        if (onChanged !== undefined) this.onChanged = onChanged;
        if (label !== undefined) this.label = label;
        if (currency !== undefined) this.currency = currency;
        if (min !== undefined) this.min = min;
        if (max !== undefined) this.max = max;
        if (placeholder !== undefined) this.placeholder = placeholder;
        if (helper !== undefined) this.helper = helper;
        if (error !== undefined) this.error = error;
        if (size !== undefined) this.size = size;
        if (this.size === undefined) this.size = 'small';
        if (size === 'small') throw $eq.exceptions.create(['System.ArgumentOutOfRangeException', 'System.ArgumentException', 'System.SystemException', 'System.Exception'], 'A text field has no Small size: text and padding cannot fit 32dp (spec B9).', { paramName: 'size' });
        this.amount = amount;
        this.onChanged = onChanged;
        this.label = label;
        this.currency = currency;
        this.min = min;
        this.max = max;
        this.placeholder = placeholder;
        this.helper = helper;
        this.error = error;
        this.size = size;
        if (props && typeof props === 'object') Object.assign(this, props);
    }

    build(_context: BuildContext) {
        let amount: any; 
        let shown = this._typing ?? ((amount = this.amount) != null ? this.write(amount) : '');
        let leads = (($0) => ($0 === 0 || $0 === 2))($eq.text.numberFormat(false).currencyPositivePattern);
        let typed: any; 
        let error = this.error ?? ((typed = this._typing) != null ? this.complaint(typed) : null);
        return new TextInput(shown, this.onChanged == null ? null : $eq.delegates.bound(this, this.type), this.label, this.placeholder, this.helper, error, null, this.size, null, 'decimal', leads ? this.currency.symbol : null, leads ? null : this.currency.symbol, null, { disabled: this.disabled, onFocusChanged: $eq.delegates.bound(this, this.focus).bind(this) });
    }

    adoptConfig(next: UiComponent) {
        let fresh: any; 
        if (!((next instanceof MoneyInput && (fresh = next, true)))) return;
        let buffer: any; 
        if ((buffer = this._typing) != null && fresh.amount !== this.amount && !MoneyInput.says(buffer, fresh.amount, fresh.currency)) this._typing = null;
        this.amount = fresh.amount;
        this.onChanged = fresh.onChanged;
        this.label = fresh.label;
        this.currency = fresh.currency;
        this.min = fresh.min;
        this.max = fresh.max;
        this.placeholder = fresh.placeholder;
        this.helper = fresh.helper;
        this.error = fresh.error;
        this.size = fresh.size;
        this._disabled = fresh.disabled;
    }

    type(text: string) {
        if ($eq.text.trim(text).length === 0) this.onChanged?.(null); else { let amount: any; if ((amount = MoneyInput.read(text, this.currency)) != null && this.inRange(amount)) this.onChanged?.(amount); }
        this.setState(() => this._typing = text);
    }

    focus(focused: boolean) {
        let typed: any; 
        if (!focused && (typed = this._typing) != null && this.complaint(typed) == null) this.setState(() => this._typing = null);
    }

    complaint(typed: string) {
        if ($eq.text.trim(typed).length === 0) return null;
        let amount: any; 
        if (!((amount = MoneyInput.read(typed, this.currency)) != null)) return SdkStrings.amountExpected(this.write(1250n));
        if (this.inRange(amount)) return null;
        let min: any; let max: any; 
        if ((min = this.min) != null && (max = this.max) != null) return SdkStrings.valueBetween(this.currency.format(min), this.currency.format(max));
        let least: any; 
        return (least = this.min) != null ? SdkStrings.valueAtLeast(this.currency.format(least)) : SdkStrings.valueAtMost(this.currency.format($eq.nullable.value(this.max!)));
    }

    inRange(amount: bigint) {
        let min: any; let max: any; 
        return (!((min = this.min) != null) || amount >= min) && (!((max = this.max) != null) || amount <= max);
    }

    write(amount: bigint) {
        return $eq.text.format(this.currency.toMajor(amount), 'N' + $eq.text.format(this.currency.minorDigits, null, undefined, undefined, 'int32'));
    }

    static says(typed: string, amount: any, currency: Currency) {
        let read: any; 
        return $eq.text.trim(typed).length === 0 ? amount == null : (read = MoneyInput.read(typed, currency)) != null && read === amount;
    }

    static read(typed: string, currency: Currency) {
        let text = $eq.text.trim(typed);
        let Plain = 1 | 2 | 4 | 32;
        let value: any; let read: any; 
        if (((value = $eq.num.decTryParse(text, Plain)) !== undefined || ((value = $eq.num.dec(0)), false)) && (read = currency.toMinor(value)) != null) return read;
        let point = $eq.text.numberFormat(false).numberDecimalSeparator;
        let other = point === ',' ? '.' : point === '.' ? ',' : null;
        let swapped: any; let readSwapped: any; 
        if (!(other == null) && ((swapped = $eq.num.decTryParse($eq.text.replace(text, other, point, 'ordinal'), Plain)) !== undefined || ((swapped = $eq.num.dec(0)), false)) && (readSwapped = currency.toMinor(swapped)) != null) return readSwapped;
        let grouped: any; 
        return ((grouped = $eq.num.decTryParse(text, 111)) !== undefined || ((grouped = $eq.num.dec(0)), false)) ? currency.toMinor(grouped) : null;
    }
}

