import { $SizeVariantValue, $TextEntryKindValue, Box, BuildContext, FormController, StatelessComponent, TextInput } from "../runtime-exports";

export class FormInput extends StatelessComponent {
    static $typeId = 'eQuantic.UI.Components.FormInput';
    declare form: FormController;
    declare name: string;
    declare label: string;
    declare placeholder: any;
    declare helper: any;
    declare leading: any;
    declare size: $SizeVariantValue;
    declare kind: $TextEntryKindValue;
    declare prefix: any;
    declare suffix: any;
    declare style: any;
    declare obscure: boolean;
    declare autofocus: boolean;
    declare disabled: boolean;

    constructor(form?: any, name?: any, label: any = '', placeholder: any = null, helper: any = null, leading: any = null, size: any = 'large', kind: any = 'text', prefix: any = null, suffix: any = null, style: any = null, props?: any) {
        super();
        if (form !== undefined) this.form = form;
        if (name !== undefined) this.name = name;
        if (label !== undefined) this.label = label;
        if (placeholder !== undefined) this.placeholder = placeholder;
        if (helper !== undefined) this.helper = helper;
        if (leading !== undefined) this.leading = leading;
        if (size !== undefined) this.size = size;
        if (kind !== undefined) this.kind = kind;
        if (prefix !== undefined) this.prefix = prefix;
        if (suffix !== undefined) this.suffix = suffix;
        if (style !== undefined) this.style = style;
        if (this.size === undefined) this.size = 'small';
        if (this.kind === undefined) this.kind = 'text';
        if (this.obscure === undefined) this.obscure = false;
        if (this.autofocus === undefined) this.autofocus = false;
        if (this.disabled === undefined) this.disabled = false;
        this.form = form;
        this.name = name;
        this.label = label;
        this.placeholder = placeholder;
        this.helper = helper;
        this.leading = leading;
        this.size = size;
        this.kind = kind;
        this.prefix = prefix;
        this.suffix = suffix;
        this.style = style;
        if (props && typeof props === 'object') Object.assign(this, props);
    }

    build(_context: BuildContext) {
        let field = this.form.field(this.name);
        if (field == null) return new Box();
        return new TextInput(field.value, (value: string) => this.form.set(this.name, value), this.label, this.placeholder, this.helper, field.visibleError, this.leading, this.size, null, this.kind, this.prefix, this.suffix, this.style, { obscure: this.obscure, autofocus: this.autofocus, disabled: this.disabled || this.form.submitting, onFocusChanged: (focused: boolean) => {
            if (!focused) this.form.touch(this.name);
        } });
    }
}

