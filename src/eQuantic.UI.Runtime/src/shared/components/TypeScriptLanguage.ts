import { CodeLanguageRules } from "../runtime-exports";
import { CurlyBraceLanguage } from "./CurlyBraceLanguage";

export class TypeScriptLanguage extends CurlyBraceLanguage {
    constructor() {
        const $$rules = (($o: any, $1: any, $2: any, $3: any) => {
        $o.lineComment = $1;
        $o.blockComment = $2;
        $o.indentWidth = $3;
        return $o;
    })(new CodeLanguageRules(), '//', ['/*', '*/'], 2);
        const $$keywords = new Set(['abstract', 'any', 'as', 'async', 'await', 'break', 'case', 'catch', 'class', 'const', 'constructor', 'continue', 'debugger', 'declare', 'default', 'delete', 'do', 'else', 'enum', 'export', 'extends', 'finally', 'for', 'from', 'function', 'get', 'if', 'implements', 'import', 'in', 'infer', 'instanceof', 'interface', 'is', 'keyof', 'let', 'namespace', 'new', 'of', 'private', 'protected', 'public', 'readonly', 'return', 'satisfies', 'set', 'static', 'super', 'switch', 'this', 'throw', 'try', 'type', 'typeof', 'var', 'while', 'yield']);
        const $$typeWords = new Set(['bigint', 'boolean', 'never', 'number', 'object', 'string', 'symbol', 'unknown', 'void']);
        const $$constantWords = new Set(['true', 'false', 'null', 'undefined', 'NaN', 'Infinity']);
        super();
        this.rules = $$rules;
        this.keywords = $$keywords;
        this.typeWords = $$typeWords;
        this.constantWords = $$constantWords;
    }

    get name(): string {
        return 'TypeScript';
    }

    get hasTemplateStrings(): boolean {
        return true;
    }

    get hasAtDecorators(): boolean {
        return true;
    }

    declare rules: CodeLanguageRules;
    declare keywords: Set<string>;
    declare typeWords: Set<string>;
    declare constantWords: Set<string>;
}

