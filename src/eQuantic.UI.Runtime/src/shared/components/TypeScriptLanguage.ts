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
        const $$$keywords = $eq.collections.hashSetOf(['abstract', 'any', 'as', 'async', 'await', 'break', 'case', 'catch', 'class', 'const', 'constructor', 'continue', 'debugger', 'declare', 'default', 'delete', 'do', 'else', 'enum', 'export', 'extends', 'finally', 'for', 'from', 'function', 'get', 'if', 'implements', 'import', 'in', 'infer', 'instanceof', 'interface', 'is', 'keyof', 'let', 'namespace', 'new', 'of', 'private', 'protected', 'public', 'readonly', 'return', 'satisfies', 'set', 'static', 'super', 'switch', 'this', 'throw', 'try', 'type', 'typeof', 'var', 'while', 'yield']);
        const $$$typeWords = $eq.collections.hashSetOf(['bigint', 'boolean', 'never', 'number', 'object', 'string', 'symbol', 'unknown', 'void']);
        const $$$constantWords = $eq.collections.hashSetOf(['true', 'false', 'null', 'undefined', 'NaN', 'Infinity']);
        super();
        this.$rules = $$$rules;
        this.$keywords = $$$keywords;
        this.$typeWords = $$$typeWords;
        this.$constantWords = $$$constantWords;
    }

    declare $rules: CodeLanguageRules;
    $keywords!: Set<string>;
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

    get keywords(): Set<string> {
        return this.$keywords;
    }

    set keywords(value: Set<string>) {
        this.$keywords = value;
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

