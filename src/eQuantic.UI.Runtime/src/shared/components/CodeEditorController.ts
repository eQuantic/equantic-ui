import { $eq, CodeDirectionValue, CodeDocument, CodeEdit, CodeGrid, CodeHighlighter, CodeHistory, CodeKeymap, CodeLanguageRules, CodeLanguages, CodeMotionValue, CodePosition, CodeRange, KeyboardConventionValue, Point, PointerPhaseValue, Rect } from "../runtime-exports";

export class CodeEditorController {
    constructor(text: string = '', language: any = null, props?: any) {
        this._selection = new CodeRange(); this._desiredColumn = -1; this._dragging = false; this._revealVersion = 0; this._composition = null; this._compositionReplaced = ''; this._compositionSelection = new CodeRange(); this._wholeLineCopy = null; this._document = CodeDocument.fromText(text);
        this._selection = new CodeRange(CodePosition.start);
        this.highlighter = new CodeHighlighter(language ?? CodeLanguages.plainText); if (props && typeof props === 'object') Object.assign(this, props);
    }

    _document: CodeDocument;
    _selection: CodeRange;
    _desiredColumn: number;
    static caretWidth: number = 2;
    _dragging: boolean;
    _revealVersion: number;
    _composition: CodeRange | null;
    _compositionReplaced: string;
    _compositionSelection: CodeRange;
    _wholeLineCopy: string | null;

    get document(): CodeDocument {
        return this._document;
    }

    set document(value: CodeDocument) {
        this._document = value;
        this._selection = new CodeRange(this._document.clamp(this._selection.focus));
        this._composition = null;
        this.highlighter.invalidate();
        this.history.clear();
        this._revealVersion++;
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
        this._revealVersion++;
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
    tabMovesFocus: boolean = false;
    grid: CodeGrid = CodeGrid.default;

    get selectionBands(): Rect[] {
        let bands: Rect[] = [];
        if (this._selection.isEmpty) return bands;
        let start = this._selection.start;
        let end = this._selection.end;
        for (let line = start.line; line <= end.line; line++) {
            let from = line === start.line ? start.column : 0;
            let to = line === end.line ? end.column : this._document.line(line).length + 1;
            if (to <= from) continue;
            let at = this.grid.pointOf(line, from);
            bands.push(new Rect(at.x, at.y, Math.fround(Math.fround(to - from) * this.grid.cell.width), this.grid.cell.height));
        }
        return bands;
    }

    get carets(): Rect[] {
        return [this.caretRect(this.caret)];
    }

    get revealVersion(): number {
        return this._revealVersion;
    }

    get composition(): CodeRange | null {
        return this._composition;
    }

    changed: ((codeEdit: CodeEdit | null) => void) | null = null;
    selectionChanged: ((codeRange: CodeRange) => void) | null = null;

    caretRect(position: CodePosition) {
        let at = this.grid.pointOf(position.line, position.column);
        return new Rect(at.x, at.y, CodeEditorController.caretWidth, this.grid.cell.height);
    }

    positionAt(point: Point) {
        let line = (Math.trunc(Math.floor(Math.fround(Math.fround(point.y - this.grid.origin.y) / this.grid.cell.height))) | 0);
        let column = (Math.trunc($eq.math.roundSingle(Math.fround(Math.fround(point.x - this.grid.origin.x) / this.grid.cell.width))) | 0);
        return this._document.clamp(new CodePosition(Math.max(0, line), Math.max(0, column)));
    }

    handleKey(key: string, modifiers: number, convention: KeyboardConventionValue, clipboard: any) {
        return CodeKeymap.handle(this, key, modifiers, convention, clipboard);
    }

    handleText(text: string) {
        if (this.readOnly || text.length === 0) return false;
        this.tabMovesFocus = false;
        let committing = !(this._composition == null);
        this.endComposition();
        let typed = false;
        for (const c of text) typed = $eq.logic.or(typed, this.type(c));
        if (committing) this.history.break();
        return typed;
    }

    setComposition(text: string) {
        if (this.readOnly) return false;
        let current: any; 
        if (!((current = this._composition) != null)) {
            if (text.length === 0) return false;
            this.history.break();
            this._compositionSelection = this._selection;
            let over = new CodeRange(this._document.clamp(this._selection.start), this._document.clamp(this._selection.end));
            this._compositionReplaced = this._document.textIn(over);
            this._composition = this.replaceUnrecorded(over, text);
            return true;
        }
        if (text.length === 0) {
            this.replaceUnrecorded(current, this._compositionReplaced);
            this._composition = null;
            this._selection = new CodeRange(this._document.clamp(this._compositionSelection.anchor), this._document.clamp(this._compositionSelection.focus));
            this._revealVersion++;
            this.selectionChanged?.(this._selection);
            return true;
        }
        this._composition = this.replaceUnrecorded(current, text);
        return true;
    }

    endComposition() {
        let current: any; 
        if (!((current = this._composition) != null)) return;
        this.replaceUnrecorded(current, this._compositionReplaced);
        this._composition = null;
        this._selection = new CodeRange(this._document.clamp(this._compositionSelection.anchor), this._document.clamp(this._compositionSelection.focus));
    }

    replaceUnrecorded(range: CodeRange, text: string) {
        let caret: any; let ordered = new CodeRange(this._document.clamp(range.start), this._document.clamp(range.end));
        let removed = this._document.textIn(ordered);
        let before = this._selection;
        let next = ($o => (caret = $o.caret, $o.$))(this._document.replace(ordered, text));
        let line = ordered.start.line;
        let linesRemoved = ordered.end.line - ordered.start.line;
        let linesInserted = caret.line - ordered.start.line;
        this._document = next;
        this._selection = new CodeRange(caret);
        this.highlighter.lineChanged(this._document, line, linesInserted, linesRemoved);
        this._revealVersion++;
        this._desiredColumn = -1;
        let edit = new CodeEdit(ordered, removed, text, before, this._selection);
        this.changed?.(edit);
        this.selectionChanged?.(this._selection);
        return new CodeRange(ordered.start, caret);
    }

    focusChanged(focused: boolean) {
        this.history.break();
        this.tabMovesFocus = false;
        if (focused) return;
        if (!(this._composition == null)) this.setComposition('');
        this._dragging = false;
    }

    handlePointer(phase: PointerPhaseValue, position: Point, modifiers: number, clicks: number) {
        switch (phase) {
            case 'down':
                {
                    let at = this.positionAt(position);
                    if (clicks >= 3) this.selectLine(at.line); else if (clicks === 2) this.selectWord(at); else if ((modifiers & 1) !== 0) this.selection = new CodeRange(this._selection.anchor, at); else this.selection = new CodeRange(at);
                    this._dragging = clicks < 2;
                    return true;
                }
            case 'move':
                {
                    if (!this._dragging) return false;
                    let at = this.positionAt(position);
                    if ($eq.equals(at, this._selection.focus)) return false;
                    this.selection = new CodeRange(this._selection.anchor, at);
                    return true;
                }
            case 'up':
                this._dragging = false;
                return false;
            default:
                return false;
        }
    }

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
        this._revealVersion++;
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
        if (motion === 'lineBoundary' && this.caret.column > 0) return this.apply(new CodeRange($eq.withPatch(this.caret, { column: 0 }), this.caret), '');
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
        let anchorShift = add ? step.length : -Math.min(step.length, this._document.indentOf(first).length);
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
        if (!this._selection.isEmpty) {
            this._wholeLineCopy = null;
            return this._document.textIn(this._selection);
        }
        let line = this._document.line(this.caret.line) + '\n';
        this._wholeLineCopy = line;
        return line;
    }

    cut() {
        let text = this.copyText();
        if (this._selection.isEmpty) this.selectLine(this.caret.line);
        this.apply(this._selection, '');
        return text;
    }

    paste(text: string) {
        if (this.readOnly || text.length === 0) return false;
        this.tabMovesFocus = false;
        this.endComposition();
        let normalized = CodeDocument.fromText(text).text;
        let line: any; 
        if (this._selection.isEmpty && (line = this._wholeLineCopy) != null && normalized === line) {
            let caret = this.caret;
            let lineStart = new CodePosition(caret.line, 0);
            if (!this.apply(new CodeRange(lineStart), normalized)) return false;
            this.selection = new CodeRange(new CodePosition(caret.line + 1, caret.column));
            return true;
        }
        return this.insert(text);
    }

    undo() {
        let selection: any; if (this.readOnly) return false;
        this.endComposition();
        let next = ($o => (selection = $o.selection, $o.$))(this.history.undo(this._document));
        if (next == null) return false;
        this._document = next;
        this._revealVersion++;
        this._selection = new CodeRange(next.clamp(selection.anchor), next.clamp(selection.focus));
        this.highlighter.invalidate();
        this.changed?.(null);
        this.selectionChanged?.(this._selection);
        return true;
    }

    redo() {
        let selection: any; if (this.readOnly) return false;
        this.endComposition();
        let next = ($o => (selection = $o.selection, $o.$))(this.history.redo(this._document));
        if (next == null) return false;
        this._document = next;
        this._revealVersion++;
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

