import { $eq, CodeSnippetPart, StringBuilder } from "../runtime-exports";

export class CodeSnippetParser {
    constructor(source: string) {
        this._source = null!;
        this._at = 0;
        this._source = source;
    }

    _source!: string;
    _at!: number;

    parse() {
        this._at = 0;
        return this.parts(false);
    }

    parts(inPlaceholder: boolean) {
        let parts: CodeSnippetPart[] = [];
        let text = $eq.text.stringBuilder();
        while (this._at < this._source.length) {
            let c = this._source[this._at];
            if (c === '\\' && this._at + 1 < this._source.length && CodeSnippetParser.isEscaped(this._source[this._at + 1])) {
                text.append(this._source[this._at + 1]);
                this._at += 2;
                continue;
            }
            if (inPlaceholder && c === '}') break;
            let part: any; 
            if (c === '$' && (part = this.construct()) != null) {
                CodeSnippetParser.flush(text, parts);
                parts.push(part);
                continue;
            }
            text.append(c);
            this._at++;
        }
        CodeSnippetParser.flush(text, parts);
        return parts;
    }

    static isEscaped(c: string) {
        return c === '$' || c === '}' || c === '\\';
    }

    static flush(text: StringBuilder, parts: CodeSnippetPart[]) {
        if (text.length === 0) return;
        parts.push(new CodeSnippetPart(text.toString(), -1, false, null, null));
        text.clear();
    }

    construct() {
        let start = this._at;
        this._at++;
        if (this._at < this._source.length && CodeSnippetParser.isDigit(this._source[this._at])) return CodeSnippetParser.stop(this.number(), null, null);
        if (this._at < this._source.length && CodeSnippetParser.isNameStart(this._source[this._at])) return CodeSnippetParser.variable(this.name(), null);
        if (this.eat('{')) {
            if (this._at < this._source.length && CodeSnippetParser.isDigit(this._source[this._at])) {
                let index = this.number();
                if (this.eat('}')) return CodeSnippetParser.stop(index, null, null);
                if (this.eat(':')) {
                    let children = this.parts(true);
                    if (this.eat('}')) return CodeSnippetParser.stop(index, children, null);
                } else if (this.eat('|')) {
                    let options: any; 
                    if ((options = this.choices()) != null && this.eat('}')) return CodeSnippetParser.stop(index, null, options);
                } else if (this.transform() && this.eat('}')) return CodeSnippetParser.stop(index, null, null);
            } else if (this._at < this._source.length && CodeSnippetParser.isNameStart(this._source[this._at])) {
                let name = this.name();
                if (this.eat('}')) return CodeSnippetParser.variable(name, null);
                if (this.eat(':')) {
                    let children = this.parts(true);
                    if (this.eat('}')) return CodeSnippetParser.variable(name, children);
                } else if (this.transform() && this.eat('}')) return CodeSnippetParser.variable(name, null);
            }
        }
        this._at = start;
        return null;
    }

    static stop(index: number, children: CodeSnippetPart[] | null, choices: string[] | null) {
        return new CodeSnippetPart('', index, false, children, choices);
    }

    static variable(name: string, children: CodeSnippetPart[] | null) {
        return new CodeSnippetPart(name, -1, true, children, null);
    }

    choices() {
        let options: string[] = [];
        let option = $eq.text.stringBuilder();
        while (this._at < this._source.length) {
            let c = this._source[this._at];
            if (c === '\\' && this._at + 1 < this._source.length && (this._source[this._at + 1] === ',' || this._source[this._at + 1] === '|' || this._source[this._at + 1] === '\\')) {
                option.append(this._source[this._at + 1]);
                this._at += 2;
                continue;
            }
            this._at++;
            if (c === ',' || c === '|') {
                options.push(option.toString());
                option.clear();
                if (c === '|') return options;
                continue;
            }
            option.append(c);
        }
        return null;
    }

    transform() {
        if (!this.eat('/')) return false;
        if (!this.until('/', false) || !this.until('/', true)) return false;
        while (this._at < this._source.length && CodeSnippetParser.isNameStart(this._source[this._at]) && this._source[this._at] !== '_') this._at++;
        return true;
    }

    until(end: string, inFormat: boolean) {
        while (this._at < this._source.length) {
            let c = this._source[this._at];
            if (c === '\\' && this._at + 1 < this._source.length) {
                this._at += 2;
                continue;
            }
            if (inFormat && c === '$' && this._at + 1 < this._source.length && this._source[this._at + 1] === '{') {
                let close = $eq.text.indexOfChar(this._source, '}', this._at);
                if (close < 0) return false;
                this._at = close + 1;
                continue;
            }
            this._at++;
            if (c === end) return true;
        }
        return false;
    }

    eat(c: string) {
        if (this._at >= this._source.length || this._source[this._at] !== c) return false;
        this._at++;
        return true;
    }

    number() {
        let value = 0;
        while (this._at < this._source.length && CodeSnippetParser.isDigit(this._source[this._at])) {
            if (value < 100_000_000) value = value * 10 + (this._source[this._at].charCodeAt(0) - 48);
            this._at++;
        }
        return value;
    }

    static isDigit(c: string) {
        return c >= '0' && c <= '9';
    }

    static isNameStart(c: string) {
        return c === '_' || c >= 'a' && c <= 'z' || c >= 'A' && c <= 'Z';
    }

    name() {
        let start = this._at;
        while (this._at < this._source.length && (CodeSnippetParser.isNameStart(this._source[this._at]) || CodeSnippetParser.isDigit(this._source[this._at]))) this._at++;
        return $eq.text.substring(this._source, start, this._at - start);
    }

    toString(): string {
        return 'eQuantic.UI.Code.CodeSnippetParser';
    }
}

