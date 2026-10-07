import { $eq, CodeLanguageRules } from "../runtime-exports";
import { CurlyBraceLanguage } from "./CurlyBraceLanguage";

export class CSharpLanguage extends CurlyBraceLanguage {
    constructor() {
        const $$$rules = (() => {
        const $o = new CodeLanguageRules();
        $o.lineComment = '//';
        $o.blockComment = ['/*', '*/'];
        return $o;
    })();
        const $$$keywords = $eq.collections.hashSetOf(['abstract', 'as', 'async', 'await', 'base', 'break', 'case', 'catch', 'checked', 'class', 'const', 'continue', 'default', 'delegate', 'do', 'else', 'enum', 'event', 'explicit', 'extern', 'file', 'finally', 'fixed', 'for', 'foreach', 'get', 'global', 'goto', 'if', 'implicit', 'in', 'init', 'interface', 'internal', 'is', 'lock', 'namespace', 'new', 'not', 'operator', 'out', 'override', 'params', 'partial', 'private', 'protected', 'public', 'readonly', 'record', 'ref', 'required', 'return', 'sealed', 'set', 'sizeof', 'stackalloc', 'static', 'struct', 'switch', 'this', 'throw', 'try', 'typeof', 'unchecked', 'unsafe', 'using', 'value', 'virtual', 'volatile', 'when', 'where', 'while', 'with', 'yield']);
        const $$$typeWords = $eq.collections.hashSetOf(['bool', 'byte', 'char', 'decimal', 'double', 'dynamic', 'float', 'int', 'long', 'nint', 'nuint', 'object', 'sbyte', 'short', 'string', 'uint', 'ulong', 'ushort', 'var', 'void']);
        const $$$constantWords = $eq.collections.hashSetOf(['true', 'false', 'null', 'default']);
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
        return 'C#';
    }

    get hasVerbatimStrings(): boolean {
        return true;
    }

    get hasRawStrings(): boolean {
        return true;
    }

    get hasBracketAttributes(): boolean {
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
        return 'eQuantic.UI.Code.CSharpLanguage';
    }
}

