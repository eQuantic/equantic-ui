import { CodeLanguageRules } from "../runtime-exports";
import { CurlyBraceLanguage } from "./CurlyBraceLanguage";
export class TypeScriptLanguage extends CurlyBraceLanguage {
    constructor(props?: any) {
        super();  if (props && typeof props === 'object') Object.assign(this, props);
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

    rules: CodeLanguageRules = new CodeLanguageRules('//', ['/*', '*/'], [['(', ')'], ['[', ']'], ['{', '}']], ['"', '\''], ['{', '(', '['], ['}', ')', ']'], 2);
    keywords: Set<string> = new Set(['abstract', 'any', 'as', 'async', 'await', 'break', 'case', 'catch', 'class', 'const', 'constructor', 'continue', 'debugger', 'declare', 'default', 'delete', 'do', 'else', 'enum', 'export', 'extends', 'finally', 'for', 'from', 'function', 'get', 'if', 'implements', 'import', 'in', 'infer', 'instanceof', 'interface', 'is', 'keyof', 'let', 'namespace', 'new', 'of', 'private', 'protected', 'public', 'readonly', 'return', 'satisfies', 'set', 'static', 'super', 'switch', 'this', 'throw', 'try', 'type', 'typeof', 'var', 'while', 'yield']);
    typeWords: Set<string> = new Set(['bigint', 'boolean', 'never', 'number', 'object', 'string', 'symbol', 'unknown', 'void']);
    constantWords: Set<string> = new Set(['true', 'false', 'null', 'undefined', 'NaN', 'Infinity']);
}

