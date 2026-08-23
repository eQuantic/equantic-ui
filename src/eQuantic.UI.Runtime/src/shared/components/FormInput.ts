import { Box, BuildContext, FormController, SizeVariantValue, StatelessComponent, TextInput } from "../runtime-exports";

export class FormInput extends StatelessComponent {
    declare form: FormController;
    declare name: string;
    declare label: string;
    declare placeholder: any;
    declare helper: any;
    declare leading: any;
    declare size: SizeVariantValue;
    declare obscure: boolean;
    declare autofocus: boolean;
    declare disabled: boolean;

    constructor(form?: any, name?: any, label: any = '', placeholder: any = null, helper: any = null, leading: any = null, size: any = 'large', props?: any) {
        super();
        if (form !== undefined) this.form = form;
        if (name !== undefined) this.name = name;
        if (label !== undefined) this.label = label;
        if (placeholder !== undefined) this.placeholder = placeholder;
        if (helper !== undefined) this.helper = helper;
        if (leading !== undefined) this.leading = leading;
        if (size !== undefined) this.size = size;
        if (this.size === undefined) this.size = 'small';
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
        if (props && typeof props === 'object') Object.assign(this, props);
    }

    build(_context: BuildContext) {
        let field = this.form.field(this.name);
        if (field == null) return new Box();
        return new TextInput(field.value, (value: string) => this.form.set(this.name, value), this.label, this.placeholder, this.helper, field.visibleError, this.leading, this.size, null, { obscure: this.obscure, autofocus: this.autofocus, disabled: this.disabled || this.form.submitting, onFocusChanged: (focused: boolean) => {
            if (!focused) this.form.touch(this.name);
        } });
    }
}

