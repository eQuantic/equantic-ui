import { CodeLanguageRules } from "../runtime-exports";
import { CurlyBraceLanguage } from "./CurlyBraceLanguage";
export class CSharpLanguage extends CurlyBraceLanguage {
    constructor(props?: any) { super();  if (props && typeof props === 'object') Object.assign(this, props); }
    get name(): string { return 'C#'; }
    get hasVerbatimStrings(): boolean { return true; }
    get hasBracketAttributes(): boolean { return true; }
    rules: CodeLanguageRules = new CodeLanguageRules('//', ['/*', '*/']);
    keywords: Set<string> = new Set(['abstract', 'as', 'async', 'await', 'base', 'break', 'case', 'catch', 'checked', 'class', 'const', 'continue', 'default', 'delegate', 'do', 'else', 'enum', 'event', 'explicit', 'extern', 'file', 'finally', 'fixed', 'for', 'foreach', 'get', 'global', 'goto', 'if', 'implicit', 'in', 'init', 'interface', 'internal', 'is', 'lock', 'namespace', 'new', 'not', 'operator', 'out', 'override', 'params', 'partial', 'private', 'protected', 'public', 'readonly', 'record', 'ref', 'required', 'return', 'sealed', 'set', 'sizeof', 'stackalloc', 'static', 'struct', 'switch', 'this', 'throw', 'try', 'typeof', 'unchecked', 'unsafe', 'using', 'value', 'virtual', 'volatile', 'when', 'where', 'while', 'with', 'yield']);
    typeWords: Set<string> = new Set(['bool', 'byte', 'char', 'decimal', 'double', 'dynamic', 'float', 'int', 'long', 'nint', 'nuint', 'object', 'sbyte', 'short', 'string', 'uint', 'ulong', 'ushort', 'var', 'void']);
    constantWords: Set<string> = new Set(['true', 'false', 'null', 'default']);
}

