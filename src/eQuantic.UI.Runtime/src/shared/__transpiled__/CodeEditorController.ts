import { $eq, CodeDirectionValue, CodeDocument, CodeEdit, CodeHighlighter, CodeHistory, CodeLanguageRules, CodeLanguages, CodeMotionValue, CodePosition, CodeRange } from "@equantic/runtime";

export class CodeEditorController {
    constructor(text: string = '', language: any = null, props?: any) {
        this._desiredColumn = -1; this._document = CodeDocument.fromText(text);
        this._selection = new CodeRange(CodePosition.start);
        this.highlighter = new CodeHighlighter(language ?? CodeLanguages.plainText); if (props && typeof props === 'object') Object.assign(this, props);
    }

    _document: CodeDocument;
    _selection: CodeRange;
    _desiredColumn: number;

    get document(): CodeDocument {
        return this._document;
    }

    set document(value: CodeDocument) {
        this._document = value;
        this._selection = new CodeRange(this._document.clamp(this._selection.focus));
        this.highlighter.invalidate();
        this.history.clear();
        this.changed?.(null);
    }

    get selection(): CodeRange {
        return this._selection;
    }

    set selection(value: CodeRange) {
        let next = new CodeRange(this._document.clamp(value.anchor), this._document.clamp(value.focus));
        if ($eq.equals(next, this._selection)) return;
        this.history.break();
        this._selection = next;
        this.selectionChanged?.(next);
    }

    get caret(): CodePosition {
        return this._selection.focus;
    }

    declare highlighter: CodeHighlighter;
    history: CodeHistory = new CodeHistory();

    get rules(): CodeLanguageRules {
        return this.highlighter.language.rules;
    }

    readOnly: boolean = false;
    changed: ((codeEdit?: CodeEdit | null) => void) | null = null;
    selectionChanged: ((codeRange: CodeRange) => void) | null = null;

    apply(range: CodeRange, text: string) {
        let caret: any; if (this.readOnly) return false;
        let ordered = new CodeRange(this._document.clamp(range.start), this._document.clamp(range.end));
        let removed = this._document.textIn(ordered);
        if (removed.length === 0 && text.length === 0) return false;
        let before = this._selection;
        let next = ($o => (caret = $o.caret, $o.$))(this._document.replace(ordered, text));
        let line = ordered.start.line;
        let linesRemoved = ordered.end.line - ordered.start.line;
        let linesInserted = caret.line - ordered.start.line;
        this._document = next;
        this._selection = new CodeRange(caret);
        let edit = new CodeEdit(ordered, removed, text, before, this._selection);
        this.history.record(edit);
        this.highlighter.lineChanged(this._document, line, linesInserted, linesRemoved);
        this.changed?.(edit);
        this.selectionChanged?.(this._selection);
        this._desiredColumn = -1;
        return true;
    }

    insert(text: string) {
        return this.apply(this._selection, text);
    }

    type(c: string) {
        if (this.readOnly) return false;
        let rules = this.rules;
        if (!this._selection.isEmpty) {
            for (const [open, close] of rules.brackets) {
                if (c !== open) continue;
                let text = this._document.textIn(this._selection);
                return this.apply(this._selection, open + text + close);
            }
            for (const quote of rules.quotes) {
                if (c !== quote) continue;
                let text = this._document.textIn(this._selection);
                return this.apply(this._selection, quote + text + quote);
            }
        }
        let line = this._document.line(this.caret.line);
        let after = this.caret.column < line.length ? line[this.caret.column] : '\0';
        for (const [_, close] of rules.brackets) {
            if (c === close && after === close) {
                this.selection = new CodeRange($eq.withPatch(this.caret, { column: this.caret.column + 1 }));
                return true;
            }
        }
        for (const quote of rules.quotes) {
            if (c === quote && after === quote) {
                this.selection = new CodeRange($eq.withPatch(this.caret, { column: this.caret.column + 1 }));
                return true;
            }
        }
        for (const [open, close] of rules.brackets) {
            if (c !== open) continue;
            if (after === '\0' || (/^\s$/.test(after)) || rules.brackets.some((p) => p[1] === after)) {
                if (!this.apply(this._selection, `${open}${close}`)) return false;
                this.selection = new CodeRange($eq.withPatch(this.caret, { column: this.caret.column - 1 }));
                return true;
            }
        }
        for (const quote of rules.quotes) {
            if (c !== quote) continue;
            let before = this.caret.column > 0 ? line[this.caret.column - 1] : '\0';
            if (CodeDocument.isWordChar(before) || CodeDocument.isWordChar(after)) break;
            if (after === '\0' || (/^\s$/.test(after))) {
                if (!this.apply(this._selection, `${quote}${quote}`)) return false;
                this.selection = new CodeRange($eq.withPatch(this.caret, { column: this.caret.column - 1 }));
                return true;
            }
        }
        return this.apply(this._selection, String(c));
    }

    insertNewLine() {
        if (this.readOnly) return false;
        let rules = this.rules;
        let line = this._document.line(this.caret.line);
        let indent = this._document.indentOf(this.caret.line);
        let step = rules.insertSpaces ? ' '.repeat(rules.indentWidth) : '	';
        let beforeCaret = line.slice(0, Math.min(this.caret.column, line.length)).trimEnd();
        let afterCaret = this.caret.column < line.length ? line.slice(this.caret.column).trimStart() : '';
        let opens = beforeCaret.length > 0 && rules.indentAfter.includes(beforeCaret[beforeCaret.length - 1]);
        let closesNext = afterCaret.length > 0 && rules.outdentOn.includes(afterCaret[0]);
        if (opens && closesNext) {
            if (!this.apply(this._selection, `\n${indent}${step}\n${indent}`)) return false;
            this.selection = new CodeRange(new CodePosition(this.caret.line - 1, indent.length + step.length));
            return true;
        }
        return this.apply(this._selection, '\n' + indent + (opens ? step : ''));
    }

    deleteBackward(motion: CodeMotionValue = 'character') {
        if (this.readOnly) return false;
        if (!this._selection.isEmpty) return this.apply(this._selection, '');
        if (motion === 'word') {
            let start = this.moveTo(this.caret, 'word', 'backward');
            return this.apply(new CodeRange(start, this.caret), '');
        }
        let line = this._document.line(this.caret.line);
        let indent = this._document.indentOf(this.caret.line).length;
        if (this.caret.column > 0 && this.caret.column <= indent && this.rules.insertSpaces) {
            let width = this.rules.indentWidth;
            let back = this.caret.column % width === 0 ? width : this.caret.column % width;
            return this.apply(new CodeRange($eq.withPatch(this.caret, { column: this.caret.column - back }), this.caret), '');
        }
        if (this.caret.column > 0 && this.caret.column < line.length) {
            let before = line[this.caret.column - 1];
            let after = line[this.caret.column];
            let paired = this.rules.brackets.some((p) => p[0] === before && p[1] === after) || this.rules.quotes.includes(before) && before === after;
            if (paired) {
                return this.apply(new CodeRange($eq.withPatch(this.caret, { column: this.caret.column - 1 }), $eq.withPatch(this.caret, { column: this.caret.column + 1 })), '');
            }
        }
        let previous = this._document.previous(this.caret);
        return !$eq.equals(previous, this.caret) && this.apply(new CodeRange(previous, this.caret), '');
    }

    deleteForward(motion: CodeMotionValue = 'character') {
        if (this.readOnly) return false;
        if (!this._selection.isEmpty) return this.apply(this._selection, '');
        let to = motion === 'word' ? this.moveTo(this.caret, 'word', 'forward') : this._document.next(this.caret);
        return !$eq.equals(to, this.caret) && this.apply(new CodeRange(this.caret, to), '');
    }

    indent() {
        if (this.readOnly) return false;
        if (this._selection.isEmpty) {
            if (!this.rules.insertSpaces) return this.apply(this._selection, '	');
            let width = this.rules.indentWidth;
            return this.apply(this._selection, ' '.repeat(width - this.caret.column % width));
        }
        return this.shiftLines(true);
    }

    outdent() {
        return !this.readOnly && this.shiftLines(false);
    }

    shiftLines(add: boolean) {
        let step = this.rules.insertSpaces ? ' '.repeat(this.rules.indentWidth) : '	';
        let first = this._selection.start.line;
        let last = this._selection.end.line;
        if (last > first && this._selection.end.column === 0) last--;
        let lines: string[] = [];
        for (let line = first; line <= last; line++) {
            let text = this._document.line(line);
            if (add) lines.push(text.length === 0 ? text : step + text); else if (text.startsWith(step)) lines.push(text.slice(step.length)); else lines.push((_s => { const _c = ' ' + '\t'; let _i = 0; while (_i < _s.length && _c.includes(_s[_i])) _i++; return _s.slice(_i); })(text).length === text.length ? text : text.slice(1));
        }
        let range = new CodeRange(new CodePosition(first, 0), new CodePosition(last, this._document.line(last).length));
        let anchorShift = add ? step.length : -(Math.trunc(Math.min(step.length, this._document.indentOf(first).length)) | 0);
        if (!this.apply(range, lines.join('\n'))) return false;
        this.selection = new CodeRange(new CodePosition(first, Math.max(0, this._selection.anchor.column + anchorShift)), new CodePosition(last, this._document.line(last).length));
        return true;
    }

    toggleLineComment() {
        let marker: any; 
        if (this.readOnly || !((marker = this.rules.lineComment) != null)) return false;
        let first = this._selection.start.line;
        let last = this._selection.end.line;
        if (last > first && this._selection.end.column === 0) last--;
        let allCommented = true;
        for (let line = first; line <= last; line++) {
            let text = this._document.line(line).trimStart();
            if (text.length === 0) continue;
            if (!text.startsWith(marker)) {
                allCommented = false;
                break;
            }
        }
        let lines: string[] = [];
        for (let line = first; line <= last; line++) {
            let text = this._document.line(line);
            if (allCommented) {
                let at = text.indexOf(marker);
                if (at < 0) {
                    lines.push(text);
                    continue;
                }
                let after = at + marker.length;
                if (after < text.length && text[after] === ' ') after++;
                lines.push(text.slice(0, at) + text.slice(after));
            } else {
                let indent = this._document.indentOf(line);
                lines.push(text.length === 0 ? marker + ' ' : indent + marker + ' ' + text.slice(indent.length));
            }
        }
        let range = new CodeRange(new CodePosition(first, 0), new CodePosition(last, this._document.line(last).length));
        return this.apply(range, lines.join('\n'));
    }

    move(motion: CodeMotionValue, direction: CodeDirectionValue, extend: boolean = false, pageLines: number = 20) {
        if (!extend && !this._selection.isEmpty && motion === 'character') {
            this.selection = new CodeRange(direction === 'forward' ? this._selection.end : this._selection.start);
            return;
        }
        let target = this.moveTo(this.caret, motion, direction, pageLines);
        this.selection = extend ? $eq.withPatch(this._selection, { focus: target }) : new CodeRange(target);
    }

    moveTo(from: CodePosition, motion: CodeMotionValue, direction: CodeDirectionValue, pageLines: number = 20) {
        let forward = direction === 'forward';
        switch (motion) {
            case 'character':
                this._desiredColumn = -1;
                return forward ? this._document.next(from) : this._document.previous(from);
            case 'word':
                this._desiredColumn = -1;
                return this.wordStep(from, forward);
            case 'line':
                {
                    if (this._desiredColumn < 0) this._desiredColumn = from.column;
                    let line = Math.min(Math.max(from.line + (forward ? 1 : -1), 0), this._document.lineCount - 1);
                    let column = Math.min(this._desiredColumn, this._document.line(line).length);
                    return new CodePosition(line, column);
                }
            case 'page':
                {
                    if (this._desiredColumn < 0) this._desiredColumn = from.column;
                    let line = Math.min(Math.max(from.line + (forward ? pageLines : -pageLines), 0), this._document.lineCount - 1);
                    return new CodePosition(line, Math.min(this._desiredColumn, this._document.line(line).length));
                }
            case 'lineBoundary':
                this._desiredColumn = -1;
                return forward ? this._document.lineEnd(from) : this._document.lineStart(from);
            default:
                this._desiredColumn = -1;
                return forward ? this._document.end : CodePosition.start;
        }
    }

    wordStep(from: CodePosition, forward: boolean) {
        let here = this._document.clamp(from);
        let line = this._document.line(here.line);
        if (forward) {
            if (here.column >= line.length) return this._document.next(here);
            let i = here.column;
            if (CodeDocument.isWordChar(line[i])) while (i < line.length && CodeDocument.isWordChar(line[i])) i++; else if (!(/^\s$/.test(line[i]))) while (i < line.length && !CodeDocument.isWordChar(line[i]) && !(/^\s$/.test(line[i]))) i++;
            while (i < line.length && (/^\s$/.test(line[i]))) i++;
            return $eq.withPatch(here, { column: i });
        }
        if (here.column === 0) return this._document.previous(here);
        let back = here.column;
        while (back > 0 && (/^\s$/.test(line[back - 1]))) back--;
        if (back > 0 && CodeDocument.isWordChar(line[back - 1])) while (back > 0 && CodeDocument.isWordChar(line[back - 1])) back--; else while (back > 0 && !CodeDocument.isWordChar(line[back - 1]) && !(/^\s$/.test(line[back - 1]))) back--;
        return $eq.withPatch(here, { column: back });
    }

    selectAll() {
        return this.selection = new CodeRange(CodePosition.start, this._document.end);
    }

    selectWord(at: CodePosition) {
        return this.selection = this._document.wordAt(at);
    }

    selectLine(line: number) {
        let last = Math.min(Math.max(line, 0), this._document.lineCount - 1);
        this.selection = new CodeRange(new CodePosition(last, 0), last + 1 < this._document.lineCount ? new CodePosition(last + 1, 0) : new CodePosition(last, this._document.line(last).length));
    }

    copyText() {
        return this._selection.isEmpty ? this._document.line(this.caret.line) + '\n' : this._document.textIn(this._selection);
    }

    cut() {
        let text = this.copyText();
        if (this._selection.isEmpty) this.selectLine(this.caret.line);
        this.apply(this._selection, '');
        return text;
    }

    undo() {
        let selection: any; if (this.readOnly) return false;
        let next = ($o => (selection = $o.selection, $o.$))(this.history.undo(this._document));
        if (next == null) return false;
        this._document = next;
        this._selection = new CodeRange(next.clamp(selection.anchor), next.clamp(selection.focus));
        this.highlighter.invalidate();
        this.changed?.(null);
        this.selectionChanged?.(this._selection);
        return true;
    }

    redo() {
        let selection: any; if (this.readOnly) return false;
        let next = ($o => (selection = $o.selection, $o.$))(this.history.redo(this._document));
        if (next == null) return false;
        this._document = next;
        this._selection = new CodeRange(next.clamp(selection.anchor), next.clamp(selection.focus));
        this.highlighter.invalidate();
        this.changed?.(null);
        this.selectionChanged?.(this._selection);
        return true;
    }

    findAll(needle: string, matchCase: boolean = false) {
        let matches: CodeRange[] = [];
        if (needle.length === 0) return matches;
        let pin = matchCase ? needle : needle.toLowerCase();
        for (let line = 0; line < this._document.lineCount; line++) {
            let raw = this._document.line(line);
            let text = matchCase ? raw : raw.toLowerCase();
            let at = text.indexOf(pin);
            while (at >= 0) {
                matches.push(new CodeRange(new CodePosition(line, at), new CodePosition(line, at + needle.length)));
                at = at + pin.length <= text.length ? text.indexOf(pin, at + pin.length) : -1;
            }
        }
        return matches;
    }

    findNext(needle: string, matchCase: boolean = false, backward: boolean = false) {
        let matches = this.findAll(needle, matchCase);
        if (matches.length === 0) return null;
        if (backward) {
            for (let i = matches.length - 1; i >= 0; i--) if (CodePosition.opLessOrEqual(matches[i].end, this._selection.start)) return matches[i];
            return matches[matches.length - 1];
        }
        for (const match of matches) if (CodePosition.opGreaterOrEqual(match.start, this._selection.end)) return match;
        return matches[0];
    }

    bracketAtCaret() {
        let caret = this.caret;
        if (caret.column > 0) {
            let behind = $eq.withPatch(caret, { column: caret.column - 1 });
            let match: any; 
            if ((match = this.matchingBracket(behind)) != null) return [behind, match];
        }
        let ahead: any; 
        return (ahead = this.matchingBracket(caret)) != null ? [caret, ahead] : null;
    }

    matchingBracket(at: CodePosition) {
        let here = this._document.clamp(at);
        let line = this._document.line(here.line);
        if (here.column >= line.length) return null;
        let c = line[here.column];
        for (const [open, close] of this.rules.brackets) {
            if (c === open) return this.scanForBracket(here, open, close, true);
            if (c === close) return this.scanForBracket(here, close, open, false);
        }
        return null;
    }

    scanForBracket(from: CodePosition, same: string, other: string, forward: boolean) {
        let depth = 0;
        let position = from;
        while (true) {
            let line = this._document.line(position.line);
            if (position.column < line.length) {
                let c = line[position.column];
                if (c === same) depth++; else if (c === other) {
                    depth--;
                    if (depth === 0) return position;
                }
            }
            let next = forward ? this._document.next(position) : this._document.previous(position);
            if ($eq.equals(next, position)) return null;
            position = next;
        }
    }
}

