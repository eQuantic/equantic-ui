import { $eq, CodeDocument, CodeLanguageRules, CodeToken, CodeTokenKindValue } from "../runtime-exports";

export abstract class CurlyBraceLanguage {
    constructor(props?: any) {
        if (props && typeof props === 'object') Object.assign(this, props);
    }

    static stateNormal: number = 0;
    static stateBlockComment: number = 1;
    static stateMultilineString: number = 2;
    static stateRawString: number = 16;
    static _punctuation: Set<string> | undefined;

    static get punctuation(): Set<string> {
        return CurlyBraceLanguage._punctuation ??= new Set(['(', ')', '[', ']', '{', '}', ',', ';', '.', ':']);
    }

    abstract name: string;
    rules: CodeLanguageRules = CodeLanguageRules.default;
    abstract keywords: Set<string>;
    abstract typeWords: Set<string>;
    abstract constantWords: Set<string>;

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

    tokenize(line: string, state: number, into: CodeToken[]) {
        let i = 0;
        if (state === CurlyBraceLanguage.stateBlockComment) {
            let close = line.indexOf('*/');
            if (close < 0) {
                CurlyBraceLanguage.add(into, 0, line.length, 'comment');
                return CurlyBraceLanguage.stateBlockComment;
            }
            CurlyBraceLanguage.add(into, 0, close + 2, 'comment');
            i = close + 2;
        } else if (state >= CurlyBraceLanguage.stateRawString) {
            let quotes = state - CurlyBraceLanguage.stateRawString;
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
            if ((/^\p{White_Space}$/u.test(c))) {
                i++;
                continue;
            }
            if (c === '/' && i + 1 < line.length) {
                if (line[i + 1] === '/') {
                    CurlyBraceLanguage.add(into, i, line.length - i, 'comment');
                    return CurlyBraceLanguage.stateNormal;
                }
                if (line[i + 1] === '*') {
                    let close = line.indexOf('*/', i + 2);
                    if (close < 0) {
                        CurlyBraceLanguage.add(into, i, line.length - i, 'comment');
                        return CurlyBraceLanguage.stateBlockComment;
                    }
                    CurlyBraceLanguage.add(into, i, close + 2 - i, 'comment');
                    i = close + 2;
                    continue;
                }
            }
            let prefixed: any; 
            if (this.hasVerbatimStrings && (c === '$' || c === '@') && (prefixed = CurlyBraceLanguage.prefixedString(line, i)) != null) {
                let quote = prefixed[0];
                if (this.hasRawStrings && !prefixed[1] && CurlyBraceLanguage.quotesAt(line, quote) >= 3) {
                    let quotes = CurlyBraceLanguage.quotesAt(line, quote);
                    let raw = CurlyBraceLanguage.closeRaw(line, quote + quotes, quotes);
                    if (raw < 0) {
                        CurlyBraceLanguage.add(into, i, line.length - i, 'string');
                        return CurlyBraceLanguage.stateRawString + quotes;
                    }
                    CurlyBraceLanguage.add(into, i, raw - i, 'string');
                    i = raw;
                    continue;
                }
                let end = prefixed[1] ? CurlyBraceLanguage.scanVerbatim(line, quote + 1) : CurlyBraceLanguage.scanQuoted(line, quote + 1, '"');
                if (end < 0) {
                    CurlyBraceLanguage.add(into, i, line.length - i, 'string');
                    if (prefixed[1]) return CurlyBraceLanguage.stateMultilineString;
                    i = line.length;
                    continue;
                }
                CurlyBraceLanguage.add(into, i, end - i, 'string');
                i = end;
                continue;
            }
            if (this.hasTemplateStrings && c === '`') {
                let end = CurlyBraceLanguage.scanQuoted(line, i + 1, '`');
                if (end < 0) {
                    CurlyBraceLanguage.add(into, i, line.length - i, 'string');
                    return CurlyBraceLanguage.stateMultilineString;
                }
                CurlyBraceLanguage.add(into, i, end - i, 'string');
                i = end;
                continue;
            }
            if (this.hasRawStrings && c === '"' && CurlyBraceLanguage.quotesAt(line, i) >= 3) {
                let quotes = CurlyBraceLanguage.quotesAt(line, i);
                let end = CurlyBraceLanguage.closeRaw(line, i + quotes, quotes);
                if (end < 0) {
                    CurlyBraceLanguage.add(into, i, line.length - i, 'string');
                    return CurlyBraceLanguage.stateRawString + quotes;
                }
                CurlyBraceLanguage.add(into, i, end - i, 'string');
                i = end;
                continue;
            }
            if (c === '"' || c === '\'') {
                let end = CurlyBraceLanguage.scanQuoted(line, i + 1, c);
                CurlyBraceLanguage.add(into, i, (end < 0 ? line.length : end) - i, 'string');
                i = end < 0 ? line.length : end;
                continue;
            }
            if ((/^\p{Nd}$/u.test(c))) {
                let end = CurlyBraceLanguage.scanNumber(line, i);
                CurlyBraceLanguage.add(into, i, end - i, 'number');
                i = end;
                continue;
            }
            if ((/^\p{L}$/u.test(c)) || c === '_' || this.hasAtDecorators && c === '@') {
                let start = i;
                if (line[i] === '@') i++;
                while (i < line.length && CodeDocument.isWordChar(line[i])) i++;
                let word = line.slice(start, i);
                CurlyBraceLanguage.add(into, start, i - start, this.wordKind(word, line, i));
                continue;
            }
            if (this.hasBracketAttributes && c === '[' && CurlyBraceLanguage.isLineHead(line, i)) {
                let close = line.indexOf(']', i);
                if (close > 0) {
                    CurlyBraceLanguage.add(into, i, close + 1 - i, 'attribute');
                    i = close + 1;
                    continue;
                }
            }
            CurlyBraceLanguage.add(into, i, 1, CurlyBraceLanguage.punctuation.has(c) ? 'punctuation' : 'operator');
            i++;
        }
        return CurlyBraceLanguage.stateNormal;
    }

    wordKind(word: string, line: string, afterIndex: number) {
        if (word.length > 1 && word[0] === '@') return 'attribute';
        if (this.keywords.has(word)) return 'keyword';
        if (this.constantWords.has(word)) return 'constant';
        if (this.typeWords.has(word)) return 'type';
        let next = CurlyBraceLanguage.nextNonSpace(line, afterIndex);
        if (next === '(') return 'function';
        if (word.length > 0 && (/^\p{Lu}$/u.test(word[0]))) return 'type';
        return 'plain';
    }

    closeMultilineString(line: string) {
        for (let i = 0; i < line.length; i++) {
            if (line[i] === '`' && this.hasTemplateStrings) return i + 1;
            if (line[i] !== '"') continue;
            if (i + 1 < line.length && line[i + 1] === '"') {
                i++;
                continue;
            }
            return i + 1;
        }
        return -1;
    }

    static prefixedString(line: string, start: number) {
        let verbatim = false;
        let i = start;
        while (i < line.length && (line[i] === '$' || line[i] === '@')) {
            if (line[i] === '@') {
                if (verbatim) return null;
                verbatim = true;
            }
            i++;
        }
        return i < line.length && line[i] === '"' ? [i, verbatim] : null;
    }

    static add(into: CodeToken[], start: number, length: number, kind: CodeTokenKindValue) {
        if (length <= 0) return;
        if (into.length > 0 && into[into.length - 1].kind === kind && into[into.length - 1].end === start) {
            into[into.length - 1] = $eq.withPatch(into[into.length - 1], { length: into[into.length - 1].length + length });
            return;
        }
        into.push(new CodeToken(start, length, kind));
    }

    static scanQuoted(line: string, from: number, quote: string) {
        for (let i = from; i < line.length; i++) {
            if (line[i] === '\\') {
                i++;
                continue;
            }
            if (line[i] === quote) return i + 1;
        }
        return -1;
    }

    static quotesAt(line: string, index: number) {
        let count = 0;
        while (index + count < line.length && line[index + count] === '"') count++;
        return count;
    }

    static closeRaw(line: string, from: number, quotes: number) {
        for (let i = from; i < line.length; i++) {
            if (line[i] !== '"') continue;
            let run = CurlyBraceLanguage.quotesAt(line, i);
            if (run >= quotes) return i + run;
            i += run - 1;
        }
        return -1;
    }

    static scanVerbatim(line: string, from: number) {
        for (let i = from; i < line.length; i++) {
            if (line[i] !== '"') continue;
            if (i + 1 < line.length && line[i + 1] === '"') {
                i++;
                continue;
            }
            return i + 1;
        }
        return -1;
    }

    static scanNumber(line: string, from: number) {
        let i = from;
        if (line[i] === '0' && i + 1 < line.length && (line[i + 1] === 'x' || line[i + 1] === 'X' || line[i + 1] === 'b' || line[i + 1] === 'B')) {
            i += 2;
            while (i < line.length && ((/^[\p{L}\p{Nd}]$/u.test(line[i])) || line[i] === '_')) i++;
            return i;
        }
        while (i < line.length && ((/^\p{Nd}$/u.test(line[i])) || line[i] === '_')) i++;
        if (i < line.length && line[i] === '.' && i + 1 < line.length && (/^\p{Nd}$/u.test(line[i + 1]))) {
            i++;
            while (i < line.length && ((/^\p{Nd}$/u.test(line[i])) || line[i] === '_')) i++;
        }
        if (i < line.length && (line[i] === 'e' || line[i] === 'E')) {
            let exponent = i + 1;
            if (exponent < line.length && (line[exponent] === '+' || line[exponent] === '-')) exponent++;
            if (exponent < line.length && (/^\p{Nd}$/u.test(line[exponent]))) {
                i = exponent;
                while (i < line.length && (/^\p{Nd}$/u.test(line[i]))) i++;
            }
        }
        while (i < line.length && (/^\p{L}$/u.test(line[i]))) i++;
        return i;
    }

    static nextNonSpace(line: string, from: number) {
        for (let i = from; i < line.length; i++) if (!(/^\p{White_Space}$/u.test(line[i]))) return line[i];
        return '\0';
    }

    static isLineHead(line: string, index: number) {
        for (let i = 0; i < index; i++) if (!(/^\p{White_Space}$/u.test(line[i]))) return false;
        return true;
    }
}

