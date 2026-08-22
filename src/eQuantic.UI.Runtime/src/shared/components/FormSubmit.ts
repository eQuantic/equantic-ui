import { BuildContext, Button, FormController, SizeVariantValue, StatelessComponent, VariantValue } from "../runtime-exports";

export class FormSubmit extends StatelessComponent {
    declare form: FormController;
    declare label: string;
    declare onSubmit: () => void;
    declare variant: VariantValue;
    declare size: SizeVariantValue;
    declare wide: boolean;

    constructor(form?: any, label?: any, onSubmit?: any, variant: any = 'primary', size: any = 'medium', props?: any) {
        super();
        if (form !== undefined) this.form = form;
        if (label !== undefined) this.label = label;
        if (onSubmit !== undefined) this.onSubmit = onSubmit;
        if (variant !== undefined) this.variant = variant;
        if (size !== undefined) this.size = size;
        if (this.variant === undefined) this.variant = 'primary';
        if (this.size === undefined) this.size = 'small';
        if (this.wide === undefined) this.wide = false;
        this.form = form;
        this.label = label;
        this.onSubmit = onSubmit;
        this.variant = variant;
        this.size = size;
        if (props && typeof props === 'object') Object.assign(this, props);
    }

    build(_context: BuildContext) {
        return new Button(this.label, this.variant, this.size, null, { loading: this.form.submitting, expand: this.wide, onPressed: () => this.form.submitAsync(this.onSubmit) });
    }
}

