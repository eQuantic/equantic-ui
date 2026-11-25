import { $eq, CodeDocument, CodeToken } from "../runtime-exports";

export class CodeHighlighter {
    constructor(language: any) {
        this._tokens = [];
        this._endStates = [];
        this.language = null;
        this.language = language;
    }

    _tokens!: CodeToken[][];
    _endStates!: number[];
    language!: any;

    useLanguage(language: any) {
        this.language = language;
        this.invalidate();
    }

    invalidate() {
        $eq.collections.listClear(this._tokens);
        $eq.collections.listClear(this._endStates);
    }

    tokensFor(document: CodeDocument, line: number) {
        if (line < 0 || line >= document.lineCount) return [];
        this.ensureThrough(document, line);
        return this._tokens[line];
    }

    lineChanged(document: CodeDocument, line: number, linesInserted: number = 0, linesRemoved: number = 0) {
        if (linesInserted !== linesRemoved) {
            this.trimTo(line);
            return document.lineCount - 1 | 0;
        }
        if (line >= this._tokens.length) return line;
        let last = Math.min(line + linesInserted | 0, this._tokens.length - 1 | 0);
        let before = this._endStates[last];
        for (let rewritten = line; rewritten <= last; rewritten = rewritten + 1 | 0) this.retokenize(document, rewritten);
        if (this._endStates[last] === before) return last;
        for (let next = last + 1 | 0; next < this._tokens.length; next = next + 1 | 0) {
            let previous = this._endStates[next];
            this.retokenize(document, next);
            if (this._endStates[next] === previous) return next;
        }
        return this._tokens.length - 1 | 0;
    }

    ensureThrough(document: CodeDocument, line: number) {
        while (this._tokens.length <= line && this._tokens.length < document.lineCount) {
            let index = this._tokens.length;
            let state = index === 0 ? 0 : this._endStates[index - 1 | 0];
            let tokens: CodeToken[] = [];
            let endState = this.language.tokenize(document.line(index), state, tokens);
            $eq.collections.listAdd(this._tokens, tokens);
            $eq.collections.listAdd(this._endStates, endState);
        }
    }

    retokenize(document: CodeDocument, line: number) {
        let state = line === 0 ? 0 : this._endStates[line - 1 | 0];
        let tokens = this._tokens[line];
        $eq.collections.listClear(tokens);
        $eq.collections.listSet(this._endStates, line, this.language.tokenize(document.line(line), state, tokens));
    }

    trimTo(line: number) {
        if (line >= this._tokens.length) return;
        $eq.collections.listRemoveRange(this._tokens, line, this._tokens.length - line | 0);
        $eq.collections.listRemoveRange(this._endStates, line, this._endStates.length - line | 0);
    }

    toString(): string {
        return 'eQuantic.UI.Code.CodeHighlighter';
    }
}

