import { $CodeTokenKindValue, $eq, CodeDocument, CodeLanguageRules, CodeToken } from "../runtime-exports";

export abstract class CurlyBraceLanguage {
    static $interfaces = ['eQuantic.UI.Code.ICodeLanguage'];

    constructor() {
        this.$rules = CodeLanguageRules.default;
        this._keywords = null;
    }

    $rules!: CodeLanguageRules;
    _keywords!: string[] | null;
    static stateNormal: number = 0;
    static stateBlockComment: number = 1;
    static stateMultilineString: number = 2;
    static stateRawString: number = 16;
    abstract name: string;

    get rules(): CodeLanguageRules {
        return this.$rules;
    }

    set rules(value: CodeLanguageRules) {
        this.$rules = value;
    }

    abstract reservedWords: Set<string>;
    abstract typeWords: Set<string>;
    abstract constantWords: Set<string>;

    get keywords(): string[] {
        return this._keywords ?? (this._keywords = $eq.linq.toArray($eq.query.defer(($src) => [...new Set($src)], $eq.query.concat($eq.query.concat(this.reservedWords, this.typeWords), this.constantWords))));
    }

    get hasVerbatimStrings(): boolean {
        return false;
    }

    get hasTemplateStrings(): boolean {
        return false;
    }

    get hasRawStrings(): boolean {
        return false;
    }

    get hasBracketAttributes(): boolean {
        return false;
    }

    get hasAtDecorators(): boolean {
        return false;
    }

    static $slots: any = null;
    static $failure: any = null;

    static $init(): any {
        if (CurlyBraceLanguage.$slots === null) {
            if (CurlyBraceLanguage.$failure !== null) throw CurlyBraceLanguage.$failure;
            let $slots: any = CurlyBraceLanguage.$slots = {};
            try {
                $slots.punctuation = null;
                $slots.punctuation = $eq.collections.hashSetOf(['(', ')', '[', ']', '{', '}', ',', ';', '.', ':']);
            } catch ($error) {
                CurlyBraceLanguage.$slots = null;
                throw CurlyBraceLanguage.$failure = $eq.exceptions.typeInitialization('eQuantic.UI.Code.CurlyBraceLanguage', $error);
            }
        }
        return CurlyBraceLanguage.$slots;
    }

    static get punctuation(): Set<string> {
        return CurlyBraceLanguage.$init().punctuation;
    }

    static set punctuation(value: Set<string>) {
        CurlyBraceLanguage.$init().punctuation = value;
    }

    tokenize(line: string, state: number, into: CodeToken[]) {
        let i = 0;
        if (state === CurlyBraceLanguage.stateBlockComment) {
            let close = $eq.text.indexOf(line, '*/', 'ordinal');
            if (close < 0) {
                CurlyBraceLanguage.add(into, 0, line.length, 'comment');
                return CurlyBraceLanguage.stateBlockComment;
            }
            CurlyBraceLanguage.add(into, 0, close + 2 | 0, 'comment');
            i = close + 2 | 0;
        } else if (state >= CurlyBraceLanguage.stateRawString) {
            let quotes = state - CurlyBraceLanguage.stateRawString | 0;
            let end = CurlyBraceLanguage.closeRaw(line, 0, quotes);
            if (end < 0) {
                CurlyBraceLanguage.add(into, 0, line.length, 'string');
                return state;
            }
            CurlyBraceLanguage.add(into, 0, end, 'string');
            i = end;
        } else if (state === CurlyBraceLanguage.stateMultilineString) {
            let end = this.closeMultilineString(line);
            if (end < 0) {
                CurlyBraceLanguage.add(into, 0, line.length, 'string');
                return CurlyBraceLanguage.stateMultilineString;
            }
            CurlyBraceLanguage.add(into, 0, end, 'string');
            i = end;
        }
        while (i < line.length) {
            let c = line[i];
            if ($eq.text.isWhiteSpace(c)) {
                i = i + 1 | 0;
                continue;
            }
            if (c === '/' && (i + 1 | 0) < line.length) {
                if (line[i + 1 | 0] === '/') {
                    CurlyBraceLanguage.add(into, i, line.length - i | 0, 'comment');
                    return CurlyBraceLanguage.stateNormal;
                }
                if (line[i + 1 | 0] === '*') {
                    let close = $eq.text.indexOf(line, '*/', i + 2 | 0, 'ordinal');
                    if (close < 0) {
                        CurlyBraceLanguage.add(into, i, line.length - i | 0, 'comment');
                        return CurlyBraceLanguage.stateBlockComment;
                    }
                    CurlyBraceLanguage.add(into, i, (close + 2 | 0) - i | 0, 'comment');
                    i = close + 2 | 0;
                    continue;
                }
            }
            let prefixed: any; 
            if (this.hasVerbatimStrings && (c === '$' || c === '@') && (prefixed = CurlyBraceLanguage.prefixedString(line, i)) != null) {
                let quote = prefixed[0];
                if (this.hasRawStrings && !prefixed[1] && CurlyBraceLanguage.quotesAt(line, quote) >= 3) {
                    let quotes = CurlyBraceLanguage.quotesAt(line, quote);
                    let raw = CurlyBraceLanguage.closeRaw(line, quote + quotes | 0, quotes);
                    if (raw < 0) {
                        CurlyBraceLanguage.add(into, i, line.length - i | 0, 'string');
                        return CurlyBraceLanguage.stateRawString + quotes | 0;
                    }
                    CurlyBraceLanguage.add(into, i, raw - i | 0, 'string');
                    i = raw;
                    continue;
                }
                let end = prefixed[1] ? CurlyBraceLanguage.scanVerbatim(line, quote + 1 | 0) : CurlyBraceLanguage.scanQuoted(line, quote + 1 | 0, '"');
                if (end < 0) {
                    CurlyBraceLanguage.add(into, i, line.length - i | 0, 'string');
                    if (prefixed[1]) return CurlyBraceLanguage.stateMultilineString;
                    i = line.length;
                    continue;
                }
                CurlyBraceLanguage.add(into, i, end - i | 0, 'string');
                i = end;
                continue;
            }
            if (this.hasTemplateStrings && c === '`') {
                let end = CurlyBraceLanguage.scanQuoted(line, i + 1 | 0, '`');
                if (end < 0) {
                    CurlyBraceLanguage.add(into, i, line.length - i | 0, 'string');
                    return CurlyBraceLanguage.stateMultilineString;
                }
                CurlyBraceLanguage.add(into, i, end - i | 0, 'string');
                i = end;
                continue;
            }
            if (this.hasRawStrings && c === '"' && CurlyBraceLanguage.quotesAt(line, i) >= 3) {
                let quotes = CurlyBraceLanguage.quotesAt(line, i);
                let end = CurlyBraceLanguage.closeRaw(line, i + quotes | 0, quotes);
                if (end < 0) {
                    CurlyBraceLanguage.add(into, i, line.length - i | 0, 'string');
                    return CurlyBraceLanguage.stateRawString + quotes | 0;
                }
                CurlyBraceLanguage.add(into, i, end - i | 0, 'string');
                i = end;
                continue;
            }
            if (c === '"' || c === '\'') {
                let end = CurlyBraceLanguage.scanQuoted(line, i + 1 | 0, c);
                CurlyBraceLanguage.add(into, i, (end < 0 ? line.length : end) - i | 0, 'string');
                i = end < 0 ? line.length : end;
                continue;
            }
            if ((/^\p{Nd}$/u.test(c))) {
                let end = CurlyBraceLanguage.scanNumber(line, i);
                CurlyBraceLanguage.add(into, i, end - i | 0, 'number');
                i = end;
                continue;
            }
            if ((/^\p{L}$/u.test(c)) || c === '_' || this.hasAtDecorators && c === '@') {
                let start = i;
                if (line[i] === '@') i = i + 1 | 0;
                while (i < line.length && CodeDocument.isWordChar(line[i])) i = i + 1 | 0;
                let word = line.slice(start, i);
                CurlyBraceLanguage.add(into, start, i - start | 0, this.wordKind(word, line, i));
                continue;
            }
            if (this.hasBracketAttributes && c === '[' && CurlyBraceLanguage.isLineHead(line, i)) {
                let close = $eq.text.indexOfChar(line, ']', i);
                if (close > 0) {
                    CurlyBraceLanguage.add(into, i, (close + 1 | 0) - i | 0, 'attribute');
                    i = close + 1 | 0;
                    continue;
                }
            }
            CurlyBraceLanguage.add(into, i, 1, CurlyBraceLanguage.punctuation.has(c) ? 'punctuation' : 'operator');
            i = i + 1 | 0;
        }
        return CurlyBraceLanguage.stateNormal;
    }

    wordKind(word: string, line: string, afterIndex: number) {
        if (word.length > 1 && word[0] === '@') return 'attribute';
        if (this.reservedWords.has(word)) return 'keyword';
        if (this.constantWords.has(word)) return 'constant';
        if (this.typeWords.has(word)) return 'type';
        let next = CurlyBraceLanguage.nextNonSpace(line, afterIndex);
        if (next === '(') return 'function';
        if (word.length > 0 && (/^\p{Lu}$/u.test(word[0]))) return 'type';
        return 'plain';
    }

    closeMultilineString(line: string) {
        for (let i = 0; i < line.length; i = i + 1 | 0) {
            if (line[i] === '`' && this.hasTemplateStrings) return i + 1 | 0;
            if (line[i] !== '"') continue;
            if ((i + 1 | 0) < line.length && line[i + 1 | 0] === '"') {
                i = i + 1 | 0;
                continue;
            }
            return i + 1 | 0;
        }
        return -1;
    }

    static prefixedString(line: string, start: number): [number, boolean] | null {
        let verbatim = false;
        let i = start;
        while (i < line.length && (line[i] === '$' || line[i] === '@')) {
            if (line[i] === '@') {
                if (verbatim) return null;
                verbatim = true;
            }
            i = i + 1 | 0;
        }
        return i < line.length && line[i] === '"' ? [i, verbatim] : null;
    }

    static add(into: CodeToken[], start: number, length: number, kind: $CodeTokenKindValue) {
        if (length <= 0) return;
        if (into.length > 0 && into[into.length - 1].kind === kind && into[into.length - 1].end === start) {
            $eq.collections.listSet(into, into.length - 1, $eq.withPatch(into[into.length - 1], { length: into[into.length - 1].length + length | 0 }));
            return;
        }
        $eq.collections.listAdd(into, new CodeToken(start, length, kind));
    }

    static scanQuoted(line: string, from: number, quote: string) {
        for (let i = from; i < line.length; i = i + 1 | 0) {
            if (line[i] === '\\') {
                i = i + 1 | 0;
                continue;
            }
            if (line[i] === quote) return i + 1 | 0;
        }
        return -1;
    }

    static quotesAt(line: string, index: number) {
        let count = 0;
        while ((index + count | 0) < line.length && line[index + count | 0] === '"') count = count + 1 | 0;
        return count;
    }

    static closeRaw(line: string, from: number, quotes: number) {
        for (let i = from; i < line.length; i = i + 1 | 0) {
            if (line[i] !== '"') continue;
            let run = CurlyBraceLanguage.quotesAt(line, i);
            if (run >= quotes) return i + run | 0;
            i = i + (run - 1 | 0) | 0;
        }
        return -1;
    }

    static scanVerbatim(line: string, from: number) {
        for (let i = from; i < line.length; i = i + 1 | 0) {
            if (line[i] !== '"') continue;
            if ((i + 1 | 0) < line.length && line[i + 1 | 0] === '"') {
                i = i + 1 | 0;
                continue;
            }
            return i + 1 | 0;
        }
        return -1;
    }

    static scanNumber(line: string, from: number) {
        let i = from;
        if (line[i] === '0' && (i + 1 | 0) < line.length && (line[i + 1 | 0] === 'x' || line[i + 1 | 0] === 'X' || line[i + 1 | 0] === 'b' || line[i + 1 | 0] === 'B')) {
            i = i + 2 | 0;
            while (i < line.length && ((/^[\p{L}\p{Nd}]$/u.test(line[i])) || line[i] === '_')) i = i + 1 | 0;
            return i;
        }
        while (i < line.length && ((/^\p{Nd}$/u.test(line[i])) || line[i] === '_')) i = i + 1 | 0;
        if (i < line.length && line[i] === '.' && (i + 1 | 0) < line.length && (/^\p{Nd}$/u.test(line[i + 1 | 0]))) {
            i = i + 1 | 0;
            while (i < line.length && ((/^\p{Nd}$/u.test(line[i])) || line[i] === '_')) i = i + 1 | 0;
        }
        if (i < line.length && (line[i] === 'e' || line[i] === 'E')) {
            let exponent = i + 1 | 0;
            if (exponent < line.length && (line[exponent] === '+' || line[exponent] === '-')) exponent = exponent + 1 | 0;
            if (exponent < line.length && (/^\p{Nd}$/u.test(line[exponent]))) {
                i = exponent;
                while (i < line.length && (/^\p{Nd}$/u.test(line[i]))) i = i + 1 | 0;
            }
        }
        while (i < line.length && (/^\p{L}$/u.test(line[i]))) i = i + 1 | 0;
        return i;
    }

    static nextNonSpace(line: string, from: number) {
        for (let i = from; i < line.length; i = i + 1 | 0) if (!$eq.text.isWhiteSpace(line[i])) return line[i];
        return '\u0000';
    }

    static isLineHead(line: string, index: number) {
        for (let i = 0; i < index; i = i + 1 | 0) if (!$eq.text.isWhiteSpace(line[i])) return false;
        return true;
    }

    toJSON() {
        return $eq.json(this);
    }

    toString(): string {
        return 'eQuantic.UI.Code.CurlyBraceLanguage';
    }
}

