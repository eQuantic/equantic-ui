import { $eq, CodePosition, CodeSnippet, CodeSnippetPart, CodeSnippetPlace, StringBuilder } from "../runtime-exports";

export class CodeSnippetExpansion {
    constructor(variable: (value: string) => string | null, indent: string, step: string) {
        this._variable = null!;
        this._indent = null!;
        this._step = null!;
        this._text = $eq.text.stringBuilder();
        this._places = [];
        this._defaults = $eq.collections.dictionary();
        this._defining = $eq.collections.dictionary();
        this._unknown = $eq.collections.dictionary();
        this._resolving = [];
        this._writing = [];
        this._enclosing = [];
        this._line = 0;
        this._column = 0;
        this._lineStart = false;
        this._variable = variable;
        this._indent = indent;
        this._step = step;
    }

    _variable!: (value: string) => string | null;
    _indent!: string;
    _step!: string;
    _text!: StringBuilder;
    _places!: CodeSnippetPlace[];
    _defaults!: any;
    _defining!: any;
    _unknown!: any;
    _resolving!: number[];
    _writing!: number[];
    _enclosing!: number[];
    _line!: number;
    _column!: number;
    _lineStart!: boolean;

    get text(): string {
        return this._text.toString();
    }

    get places(): CodeSnippetPlace[] {
        return this._places;
    }

    get end(): CodePosition {
        return new CodePosition(this._line, this._column);
    }

    static of(snippet: CodeSnippet, variable: (value: string) => string | null, indent: string, step: string) {
        let expansion = new CodeSnippetExpansion(variable, indent, step);
        expansion.number(snippet.parts, 1 + CodeSnippetExpansion.maxStop(snippet.parts));
        expansion.define(snippet.parts);
        expansion.write(snippet.parts);
        return expansion;
    }

    static maxStop(parts: CodeSnippetPart[]) {
        let max = 0;
        for (const part of parts) {
            if (part.isStop && part.index > max) max = part.index;
            let children: any; 
            if ((children = part.children) != null) {
                let inner = CodeSnippetExpansion.maxStop(children);
                if (inner > max) max = inner;
            }
        }
        return max;
    }

    number(parts: CodeSnippetPart[], next: number) {
        for (const part of parts) {
            if (part.variable && this._variable(part.text) == null && !this._unknown.has(part.text)) $eq.mapSet(this._unknown, part.text, next++);
            let children: any; 
            if ((children = part.children) != null) next = this.number(children, next);
        }
        return next;
    }

    define(parts: CodeSnippetPart[]) {
        for (const part of parts) {
            let index = this.indexOf(part);
            if (index >= 0 && !this._defining.has(index) && (!(part.children == null) || !(part.choices == null) || part.variable)) $eq.mapSet(this._defining, index, part);
            let children: any; 
            if ((children = part.children) != null) this.define(children);
        }
    }

    indexOf(part: CodeSnippetPart) {
        let index: any; 
        return part.isStop ? part.index : part.variable && (($0, $1) => ($0.has($1) ? ((index = $0.get($1)), true) : ((index = 0), false)))(this._unknown, part.text) ? index : -1;
    }

    defaultOf(index: number) {
        let known: any; 
        if ((($0) => ($0.has(index) ? ((known = $0.get(index)), true) : ((known = null), false)))(this._defaults)) return known;
        let defining: any; 
        if (!(($0) => ($0.has(index) ? ((defining = $0.get(index)), true) : ((defining = null), false)))(this._defining) || this._resolving.includes(index)) return '';
        this._resolving.push(index);
        let choices: any; let children: any; 
        let text = ((defining.choices != null && $eq.collections.count(defining.choices) > 0) && (choices = defining.choices, true)) ? $eq.collections.item(choices, 0) : (children = defining.children) != null ? this.plain(children) : defining.text;
        $eq.collections.remove(this._resolving, index);
        $eq.mapSet(this._defaults, index, text);
        return text;
    }

    plain(parts: CodeSnippetPart[]) {
        let text = $eq.text.stringBuilder();
        for (const part of parts) {
            if (!part.isStop && !part.variable) {
                text.append(part.text);
                continue;
            }
            let index = this.indexOf(part);
            if (index >= 0) text.append(this.defaultOf(index)); else { let value: any; let nested: any; if ((value = ($o => (nested = $o.nested, $o.$))(this.variableText(part))) != null) text.append(value); else if (!(nested == null)) text.append(this.plain(nested)); }
        }
        return text.toString();
    }

    variableText(part: CodeSnippetPart) {
        let nested: any;
        const $r = (() => {
            nested = null;
            let value = this._variable(part.text) ?? '';
            if (value.length > 0) return value;
            nested = part.children;
            return null;
        })();
        return { $: $r, nested };
    }

    write(parts: CodeSnippetPart[]) {
        for (const part of parts) {
            if (!part.isStop && !part.variable) {
                this.append(part.text);
                continue;
            }
            let index = this.indexOf(part);
            if (index < 0) {
                let value: any; let nested: any; 
                if ((value = ($o => (nested = $o.nested, $o.$))(this.variableText(part))) != null) this.append(value); else if (!(nested == null)) this.write(nested);
                continue;
            }
            let start = this.end;
            let defining: any; let children: any; 
            if ((($0) => ($0.has(index) ? ((defining = $0.get(index)), true) : ((defining = null), false)))(this._defining) && defining.choices == null && (children = defining.children) != null && !this._writing.includes(index)) {
                this._writing.push(index);
                this._enclosing.push(index);
                this.write(children);
                this._enclosing.splice(this._enclosing.length - 1, 1);
                $eq.collections.remove(this._writing, index);
            } else this.append(this.defaultOf(index));
            this._places.push(new CodeSnippetPlace(index, start, this.end, $eq.collections.listCopy(this._enclosing), part.choices));
        }
    }

    append(text: string) {
        for (let i = 0; i < text.length; i++) {
            let c = text[i];
            if (c === '\r' || c === '\n') {
                if (c === '\r' && i + 1 < text.length && text[i + 1] === '\n') i++;
                this._text.append('\n');
                this._text.append(this._indent);
                this._line++;
                this._column = this._indent.length;
                this._lineStart = true;
                continue;
            }
            if (this._lineStart && c === '\t') {
                this._text.append(this._step);
                this._column += this._step.length;
                continue;
            }
            this._lineStart = false;
            this._text.append(c);
            this._column++;
        }
    }

    toString(): string {
        return 'eQuantic.UI.Code.CodeSnippetExpansion';
    }
}

