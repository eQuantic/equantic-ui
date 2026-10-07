import { FieldRule } from "../runtime-exports";

export class FormField {
    constructor(name: string, initial: string = '', rules: FieldRule[] | null = null, relevantWhen: (() => boolean) | null = null) {
        this._rules = null!;
        this._relevantWhen = null;
        this.name = null!;
        this.initial = null!;
        this.value = null!;
        this.touched = false;
        this.error = null;
        this.name = name;
        this.initial = initial;
        this.value = initial;
        this._rules = rules == null ? [] : [...rules];
        this._relevantWhen = relevantWhen;
        this.revalidate();
    }

    _rules!: FieldRule[];
    _relevantWhen!: (() => boolean) | null;
    name!: string;
    initial!: string;
    value!: string;
    touched!: boolean;
    error!: string | null;

    get dirty(): boolean {
        return this.value !== this.initial;
    }

    get relevant(): boolean {
        return this._relevantWhen == null || this._relevantWhen!();
    }

    get visibleError(): string | null {
        return this.touched && this.relevant ? this.error : null;
    }

    get rulesFor(): FieldRule[] {
        return this._rules;
    }

    addRule(rule: FieldRule) {
        this._rules.push(rule);
        this.revalidate();
    }

    set(value: string) {
        this.value = value;
        this.revalidate();
    }

    touch() {
        this.touched = true;
        this.revalidate();
    }

    reset() {
        this.value = this.initial;
        this.touched = false;
        this.revalidate();
    }

    accept() {
        this.initial = this.value;
        this.touched = false;
        this.revalidate();
    }

    fail(message: string) {
        this.error = message;
        this.touched = true;
    }

    reveal() {
        return this.touched = true;
    }

    revalidateNow() {
        return this.revalidate();
    }

    revalidate() {
        this.error = null;
        if (!this.relevant) return;
        for (const rule of this._rules) {
            if (rule.holds(this.value)) continue;
            this.error = rule.message;
            return;
        }
    }

    toString(): string {
        return 'eQuantic.UI.Primitives.FormField';
    }
}

