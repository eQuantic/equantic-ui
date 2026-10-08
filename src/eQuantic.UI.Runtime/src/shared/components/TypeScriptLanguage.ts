import { $eq, CodeLanguageRules } from "../runtime-exports";
import { CurlyBraceLanguage } from "./CurlyBraceLanguage";

export class TypeScriptLanguage extends CurlyBraceLanguage {
    constructor() {
        const $$$rules = (() => {
        const $o = new CodeLanguageRules();
        $o.lineComment = '//';
        $o.blockComment = ['/*', '*/'];
        $o.indentWidth = 2;
        return $o;
    })();
        const $$$reservedWords = $eq.collections.hashSetOf(['abstract', 'any', 'as', 'async', 'await', 'break', 'case', 'catch', 'class', 'const', 'constructor', 'continue', 'debugger', 'declare', 'default', 'delete', 'do', 'else', 'enum', 'export', 'extends', 'finally', 'for', 'from', 'function', 'get', 'if', 'implements', 'import', 'in', 'infer', 'instanceof', 'interface', 'is', 'keyof', 'let', 'namespace', 'new', 'of', 'private', 'protected', 'public', 'readonly', 'return', 'satisfies', 'set', 'static', 'super', 'switch', 'this', 'throw', 'try', 'type', 'typeof', 'var', 'while', 'yield']);
        const $$$typeWords = $eq.collections.hashSetOf(['bigint', 'boolean', 'never', 'number', 'object', 'string', 'symbol', 'unknown', 'void']);
        const $$$constantWords = $eq.collections.hashSetOf(['true', 'false', 'null', 'undefined', 'NaN', 'Infinity']);
        super();
        this.$rules = $$$rules;
        this.$reservedWords = $$$reservedWords;
        this.$typeWords = $$$typeWords;
        this.$constantWords = $$$constantWords;
    }

    declare $rules: CodeLanguageRules;
    $reservedWords!: Set<string>;
    $typeWords!: Set<string>;
    $constantWords!: Set<string>;

    get name(): string {
        return 'TypeScript';
    }

    get hasTemplateStrings(): boolean {
        return true;
    }

    get hasAtDecorators(): boolean {
        return true;
    }

    get rules(): CodeLanguageRules {
        return this.$rules;
    }

    set rules(value: CodeLanguageRules) {
        this.$rules = value;
    }

    get reservedWords(): Set<string> {
        return this.$reservedWords;
    }

    set reservedWords(value: Set<string>) {
        this.$reservedWords = value;
    }

    get typeWords(): Set<string> {
        return this.$typeWords;
    }

    set typeWords(value: Set<string>) {
        this.$typeWords = value;
    }

    get constantWords(): Set<string> {
        return this.$constantWords;
    }

    set constantWords(value: Set<string>) {
        this.$constantWords = value;
    }

    toJSON() {
        return $eq.json(this);
    }

    toString(): string {
        return 'eQuantic.UI.Code.TypeScriptLanguage';
    }
}

