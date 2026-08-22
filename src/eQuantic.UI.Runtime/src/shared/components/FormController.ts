import { FieldError, FieldRule, FormField } from "../runtime-exports";

export class FormController {
    constructor(props?: any) {
        this._fields = [];  if (props && typeof props === 'object') Object.assign(this, props);
    }

    _fields: FormField[];

    get fields(): FormField[] {
        return this._fields;
    }

    submitting: boolean = false;
    declare submitError: string | null;

    get valid(): boolean {
        for (const entry of this._fields) if (entry.relevant && !(entry.error == null)) return false;
        return true;
    }

    get dirty(): boolean {
        for (const entry of this._fields) if (entry.dirty) return true;
        return false;
    }

    changed: (() => void) | null = null;

    add(name: string, initial: string = '', rules: FieldRule[] | null = null, relevantWhen: (() => boolean) | null = null) {
        let field = new FormField(name, initial, rules, relevantWhen);
        this._fields.push(field);
        return field;
    }

    field(name: string) {
        for (const field of this._fields) if (field.name === name) return field;
        return null;
    }

    set(name: string, value: string) {
        let target = this.field(name);
        if (target == null) return;
        target.set(value);
        for (const entry of this._fields) if (!(entry === target)) entry.revalidateNow();
        this.changed?.();
    }

    revalidate() {
        for (const field of this._fields) field.revalidateNow();
        this.changed?.();
    }

    touch(name: string) {
        this.field(name)?.touch();
        this.changed?.();
    }

    validate() {
        for (const field of this._fields) field.reveal();
        this.changed?.();
        return this.valid;
    }

    reset() {
        for (const field of this._fields) field.reset();
        this.submitError = null;
        this.changed?.();
    }

    accept() {
        for (const field of this._fields) field.accept();
        this.submitError = null;
        this.changed?.();
    }

    async submitAsync(submit: () => void) {
        if (this.submitting) return false;
        if (!this.validate()) return false;
        this.submitting = true;
        this.submitError = null;
        this.changed?.();
        try {
            await submit();
            return true;
        } catch (error: any) {
            this.submitError = error.message;
            return false;
        } finally {
            this.submitting = false;
            this.changed?.();
        }
    }

    applyServerErrors(errors: FieldError[], formError: string | null = null) {
        for (const error of errors) this.field(error.field)?.fail(error.message);
        this.submitError = formError;
        this.changed?.();
    }
}

