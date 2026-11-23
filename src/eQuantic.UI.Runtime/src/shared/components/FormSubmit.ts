import { $SizeVariantValue, $VariantValue, BuildContext, Button, FormController, StatelessComponent } from "../runtime-exports";

export class FormSubmit extends StatelessComponent {
    static $typeId = 'eQuantic.UI.Components.FormSubmit';
    declare form: FormController;
    declare label: string;
    declare onSubmit: () => void;
    declare variant: $VariantValue;
    declare size: $SizeVariantValue;
    declare wide: boolean;
    declare style: any;

    constructor(form?: any, label?: any, onSubmit?: any, variant: any = 'primary', size: any = 'medium', style: any = null, props?: any) {
        super();
        if (form !== undefined) this.form = form;
        if (label !== undefined) this.label = label;
        if (onSubmit !== undefined) this.onSubmit = onSubmit;
        if (variant !== undefined) this.variant = variant;
        if (size !== undefined) this.size = size;
        if (style !== undefined) this.style = style;
        if (this.variant === undefined) this.variant = 'primary';
        if (this.size === undefined) this.size = 'small';
        if (this.wide === undefined) this.wide = false;
        this.form = form;
        this.label = label;
        this.onSubmit = onSubmit;
        this.variant = variant;
        this.size = size;
        this.style = style;
        if (props && typeof props === 'object') Object.assign(this, props);
    }

    build(_context: BuildContext) {
        return new Button(this.label, this.variant, this.size, null, this.style, { loading: this.form.submitting, expand: this.wide, onPressed: () => this.form.submitAsync(this.onSubmit) });
    }
}

