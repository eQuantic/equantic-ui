import { CodeDocument, CodeToken } from "@equantic/runtime";
export class CodeHighlighter {
    constructor(language: any) { this._tokens = []; this._endStates = []; this.language = language; }
    _tokens: CodeToken[][];
    _endStates: number[];
    declare language: any;
    useLanguage(language: any) { this.language = language;this.invalidate(); }
    invalidate() { this._tokens.splice(0);this._endStates.splice(0); }
    tokensFor(document: CodeDocument, line: number) { if (line < 0 || line >= document.lineCount) return [];this.ensureThrough(document, line);return this._tokens[line]; }
    lineChanged(document: CodeDocument, line: number, linesInserted: number = 0, linesRemoved: number = 0) { if (linesInserted !== linesRemoved) {this.trimTo(line);return document.lineCount - 1;}if (line >= this._tokens.length) return line;let before = this._endStates[line];this.retokenize(document, line);if (this._endStates[line] === before) return line;for (let next = line + 1; next < this._tokens.length; next++) {let previous = this._endStates[next];this.retokenize(document, next);if (this._endStates[next] === previous) return next;}return this._tokens.length - 1; }
    ensureThrough(document: CodeDocument, line: number) { while (this._tokens.length <= line && this._tokens.length < document.lineCount) {let index = this._tokens.length;let state = index === 0 ? 0 : this._endStates[index - 1];let tokens: CodeToken[] = [];let endState = this.language.tokenize(document.line(index), state, tokens);this._tokens.push(tokens);this._endStates.push(endState);} }
    retokenize(document: CodeDocument, line: number) { let state = line === 0 ? 0 : this._endStates[line - 1];let tokens = this._tokens[line];tokens.splice(0);this._endStates[line] = this.language.tokenize(document.line(line), state, tokens); }
    trimTo(line: number) { if (line >= this._tokens.length) return;this._tokens.splice(line, this._tokens.length - line);this._endStates.splice(line, this._endStates.length - line); }
}

