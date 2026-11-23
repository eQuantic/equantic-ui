import { $CodeTokenKindValue, $eq, CodeDocument, CodeLanguageRules, CodeToken } from "../runtime-exports";

export class XmlLanguage {
    static $interfaces = ['eQuantic.UI.Code.ICodeLanguage'];

    constructor() {
        this.rules = (() => { let $n0: CodeLanguageRules; return ($n0 = new CodeLanguageRules(), $n0.blockComment = ['<!--', '-->'], $n0.brackets = [['<', '>'], ['(', ')'], ['[', ']']], $n0.indentAfter = ['>'], $n0.outdentOn = ['<'], $n0.indentWidth = 2, $n0); })();
    }

    rules!: CodeLanguageRules;
    static normal: number = 0;
    static inComment: number = 1;

    get name(): string {
        return 'XML';
    }

    tokenize(line: string, state: number, into: CodeToken[]) {
        let i = 0;
        if (state === XmlLanguage.inComment) {
            let close = $eq.text.indexOf(line, '-->', 'ordinal');
            if (close < 0) {
                $eq.collections.listAdd(into, new CodeToken(0, line.length, 'comment'));
                return XmlLanguage.inComment;
            }
            $eq.collections.listAdd(into, new CodeToken(0, close + 3 | 0, 'comment'));
            i = close + 3 | 0;
        }
        while (i < line.length) {
            let c = line[i];
            if ($eq.text.isWhiteSpace(c)) {
                i = i + 1 | 0;
                continue;
            }
            if (c === '<') {
                if ($eq.text.startsWith(line.slice(i), '<!--', 'ordinal')) {
                    let close = $eq.text.indexOf(line, '-->', i, 'ordinal');
                    if (close < 0) {
                        $eq.collections.listAdd(into, new CodeToken(i, line.length - i | 0, 'comment'));
                        return XmlLanguage.inComment;
                    }
                    $eq.collections.listAdd(into, new CodeToken(i, (close + 3 | 0) - i | 0, 'comment'));
                    i = close + 3 | 0;
                    continue;
                }
                let nameStart = i + 1 | 0;
                while (nameStart < line.length && (line[nameStart] === '/' || line[nameStart] === '?' || line[nameStart] === '!')) nameStart = nameStart + 1 | 0;
                $eq.collections.listAdd(into, new CodeToken(i, nameStart - i | 0, 'punctuation'));
                let nameEnd = nameStart;
                while (nameEnd < line.length && (CodeDocument.isWordChar(line[nameEnd]) || line[nameEnd] === '-' || line[nameEnd] === ':')) nameEnd = nameEnd + 1 | 0;
                if (nameEnd > nameStart) $eq.collections.listAdd(into, new CodeToken(nameStart, nameEnd - nameStart | 0, 'keyword'));
                i = nameEnd;
                continue;
            }
            if (c === '"' || c === '\'') {
                let end = i + 1 | 0;
                while (end < line.length && line[end] !== c) end = end + 1 | 0;
                if (end < line.length) end = end + 1 | 0;
                $eq.collections.listAdd(into, new CodeToken(i, end - i | 0, 'string'));
                i = end;
                continue;
            }
            if (CodeDocument.isWordChar(c)) {
                let end = i;
                while (end < line.length && (CodeDocument.isWordChar(line[end]) || line[end] === '-' || line[end] === ':')) end = end + 1 | 0;
                let kind: $CodeTokenKindValue = XmlLanguage.nextNonSpace(line, end) === '=' ? 'property' : 'plain';
                $eq.collections.listAdd(into, new CodeToken(i, end - i | 0, kind));
                i = end;
                continue;
            }
            $eq.collections.listAdd(into, new CodeToken(i, 1, ((c === '>' || c === '/') || c === '=') ? 'punctuation' : 'plain'));
            i = i + 1 | 0;
        }
        return XmlLanguage.normal;
    }

    static nextNonSpace(line: string, from: number) {
        for (let i = from; i < line.length; i = i + 1 | 0) if (!$eq.text.isWhiteSpace(line[i])) return line[i];
        return '\u0000';
    }

    get keywords(): string[] {
        return [];
    }

    toString(): string {
        return 'eQuantic.UI.Code.XmlLanguage';
    }
}

