import { $eq, CodeLanguageRules, CodeToken } from "../runtime-exports";

export class JsonLanguage {
    static $interfaces = ['eQuantic.UI.Code.ICodeLanguage'];

    constructor() {
        this.rules = (() => { let $n0: CodeLanguageRules; return ($n0 = new CodeLanguageRules(), $n0.indentWidth = 2, $n0.quotes = ['"'], $n0); })();
        this.keywords = ['true', 'false', 'null'];
    }

    rules!: CodeLanguageRules;
    keywords!: string[];

    get name(): string {
        return 'JSON';
    }

    tokenize(line: string, _state: number, into: CodeToken[]) {
        let i = 0;
        while (i < line.length) {
            let c = line[i];
            if ($eq.text.isWhiteSpace(c)) {
                i = i + 1 | 0;
                continue;
            }
            if (c === '"') {
                let end = i + 1 | 0;
                while (end < line.length) {
                    if (line[end] === '\\') {
                        end = end + 2 | 0;
                        continue;
                    }
                    if (line[end] === '"') {
                        end = end + 1 | 0;
                        break;
                    }
                    end = end + 1 | 0;
                }
                let isKey = JsonLanguage.nextNonSpace(line, end) === ':';
                $eq.collections.listAdd(into, new CodeToken(i, Math.min(end, line.length) - i | 0, isKey ? 'property' : 'string'));
                i = Math.min(end, line.length);
                continue;
            }
            if ((/^\p{Nd}$/u.test(c)) || c === '-' && (i + 1 | 0) < line.length && (/^\p{Nd}$/u.test(line[i + 1 | 0]))) {
                let end = i + 1 | 0;
                while (end < line.length && ((/^\p{Nd}$/u.test(line[end])) || line[end] === '.' || line[end] === 'e' || line[end] === 'E' || line[end] === '+' || line[end] === '-')) end = end + 1 | 0;
                $eq.collections.listAdd(into, new CodeToken(i, end - i | 0, 'number'));
                i = end;
                continue;
            }
            if ((/^\p{L}$/u.test(c))) {
                let end = i;
                while (end < line.length && (/^\p{L}$/u.test(line[end]))) end = end + 1 | 0;
                $eq.collections.listAdd(into, new CodeToken(i, end - i | 0, 'constant'));
                i = end;
                continue;
            }
            $eq.collections.listAdd(into, new CodeToken(i, 1, 'punctuation'));
            i = i + 1 | 0;
        }
        return 0;
    }

    static nextNonSpace(line: string, from: number) {
        for (let i = from; i < line.length; i = i + 1 | 0) if (!$eq.text.isWhiteSpace(line[i])) return line[i];
        return '\u0000';
    }

    toString(): string {
        return 'eQuantic.UI.Code.JsonLanguage';
    }
}

