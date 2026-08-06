import { CodeLanguageRules, CodeToken } from "@equantic/runtime";
export class JsonLanguage {
    constructor(props?: any) {  if (props && typeof props === 'object') Object.assign(this, props); }
    get name(): string { return 'JSON'; }
    rules: CodeLanguageRules = new CodeLanguageRules(null, null, null, ['"'], null, null, 2);
    tokenize(line: string, _state: number, into: CodeToken[]) { let i = 0;while (i < line.length) {let c = line[i];if ((/^\s$/.test(c))) {i++;continue;}if (c === '"') {let end = i + 1;while (end < line.length) {if (line[end] === '\\') {end += 2;continue;}if (line[end] === '"') {end++;break;}end++;}let isKey = JsonLanguage.nextNonSpace(line, end) === ':';into.push(new CodeToken(i, Math.min(end, line.length) - i, isKey ? 'property' : 'string'));i = Math.min(end, line.length);continue;}if ((/^\p{Nd}$/u.test(c)) || (c === '-' && i + 1 < line.length && (/^\p{Nd}$/u.test(line[i + 1])))) {let end = i + 1;while (end < line.length && ((/^\p{Nd}$/u.test(line[end])) || line[end] === '.' || line[end] === 'e' || line[end] === 'E' || line[end] === '+' || line[end] === '-')) end++;into.push(new CodeToken(i, end - i, 'number'));i = end;continue;}if ((/^\p{L}$/u.test(c))) {let end = i;while (end < line.length && (/^\p{L}$/u.test(line[end]))) end++;into.push(new CodeToken(i, end - i, 'constant'));i = end;continue;}into.push(new CodeToken(i, 1, 'punctuation'));i++;}return 0; }
    static nextNonSpace(line: string, from: number) { for (let i = from; i < line.length; i++) if (!(/^\s$/.test(line[i]))) return line[i];return '\0'; }
}

