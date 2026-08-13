import { FieldRule } from "../runtime-exports";
export class FormField {
    constructor(name: string, initial: string = '', rules: FieldRule[] | null = null, props?: any) { this.name = name;this.initial = initial;this.value = initial;this._rules = rules == null ? [] : [...rules];this.revalidate(); if (props && typeof props === 'object') Object.assign(this, props); }
    _rules: FieldRule[];
    declare name: string;
    declare initial: string;
    declare value: string;
    touched: boolean = false;
    get dirty(): boolean { return this.value !== this.initial; }
    declare error: string | null;
    get visibleError(): string | null { return this.touched ? this.error : null; }
    get rulesFor(): FieldRule[] { return this._rules; }
    addRule(rule: FieldRule) { this._rules.push(rule);this.revalidate(); }
    set(value: string) { this.value = value;this.revalidate(); }
    touch() { this.touched = true;this.revalidate(); }
    reset() { this.value = this.initial;this.touched = false;this.revalidate(); }
    accept() { this.initial = this.value;this.touched = false;this.revalidate(); }
    fail(message: string) { this.error = message;this.touched = true; }
    reveal() { return this.touched = true; }
    revalidateNow() { return this.revalidate(); }
    revalidate() { this.error = null;for (const rule of this._rules) {if (rule.holds(this.value)) continue;this.error = rule.message;return;} }
}

