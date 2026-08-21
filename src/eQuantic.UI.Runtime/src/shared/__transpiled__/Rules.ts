import { FieldRule, FormField } from "@equantic/runtime";
export class Rules {
    static required(message: string = 'This field is required.') {
        return new FieldRule(message, (value: string) => value.trim().length > 0);
    }
    static minLength(length: number, message: string | null = null) {
        return new FieldRule(message ?? `Use at least ${length} characters.`, (value: string) => value.length === 0 || value.length >= length);
    }
    static maxLength(length: number, message: string | null = null) {
        return new FieldRule(message ?? `Use at most ${length} characters.`, (value: string) => value.length <= length);
    }
    static email(message: string = 'Enter a valid email address.') {
        return new FieldRule(message, (value: string) => {
            if (value.length === 0) return true;
            let at = value.indexOf('@');
            if (at <= 0 || at !== value.lastIndexOf('@')) return false;
            let domain = value.slice((at + 1));
            let dot = domain.indexOf('.');
            return dot > 0 && dot < domain.length - 1 && !domain.includes(' ') && !value.slice(0, at).includes(' ');
        });
    }
    static range(min: number, max: number, message: string | null = null) {
        let number: any; return new FieldRule(message ?? `Enter a number between ${min} and ${max}.`, (value: string) => {
            if (value.length === 0) return true;
            return (number = parseFloat(value), !isNaN(number)) && number >= min && number <= max;
        });
    }
    static matches(other: FormField, message: string | null = null) {
        return new FieldRule(message ?? 'The two values do not match.', (value: string) => value === other.value);
    }
    static custom(message: string, holds: (value: string) => boolean) {
        return new FieldRule(message, holds);
    }
    static when(condition: () => boolean, rule: FieldRule) {
        return new FieldRule(rule.message, (value: string) => !condition() || rule.holds(value));
    }
}

