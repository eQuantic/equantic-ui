import { CodeDocument, CodeLanguageRules, CodeToken, CodeTokenKindValue } from "@equantic/runtime";
export class XmlLanguage {
    constructor(props?: any) {
        if (props && typeof props === 'object') Object.assign(this, props);
    }
    static normal: number = 0;
    static inComment: number = 1;
    get name(): string {
        return 'XML';
    }
    rules: CodeLanguageRules = new CodeLanguageRules(null, ['<!--', '-->'], [['<', '>'], ['(', ')'], ['[', ']']], ['"', '\''], ['>'], ['<'], 2);
    tokenize(line: string, state: number, into: CodeToken[]) {
        let i = 0;
        if (state === XmlLanguage.inComment) {
            let close = line.indexOf('-->');
            if (close < 0) {
                into.push(new CodeToken(0, line.length, 'comment'));
                return XmlLanguage.inComment;
            }
            into.push(new CodeToken(0, close + 3, 'comment'));
            i = close + 3;
        }
        while (i < line.length) {
            let c = line[i];
            if ((/^\s$/.test(c))) {
                i++;
                continue;
            }
            if (c === '<') {
                if (line.slice(i).startsWith('<!--')) {
                    let close = line.indexOf('-->', i);
                    if (close < 0) {
                        into.push(new CodeToken(i, line.length - i, 'comment'));
                        return XmlLanguage.inComment;
                    }
                    into.push(new CodeToken(i, close + 3 - i, 'comment'));
                    i = close + 3;
                    continue;
                }
                let nameStart = i + 1;
                while (nameStart < line.length && (line[nameStart] === '/' || line[nameStart] === '?' || line[nameStart] === '!')) nameStart++;
                into.push(new CodeToken(i, nameStart - i, 'punctuation'));
                let nameEnd = nameStart;
                while (nameEnd < line.length && (CodeDocument.isWordChar(line[nameEnd]) || line[nameEnd] === '-' || line[nameEnd] === ':')) nameEnd++;
                if (nameEnd > nameStart) into.push(new CodeToken(nameStart, nameEnd - nameStart, 'keyword'));
                i = nameEnd;
                continue;
            }
            if (c === '"' || c === '\'') {
                let end = i + 1;
                while (end < line.length && line[end] !== c) end++;
                if (end < line.length) end++;
                into.push(new CodeToken(i, end - i, 'string'));
                i = end;
                continue;
            }
            if (CodeDocument.isWordChar(c)) {
                let end = i;
                while (end < line.length && (CodeDocument.isWordChar(line[end]) || line[end] === '-' || line[end] === ':')) end++;
                let kind: CodeTokenKindValue = XmlLanguage.nextNonSpace(line, end) === '=' ? 'property' : 'plain';
                into.push(new CodeToken(i, end - i, kind));
                i = end;
                continue;
            }
            into.push(new CodeToken(i, 1, ((c === '>' || c === '/') || c === '=') ? 'punctuation' : 'plain'));
            i++;
        }
        return XmlLanguage.normal;
    }
    static nextNonSpace(line: string, from: number) {
        for (let i = from; i < line.length; i++) if (!(/^\s$/.test(line[i]))) return line[i];
        return '\0';
    }
}

