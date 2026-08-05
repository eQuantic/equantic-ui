import { CodeToken } from "../runtime-exports";
export class PlainTextLanguage {
    get name(): string { return 'Text'; }
    tokenize(line: string, _state: number, into: CodeToken[]) { if (line.length > 0) into.push(new CodeToken(0, line.length, 'plain'));return 0; }
}

