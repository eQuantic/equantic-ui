import { $eq, CodeCaret, CodeChange, CodeCommandValue, CodeCompletion, CodeDiagnostics, CodeDirectionValue, CodeDocument, CodeEdit, CodeGrid, CodeHighlighter, CodeHistory, CodeHoverSession, CodeKeyBinding, CodeKeymap, CodeLanguageRules, CodeLanguages, CodeLineAdvances, CodeLineCells, CodeMotionValue, CodePosition, CodeRange, CodeSignatureHelpSession, CodeStep, KeyboardConventionValue, KeyChord, Point, PointerPhaseValue, Rect } from "../runtime-exports";

export class CodeEditorController {
    constructor(text: string = '', language: any = null) {
        this._document = null!;
        this._selection = CodeRange.$zero();
        this._readOnly = false;
        this._others = [];
        this.highlighter = null!;
        this.history = new CodeHistory();
        this.completion = null!;
        this.diagnostics = null!;
        this.hover = null!;
        this.signatureHelp = null!;
        this.pendingChord = null;
        this.keyBindings = [];
        this.tabMovesFocus = false;
        this.changed = null;
        this.selectionChanged = null;
        this._desiredAdvance = -1;
        this._cells = $eq.collections.dictionary();
        this._advances = $eq.collections.dictionary();
        this._widths = null;
        this._widthsTabs = 0;
        this._widest = 0;
        this.grid = CodeGrid.default;
        this._dragging = false;
        this._revealVersion = 0;
        this._focusVersion = 0;
        this._composition = null;
        this._compositionReplaced = '';
        this._compositionSelection = CodeRange.$zero();
        this._step = null;
        this._stepMoved = false;
        this._ahead = null;
        this._unheard = [];
        this._raising = false;
        this._unheardMoved = false;
        this._copied = null;
        this._copiedText = '';
        this._wholeLineCopy = null;
        this._occurrence = null;
        this._column = null;
        this._document = CodeDocument.fromText(text);
        this._selection = new CodeRange(CodePosition.start);
        this.highlighter = new CodeHighlighter(language ?? CodeLanguages.plainText);
        this.completion = new CodeCompletion(this);
        this.diagnostics = new CodeDiagnostics(this);
        this.hover = new CodeHoverSession(this);
        this.signatureHelp = new CodeSignatureHelpSession(this);
    }

    _document!: CodeDocument;
    _selection!: CodeRange;
    _readOnly!: boolean;
    _others!: CodeCaret[];
    highlighter!: CodeHighlighter;
    history!: CodeHistory;
    completion!: CodeCompletion;
    diagnostics!: CodeDiagnostics;
    hover!: CodeHoverSession;
    signatureHelp!: CodeSignatureHelpSession;
    pendingChord!: KeyChord | null;
    keyBindings!: CodeKeyBinding[];
    tabMovesFocus!: boolean;
    changed!: ((codeChange: CodeChange) => void) | null;
    selectionChanged!: ((codeRange: CodeRange) => void) | null;
    _desiredAdvance!: number;
    _cells!: any;
    _advances!: any;
    _widths!: number[] | null;
    _widthsTabs!: number;
    _widest!: number;
    grid!: CodeGrid;
    _dragging!: boolean;
    _revealVersion!: number;
    _focusVersion!: number;
    _composition!: CodeRange | null;
    _compositionReplaced!: string;
    _compositionSelection!: CodeRange;
    _step!: CodeEdit[] | null;
    _stepMoved!: boolean;
    _ahead!: CodePosition | null;
    _unheard!: CodeChange[];
    _raising!: boolean;
    _unheardMoved!: boolean;
    _copied!: string[] | null;
    _copiedText!: string;
    _wholeLineCopy!: string | null;
    _occurrence!: string | null;
    _column!: CodeCaret | null;
    static caretWidth: number = 2;

    get document(): CodeDocument {
        return this._document;
    }

    set document(value: CodeDocument) {
        this.completion.dismiss();
        this._document = value;
        this._widths = null;
        this._selection = new CodeRange(this._document.clamp(this._selection.focus));
        this._others.splice(0);
        this._composition = null;
        this.highlighter.invalidate();
        this.history.clear();
        this.diagnostics.reset();
        this._revealVersion++;
        this.raise(new CodeChange([], true), false);
    }

    get selection(): CodeRange {
        return this._selection;
    }

    set selection(value: CodeRange) {
        let had = this._others.length > 0;
        this._others.splice(0);
        this.select(value, false, had);
    }

    get selections(): CodeRange[] {
        let all = ($eq.collections.listCapacity(this._others.length + 1), [this._selection]);
        for (const other of this._others) all.push(other.selection);
        return all;
    }

    set selections(value: CodeRange[]) {
        if ($eq.collections.count(value) === 0) return;
        this._others.splice(0);
        for (let i = 1; i < $eq.collections.count(value); i++) this._others.push(new CodeCaret(this.onElementEdges($eq.collections.item(value, i)), -1));
        this._occurrence = null;
        this.placed(new CodeCaret($eq.collections.item(value, 0), -1));
    }

    get caret(): CodePosition {
        return this._selection.focus;
    }

    get rules(): CodeLanguageRules {
        return this.highlighter.language.rules;
    }

    get readOnly(): boolean {
        return this._readOnly;
    }

    set readOnly(value: boolean) {
        this._readOnly = value;
        if (value) this.completion.dismiss();
    }

    get widestLine(): number {
        let tabSize = this.rules.indentWidth;
        if (this._widths == null || this._widthsTabs !== tabSize) {
            this._widths = ($eq.collections.listCapacity(this._document.lineCount), []);
            for (let line = 0; line < this._document.lineCount; line++) this._widths.push(CodeLineCells.widthOf(this._document.line(line), tabSize));
            this._widthsTabs = tabSize;
            this._widest = CodeEditorController.widest(this._widths);
        }
        return this._widest;
    }

    get carets(): Rect[] {
        let carets = ($eq.collections.listCapacity(this._others.length + 1), [this.caretRect(this.caret)]);
        for (const other of this._others) carets.push(this.caretRect(other.selection.focus));
        return carets;
    }

    get revealVersion(): number {
        return this._revealVersion;
    }

    get focusVersion(): number {
        return this._focusVersion;
    }

    get composition(): CodeRange | null {
        return this._composition;
    }

    get indentStep(): string {
        return this.rules.insertSpaces ? ' '.repeat(this.rules.indentWidth) : '\t';
    }

    get hasOtherCarets(): boolean {
        return this._others.length > 0;
    }

    select(value: CodeRange, keepCell: boolean, moved: boolean = false) {
        if (!keepCell) this._desiredAdvance = -1;
        let next = this.onElementEdges(value);
        if ($eq.equals(next, this._selection) && !moved) return;
        this._occurrence = null;
        this._selection = next;
        this._revealVersion++;
        if (!(this._step == null)) {
            this._stepMoved = true;
            return;
        }
        this.history.break();
        this.selectionChanged?.(next);
    }

    merge() {
        if (this._others.length === 0) return;
        let carets = [...$eq.collections.listCopy(this._others), new CodeCaret(this._selection, this._desiredAdvance)];
        let primary = carets.length - 1;
        let order: number[] = [];
        for (let i = 0; i < carets.length; i++) order.push(i);
        $eq.collections.listSortBy(order, (a: number, b: number) => carets[a].selection.start.compareTo(carets[b].selection.start), 'System.Comparison`1[System.Int32]');
        let merged: CodeCaret[] = [];
        let primaryAt = -1;
        for (const i of order) {
            let caret = carets[i];
            if (merged.length > 0) {
                let last = merged[merged.length - 1].selection;
                let next = caret.selection;
                let meets = last.isEmpty || next.isEmpty ? CodePosition.opLessOrEqual(next.start, last.end) : CodePosition.opLessThan(next.start, last.end);
                if (meets) {
                    let end = CodePosition.opGreaterThan(next.end, last.end) ? next.end : last.end;
                    let keeps = i === primary ? next : merged.length - 1 === primaryAt ? last : next;
                    let backwards = CodePosition.opLessThan(keeps.focus, keeps.anchor);
                    let range = backwards ? new CodeRange(end, last.start) : new CodeRange(last.start, end);
                    merged[merged.length - 1] = new CodeCaret(range, i === primary ? caret.advance : merged[merged.length - 1].advance);
                    if (i === primary) primaryAt = merged.length - 1;
                    continue;
                }
            }
            if (i === primary) primaryAt = merged.length;
            merged.push(caret);
        }
        this._others.splice(0);
        for (let i = 0; i < merged.length; i++) {
            if (i === primaryAt) {
                this._selection = merged[i].selection;
                this._desiredAdvance = merged[i].advance;
            } else this._others.push(merged[i]);
        }
    }

    onElementEdges(value: CodeRange) {
        let anchor = this._document.clamp(value.anchor);
        let focus = this._document.clamp(value.focus);
        let backwards = !$eq.equals(new CodeRange(anchor, focus).start, anchor);
        return $eq.equals(anchor, focus) ? new CodeRange(this.boundary(anchor, false)) : new CodeRange(this.boundary(anchor, backwards), this.boundary(focus, !backwards));
    }

    widthsChanged(line: number, linesInserted: number, linesRemoved: number) {
        if (this._widths == null) return;
        let old = this._widths;
        let gone = Math.min(linesRemoved + 1, old.length - line);
        let lostTheWidest = false;
        for (let i = line; i < line + gone; i++) if (old[i] >= this._widest) lostTheWidest = true;
        if (linesInserted === linesRemoved && gone === linesRemoved + 1) {
            let widestHere = 0;
            for (let i = line; i <= line + linesInserted; i++) {
                let width = CodeLineCells.widthOf(this._document.line(i), this._widthsTabs);
                old[i] = width;
                if (width > widestHere) widestHere = width;
            }
            if (lostTheWidest && widestHere < this._widest) this._widest = CodeEditorController.widest(old); else if (widestHere > this._widest) this._widest = widestHere;
            return;
        }
        let next: number[] = ($eq.collections.listCapacity(old.length - gone + linesInserted + 1), []);
        for (let i = 0; i < line; i++) next.push(old[i]);
        let measuredWidest = 0;
        for (let i = line; i <= line + linesInserted; i++) {
            let width = CodeLineCells.widthOf(this._document.line(i), this._widthsTabs);
            if (width > measuredWidest) measuredWidest = width;
            next.push(width);
        }
        for (let i = line + gone; i < old.length; i++) next.push(old[i]);
        this._widths = next;
        if (lostTheWidest) this._widest = CodeEditorController.widest(next); else if (measuredWidest > this._widest) this._widest = measuredWidest;
    }

    static widest(widths: number[]) {
        let widest = 0;
        for (const width of widths) if (width > widest) widest = width;
        return widest;
    }

    cellsOf(line: number) {
        let text = this._document.line(line);
        let tabSize = this.rules.indentWidth;
        let cells: any; 
        if ((($0) => ($0.has(line) ? ((cells = $0.get(line)), true) : ((cells = null), false)))(this._cells) && cells.text === text && cells.tabSize === tabSize) return cells;
        cells = new CodeLineCells(text, tabSize);
        $eq.mapSet(this._cells, line, cells);
        return cells;
    }

    advancesOf(line: number, measure: (value: string) => number) {
        let cells = this.cellsOf(line);
        let advances: any; 
        if ((($0) => ($0.has(line) ? ((advances = $0.get(line)), true) : ((advances = null), false)))(this._advances) && advances.text === cells.text && advances.measure === measure) return advances;
        advances = new CodeLineAdvances(cells, measure);
        $eq.mapSet(this._advances, line, advances);
        return advances;
    }

    advanceOf(line: number, column: number) {
        let measure: any; 
        return (measure = this.grid.measure) != null ? this.advancesOf(line, measure).of(column) : Math.fround(this.cellsOf(line).cellOf(column));
    }

    columnAtAdvance(line: number, advance: number) {
        let measure: any; 
        return (measure = this.grid.measure) != null ? this.advancesOf(line, measure).columnAt(advance) : this.cellsOf(line).columnAt(advance);
    }

    advanceAtX(x: number) {
        return this.grid.measure == null ? Math.fround(Math.fround(x - this.grid.origin.x) / this.grid.cell.width) : Math.fround(x - this.grid.origin.x);
    }

    xAtAdvance(advance: number) {
        return this.grid.measure == null ? Math.fround(this.grid.origin.x + Math.fround(advance * this.grid.cell.width)) : Math.fround(this.grid.origin.x + advance);
    }

    widthOf(line: number) {
        return this.advanceOf(line, this._document.line(line).length);
    }

    topOf(line: number) {
        return Math.fround(this.grid.origin.y + Math.fround(Math.fround(this.grid.rowOf(line)) * this.grid.cell.height));
    }

    before(position: CodePosition) {
        let here = this._document.clamp(position);
        if (here.column > 0) return $eq.withPatch(here, { column: this.cellsOf(here.line).previous(here.column) });
        if (here.line === 0) return CodePosition.start;
        return new CodePosition(here.line - 1, this._document.line(here.line - 1).length);
    }

    boundary(position: CodePosition, after: boolean) {
        let cells = this.cellsOf(position.line);
        if (position.column <= 0 || position.column >= cells.text.length) return position;
        let element = cells.elementAt(cells.indexOf(position.column));
        if (element.start === position.column) return position;
        return $eq.withPatch(position, { column: after ? element.end : element.start });
    }

    after(position: CodePosition) {
        let here = this._document.clamp(position);
        if (here.column < this._document.line(here.line).length) return $eq.withPatch(here, { column: this.cellsOf(here.line).next(here.column) });
        if (here.line === this._document.lineCount - 1) return here;
        return new CodePosition(here.line + 1, 0);
    }

    selectionBandsIn(first: number, last: number) {
        let bands: Rect[] = [];
        for (const selection of this.selectionsIn(first, last)) this.bandsOf(selection, first, last, bands);
        return bands;
    }

    selectionsIn(first: number, last: number) {
        let all: CodeRange[] = [];
        let placed = false;
        for (const other of this._others) {
            if (!placed && CodePosition.opLessThan(this._selection.start, other.selection.start)) {
                if (CodeEditorController.reaches(this._selection, first, last)) all.push(this._selection);
                placed = true;
            }
            if (CodeEditorController.reaches(other.selection, first, last)) all.push(other.selection);
        }
        if (!placed && CodeEditorController.reaches(this._selection, first, last)) all.push(this._selection);
        return all;
    }

    caretLinesIn(first: number, last: number) {
        let lines: number[] = [];
        for (const selection of this.selectionsIn(first, last)) {
            let line = selection.focus.line;
            if (line < first || line > last || lines.length > 0 && lines[lines.length - 1] === line) continue;
            lines.push(line);
        }
        return lines;
    }

    static reaches(selection: CodeRange, first: number, last: number) {
        return selection.end.line >= first && selection.start.line <= last;
    }

    bandsOf(selection: CodeRange, first: number, last: number, bands: Rect[]) {
        if (selection.isEmpty) return;
        let start = selection.start;
        let end = selection.end;
        for (let line = Math.max(start.line, first); line <= Math.min(end.line, last); line++) {
            let from = line === start.line ? start.column : 0;
            let rows: any; 
            if ((rows = this.grid.rows) != null && !rows.isVisible(line)) continue;
            let fromAdvance = this.advanceOf(line, from);
            let toAdvance = line === end.line ? this.advanceOf(line, end.column) : this.grid.measure == null ? Math.fround(this.cellsOf(line).width + 1) : Math.fround(this.advanceOf(line, this._document.line(line).length) + this.grid.cell.width);
            if (toAdvance <= fromAdvance) continue;
            let width = this.grid.measure == null ? Math.fround(Math.fround(toAdvance - fromAdvance) * this.grid.cell.width) : Math.fround(toAdvance - fromAdvance);
            bands.push(new Rect(this.xAtAdvance(fromAdvance), this.topOf(line), width, this.grid.cell.height));
        }
    }

    requestFocus() {
        return this._focusVersion++;
    }

    caretRect(position: CodePosition) {
        return new Rect(this.xAtAdvance(this.advanceOf(position.line, position.column)), this.topOf(position.line), CodeEditorController.caretWidth, this.grid.cell.height);
    }

    positionAt(point: Point) {
        let rows = this.grid.rows?.rowCount ?? this._document.lineCount;
        if (point.y >= Math.fround(this.grid.origin.y + Math.fround(Math.fround(rows) * this.grid.cell.height))) return this._document.end;
        let target = this._document.clamp(new CodePosition(Math.max(0, this.grid.lineAt(point.y)), 0)).line;
        return new CodePosition(target, this.columnAtAdvance(target, this.advanceAtX(point.x)));
    }

    characterAt(point: Point) {
        let rows = this.grid.rows?.rowCount ?? this._document.lineCount;
        if (point.x < this.grid.origin.x || point.y < this.grid.origin.y) return null;
        let row = (Math.trunc(Math.floor(Math.fround(Math.fround(point.y - this.grid.origin.y) / this.grid.cell.height))) | 0);
        if (row >= rows) return null;
        let map: any; 
        if ((map = this.grid.rows) != null && map.rowAt(row).kind !== 'line') return null;
        let line = this.grid.lineAt(point.y);
        if (line < 0 || line >= this._document.lineCount) return null;
        let advance = this.advanceAtX(point.x);
        let measure: any; 
        let column = (measure = this.grid.measure) != null ? this.advancesOf(line, measure).columnUnder(advance) : this.cellsOf(line).columnUnder(advance);
        return column < 0 ? null : new CodePosition(line, column);
    }

    handleKey(key: string, modifiers: number, convention: KeyboardConventionValue, clipboard: any) {
        return CodeKeymap.handle(this, key, modifiers, convention, clipboard);
    }

    execute(command: CodeCommandValue, clipboard: any = null) {
        {
            let pasted: any;
            switch (command) {
                case 'cursorLeft':
                    return this.step('character', 'backward', false);
                case 'cursorLeftSelect':
                    return this.step('character', 'backward', true);
                case 'cursorRight':
                    return this.step('character', 'forward', false);
                case 'cursorRightSelect':
                    return this.step('character', 'forward', true);
                case 'cursorUp':
                    return this.step('line', 'backward', false);
                case 'cursorUpSelect':
                    return this.step('line', 'backward', true);
                case 'cursorDown':
                    return this.step('line', 'forward', false);
                case 'cursorDownSelect':
                    return this.step('line', 'forward', true);
                case 'cursorWordLeft':
                    return this.step('word', 'backward', false);
                case 'cursorWordLeftSelect':
                    return this.step('word', 'backward', true);
                case 'cursorWordRight':
                    return this.step('word', 'forward', false);
                case 'cursorWordRightSelect':
                    return this.step('word', 'forward', true);
                case 'cursorLineStart':
                    return this.step('lineBoundary', 'backward', false);
                case 'cursorLineStartSelect':
                    return this.step('lineBoundary', 'backward', true);
                case 'cursorLineEnd':
                    return this.step('lineBoundary', 'forward', false);
                case 'cursorLineEndSelect':
                    return this.step('lineBoundary', 'forward', true);
                case 'cursorDocumentStart':
                    return this.step('documentBoundary', 'backward', false);
                case 'cursorDocumentStartSelect':
                    return this.step('documentBoundary', 'backward', true);
                case 'cursorDocumentEnd':
                    return this.step('documentBoundary', 'forward', false);
                case 'cursorDocumentEndSelect':
                    return this.step('documentBoundary', 'forward', true);
                case 'cursorPageUp':
                    return this.step('page', 'backward', false);
                case 'cursorPageUpSelect':
                    return this.step('page', 'backward', true);
                case 'cursorPageDown':
                    return this.step('page', 'forward', false);
                case 'cursorPageDownSelect':
                    return this.step('page', 'forward', true);
                case 'selectAll':
                    this.selectAll();
                    return true;
                case 'newLine':
                    return this.insertNewLine();
                case 'deleteLeft':
                    return this.deleteBackward('character');
                case 'deleteRight':
                    return this.deleteForward('character');
                case 'deleteWordLeft':
                    return this.deleteBackward('word');
                case 'deleteWordRight':
                    return this.deleteForward('word');
                case 'deleteToLineStart':
                    return this.deleteBackward('lineBoundary');
                case 'indent':
                    return !this.readOnly && !this.tabMovesFocus && this.indent();
                case 'outdent':
                    return !this.readOnly && !this.tabMovesFocus && this.outdent();
                case 'toggleLineComment':
                    return this.toggleLineComment();
                case 'undo':
                    return this.undo();
                case 'redo':
                    return this.redo();
                case 'moveLinesUp':
                    return this.moveLines('backward');
                case 'moveLinesDown':
                    return this.moveLines('forward');
                case 'copyLinesUp':
                    return this.copyLines('backward');
                case 'copyLinesDown':
                    return this.copyLines('forward');
                case 'deleteLines':
                    return this.deleteLines();
                case 'joinLines':
                    return this.joinLines();
                case 'sortLinesAscending':
                    return this.sortLines(false);
                case 'sortLinesDescending':
                    return this.sortLines(true);
                case 'insertLineAbove':
                    return this.insertLine('backward');
                case 'insertLineBelow':
                    return this.insertLine('forward');
                case 'addCursorAbove':
                    return this.addCursor('backward');
                case 'addCursorBelow':
                    return this.addCursor('forward');
                case 'addNextOccurrence':
                    return this.addNextOccurrence();
                case 'selectAllOccurrences':
                    return this.selectAllOccurrences();
                case 'removeSecondaryCursors':
                    return this.droppingAnUnshownList(this.removeSecondaryCursors());
                case 'copy':
                    if (clipboard == null) return false;
                    clipboard.write(this.copyText());
                    return true;
                case 'cut':
                    if (clipboard == null) return false;
                    clipboard.write(this.copyText());
                    this.cut();
                    return true;
                case 'paste':
                    if (clipboard == null) return false;
                    if ((($v37776) => (($v37776 != null && $v37776.length > 0) && (pasted = $v37776, true)))(clipboard.read())) this.paste(pasted);
                    return true;
                case 'completionShow':
                    return this.completion.invoke();
                case 'completionNext':
                    return this.completion.isOpen && this.completion.move(1);
                case 'completionPrevious':
                    return this.completion.isOpen && this.completion.move(-1);
                case 'completionNextPage':
                    return this.completion.isOpen && this.completion.movePage(1);
                case 'completionPreviousPage':
                    return this.completion.isOpen && this.completion.movePage(-1);
                case 'completionAccept':
                    return this.completion.isOpen && this.completion.accept();
                case 'completionAcceptChangingText':
                    return this.completion.isOpen && this.completion.acceptChangesText && this.completion.accept();
                case 'completionHide':
                    return this.completion.isOpen && this.completion.dismiss();
                case 'signatureHelpShow':
                    return this.signatureHelp.invoke();
                case 'signatureHelpNext':
                    return !this.hover.isOpen && this.signatureHelp.move(1);
                case 'signatureHelpPrevious':
                    return !this.hover.isOpen && this.signatureHelp.move(-1);
                case 'signatureHelpHide':
                    return this.droppingAnUnshownList(this.signatureHelp.dismiss());
                case 'hoverShow':
                    return this.hover.showAt(this.caret);
                case 'hoverHide':
                    return this.droppingAnUnshownList(this.hover.dismiss());
                case 'problemNext':
                    return !(this.diagnostics.next(false) == null);
                case 'problemPrevious':
                    return !(this.diagnostics.next(true) == null);
                case 'releaseKeyboard':
                    this.completion.dismiss();
                    this.tabMovesFocus = true;
                    return false;
                default:
                    return false;
            }
        }
    }

    droppingAnUnshownList(closed: boolean) {
        if (closed && !this.completion.isOpen) this.completion.dismiss();
        return closed;
    }

    canExecute(command: CodeCommandValue) {
        return (() => {let help: any;  const $s = command; if (($s === 'none' || $s === 'releaseKeyboard')) return false; if ((($s === 'newLine' || $s === 'cut') || $s === 'paste')) return !this.readOnly; if ((($s === 'deleteLeft' || $s === 'deleteWordLeft') || $s === 'deleteToLineStart')) return !this.readOnly && (this._others.length > 0 || !this._selection.isEmpty || !$eq.equals(this.caret, CodePosition.start)); if (($s === 'deleteRight' || $s === 'deleteWordRight')) return !this.readOnly && (this._others.length > 0 || !this._selection.isEmpty || !$eq.equals(this.caret, this._document.end)); if ($s === 'toggleLineComment') return !this.readOnly && !(this.rules.lineComment == null); if (($s === 'indent' || $s === 'outdent')) return !this.readOnly && !this.tabMovesFocus; if ($s === 'undo') return !this.readOnly && this.history.canUndo; if ($s === 'redo') return !this.readOnly && this.history.canRedo; if ($s === 'completionShow') return !this.readOnly && $eq.collections.count(this.completion.providers) > 0; if (((($s === 'completionNext' || $s === 'completionPrevious') || $s === 'completionNextPage') || $s === 'completionPreviousPage')) return this.completion.isOpen && $eq.collections.count(this.completion.items) > 1; if (($s === 'completionAccept' || $s === 'completionHide')) return this.completion.isOpen; if ($s === 'completionAcceptChangingText') return this.completion.isOpen && this.completion.acceptChangesText; if ($s === 'signatureHelpShow') return $eq.collections.count(this.signatureHelp.providers) > 0; if (($s === 'signatureHelpNext' || $s === 'signatureHelpPrevious')) return !this.hover.isOpen && (help = this.signatureHelp.help) != null && $eq.collections.count(help.signatures) > 1; if ($s === 'signatureHelpHide') return this.signatureHelp.isOpen; if ($s === 'hoverShow') return this.hover.canShowAt(this.caret); if ($s === 'hoverHide') return this.hover.isOpen; if (($s === 'problemNext' || $s === 'problemPrevious')) return $eq.collections.count(this.diagnostics.items) > 0; if ($s === 'moveLinesUp') return !this.readOnly && (this._others.length > 0 || this.selectedLines()[0] > 0); if ($s === 'moveLinesDown') return !this.readOnly && (this._others.length > 0 || this._selection.end.line < this._document.lineCount - 1); if (((($s === 'copyLinesUp' || $s === 'copyLinesDown') || $s === 'insertLineAbove') || $s === 'insertLineBelow')) return !this.readOnly; if ($s === 'deleteLines') return !this.readOnly && (this._document.lineCount > 1 || this._document.line(0).length > 0); if ($s === 'joinLines') return !this.readOnly && (this._others.length > 0 || this.canJoin()); if ($s === 'removeSecondaryCursors') return this._others.length > 0; if ($s === 'sortLinesAscending') return !this.readOnly && (this._others.length > 0 || this.unsorted(false)); if ($s === 'sortLinesDescending') return !this.readOnly && (this._others.length > 0 || this.unsorted(true)); return true; })();
    }

    unsorted(descending: boolean) {
        let [first, last] = this.selectedLines();
        return !this.linesInOrder(first, last, descending);
    }

    canJoin() {
        let [first, last] = this.joinedLines();
        return last > first || first < this._document.lineCount - 1;
    }

    joinedLines(): [number, number] {
        return [this._selection.start.line, this._selection.end.line];
    }

    step(motion: CodeMotionValue, direction: CodeDirectionValue, extend: boolean) {
        this.move(motion, direction, extend);
        return true;
    }

    handleText(text: string) {
        if (this.readOnly || text.length === 0) return false;
        this.tabMovesFocus = false;
        let committing = !(this._composition == null);
        this.endComposition();
        if (text.length === 1 && this.completion.acceptsOn(text[0])) this.completion.accept();
        let typed = false;
        let starts = $eq.text.textElementStarts(text);
        for (let i = 0; i < starts.length; i++) {
            let end = i + 1 < starts.length ? starts[i + 1] : text.length;
            let element = $eq.text.substring(text, starts[i], end - starts[i]);
            typed = $eq.logic.or(typed, element.length === 1 ? this.type(element[0]) : this.atEveryCaret(() => this.edit(this._selection, element, true)));
        }
        if (committing) this.history.break();
        if (typed) {
            this.completion.typed(text[text.length - 1]);
            this.signatureHelp.typed(text[text.length - 1]);
        }
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
        let ordered = new CodeRange(this._document.clamp(range.start), this._document.clamp(range.end));
        let removed = this._document.textIn(ordered);
        let before = this._selection;
        let caret: any; 
        let next = ($o => (caret = $o.caret, $o.$))(this._document.replace(ordered, text));
        this._document = next;
        this._selection = new CodeRange(caret);
        this._revealVersion++;
        this._desiredAdvance = -1;
        let edit = new CodeEdit(ordered, removed, next.textIn(new CodeRange(ordered.start, caret)), before, this._selection, false);
        this.took(edit);
        if (this._others.length > 0) this.carryOthers(edit);
        this.raise(new CodeChange([edit]));
        return new CodeRange(ordered.start, caret);
    }

    focusChanged(focused: boolean) {
        this.history.break();
        this.tabMovesFocus = false;
        if (focused) return;
        this.completion.dismiss();
        this.hover.dismiss();
        this.signatureHelp.dismiss();
        this.pendingChord = null;
        if (!(this._composition == null)) this.setComposition('');
        this._dragging = false;
    }

    handlePointer(phase: PointerPhaseValue, position: Point, modifiers: number, clicks: number) {
        switch (phase) {
            case 'down':
                {
                    this.hover.dismiss();
                    this.pendingChord = null;
                    if (this.onPlaceholder(position)) return false;
                    let at = this.positionAt(position);
                    let alt = (modifiers & 2) !== 0;
                    if (alt && clicks < 2 && (modifiers & 1) !== 0) {
                        let anchor = this._selection.anchor;
                        this._column = new CodeCaret(new CodeRange(anchor), this.advanceOf(anchor.line, anchor.column));
                        this.selectColumn(at.line, this.advanceAtX(position.x));
                        this._dragging = true;
                        return true;
                    }
                    if (alt && clicks < 2) {
                        this.toggleCaret(at);
                        this._dragging = true;
                        return true;
                    }
                    if (alt && clicks === 2) {
                        this._others.push(new CodeCaret(this._selection, this._desiredAdvance));
                        this.placed(new CodeCaret(this.wordRange(at), -1));
                        return true;
                    }
                    if (clicks >= 3) this.selectLine(at.line); else if (clicks === 2) this.selectWord(at); else if ((modifiers & 1) !== 0) this.selection = new CodeRange(this._selection.anchor, at); else this.selection = new CodeRange(at);
                    this._dragging = clicks < 2;
                    return true;
                }
            case 'move':
                {
                    if (!this._dragging || this.onPlaceholder(position)) return false;
                    let at = this.positionAt(position);
                    if (!(this._column == null)) {
                        this.selectColumn(at.line, this.advanceAtX(position.x));
                        return true;
                    }
                    if ($eq.equals(at, this._selection.focus)) return false;
                    this.select(new CodeRange(this._selection.anchor, at), false);
                    return true;
                }
            case 'up':
            case 'cancel':
                this._dragging = false;
                this._column = null;
                return false;
            case 'hover':
                this.hover.pointerAt(this.characterAt(position));
                return false;
            case 'exit':
                this.hover.pointerLeft();
                return false;
            default:
                return false;
        }
    }

    onPlaceholder(point: Point) {
        let rows: any; 
        if (!((rows = this.grid.rows) != null)) return false;
        let row = (Math.trunc(Math.floor(Math.fround(Math.fround(point.y - this.grid.origin.y) / this.grid.cell.height))) | 0);
        return row >= 0 && row < rows.rowCount && rows.rowAt(row).kind === 'placeholder';
    }

    apply(range: CodeRange, text: string) {
        return this.edit(range, text, false);
    }

    edit(range: CodeRange, text: string, typed: boolean, after: CodeRange | null = null) {
        if (this.readOnly) return false;
        let ordered = new CodeRange(this._document.clamp(range.start), this._document.clamp(range.end));
        let removed = this._document.textIn(ordered);
        if (removed.length === 0 && text.length === 0) return false;
        let before = this._selection;
        let found = this._step == null ? this.selections : [];
        let caret: any; 
        let next = ($o => (caret = $o.caret, $o.$))(this._document.replace(ordered, text));
        this._document = next;
        let left: any; 
        this._selection = (left = after) != null ? this.onElementEdges(left) : new CodeRange(caret);
        this._revealVersion++;
        let edit = new CodeEdit(ordered, removed, next.textIn(new CodeRange(ordered.start, caret)), before, this._selection, typed);
        this.took(edit);
        this._desiredAdvance = -1;
        let step: any; 
        if ((step = this._step) != null) step.push(edit); else {
            if (this._others.length > 0) this.carryOthers(edit);
            this.history.record(new CodeStep([edit], found, this.selections));
            this.raise(new CodeChange([edit]));
        }
        return true;
    }

    carryOthers(edit: CodeEdit) {
        for (let i = 0; i < this._others.length; i++) this._others[i] = $eq.withPatch(this._others[i], { selection: edit.carry(this._others[i].selection) });
        this.merge();
    }

    asOneStep(edits: () => boolean) {
        if (!(this._step == null)) return edits();
        let step: CodeEdit[] = [];
        let before = this._selection;
        let found = this.selections;
        this._step = step;
        this._stepMoved = false;
        try {
            return edits() || step.length > 0;
        } finally {
            this._step = null;
            this.history.record(new CodeStep(step, found, this.selections));
            if (this._stepMoved) this.history.break();
            if (step.length > 0) this.raise(new CodeChange(this.heard(step, before))); else if (this._stepMoved) this.selectionChanged?.(this._selection);
        }
    }

    atEveryCaret(command: () => boolean) {
        if (this._others.length === 0) return command();
        let primary: any; 
        let carets = ($o => (primary = $o.primary, $o.$))(this.caretsInOrder());
        let units: CodeCaret[][] = [];
        for (const caret of carets) units.push([caret]);
        return this.atEvery(units, primary, 0, (unit: number) => {
            this._selection = units[unit][0].selection;
            this._desiredAdvance = units[unit][0].advance;
            let made = command();
            return [made, [new CodeCaret(this._selection, this._desiredAdvance)]];
        });
    }

    atEveryBlock(lines: (value: CodeRange) => [number, number], touching: boolean, command: (value: CodeCaret[]) => CodeCaret[] | null) {
        let primary: any; 
        let carets = ($o => (primary = $o.primary, $o.$))(this.caretsInOrder());
        let units: CodeCaret[][] = [];
        let last = -1;
        let primaryUnit = 0;
        let primaryIndex = 0;
        for (let i = 0; i < carets.length; i++) {
            let taken = lines(carets[i].selection);
            if (units.length > 0 && taken[0] <= last + (touching ? 1 : 0)) units[units.length - 1].push(carets[i]); else units.push([carets[i]]);
            if (taken[1] > last) last = taken[1];
            if (i === primary) {
                primaryUnit = units.length - 1;
                primaryIndex = units[units.length - 1].length - 1;
            }
        }
        return this.atEvery(units, primaryUnit, primaryIndex, (unit: number) => {
            let left = command(units[unit]);
            return left == null ? [false, units[unit]] : [true, left];
        });
    }

    static blockLines(carets: CodeCaret[]): [number, number] {
        let first = 2147483647;
        let last = -1;
        for (const caret of carets) {
            let taken = CodeEditorController.selectedLinesOf(caret.selection);
            if (taken[0] < first) first = taken[0];
            if (taken[1] > last) last = taken[1];
        }
        return [first, last];
    }

    caretsInOrder() {
        let primary: any;
        const $r = (() => {
            let carets: CodeCaret[] = $eq.collections.listCopy(this._others);
            primary = 0;
            while (primary < carets.length && CodePosition.opLessThan(carets[primary].selection.start, this._selection.start)) primary++;
            carets.splice(primary, 0, new CodeCaret(this._selection, this._desiredAdvance));
            return carets;
        })();
        return { $: $r, primary };
    }

    atEvery(units: CodeCaret[][], primaryUnit: number, primaryIndex: number, run: (value: number) => [boolean, CodeCaret[]]) {
        return this.asOneStep(() => {
            this._others.splice(0);
            let step = this._step!;
            let count = units.length;
            let left: CodeCaret[][] = [];
            let from: number[] = [];
            let to: number[] = [];
            for (let unit = 0; unit < count; unit++) {
                left.push(units[unit]);
                from.push(0);
                to.push(0);
            }
            let any = false;
            let reached = false;
            let earliest = CodePosition.start;
            try {
                for (let unit = count - 1; unit >= 0; unit--) {
                    from[unit] = step.length;
                    to[unit] = step.length;
                    let carets = units[unit];
                    if (reached && CodePosition.opLessThan(earliest, carets[carets.length - 1].selection.end)) {
                        let carried: CodeCaret[] = [];
                        for (const caret of carets) {
                            let range = caret.selection;
                            for (const edit of step) range = edit.carry(range);
                            carried.push(new CodeCaret(range, -1));
                        }
                        left[unit] = carried;
                        continue;
                    }
                    this._ahead = unit + 1 < count ? left[unit + 1][0].selection.start : null;
                    let [made, placed] = run(unit);
                    any = any || made;
                    left[unit] = placed;
                    to[unit] = step.length;
                    for (let i = from[unit]; i < to[unit]; i++) {
                        if (reached && CodePosition.opGreaterOrEqual(step[i].range.start, earliest)) continue;
                        earliest = step[i].range.start;
                        reached = true;
                    }
                }
            } finally {
                this._ahead = null;
            }
            this.place(left, from, to, primaryUnit, primaryIndex);
            return any;
        });
    }

    place(left: CodeCaret[][], from: number[], to: number[], primaryUnit: number, primaryIndex: number) {
        let step = this._step!;
        let placed: CodeCaret[] = [];
        let primary = CodeCaret.$zero();
        let shifted = -1;
        let lines = 0;
        let columns = 0;
        for (let unit = 0; unit < left.length; unit++) {
            let carets = left[unit];
            for (let i = 0; i < carets.length; i++) {
                let anchor = carets[i].selection.anchor;
                let focus = carets[i].selection.focus;
                let caret = $eq.withPatch(carets[i], { selection: new CodeRange(new CodePosition(anchor.line + lines, anchor.column + (anchor.line === shifted ? columns : 0)), new CodePosition(focus.line + lines, focus.column + (focus.line === shifted ? columns : 0))) });
                if (unit === primaryUnit && i === (primaryIndex < carets.length ? primaryIndex : 0)) primary = caret; else placed.push(caret);
            }
            if (unit + 1 === left.length) break;
            let probe = left[unit + 1][0].selection.start;
            let image = probe;
            for (let i = from[unit]; i < to[unit]; i++) image = step[i].carry(new CodeRange(image)).start;
            columns = image.column - probe.column + (image.line === shifted ? columns : 0);
            lines += image.line - probe.line;
            shifted = probe.line;
        }
        this._selection = this.onElementEdges(primary.selection);
        this._desiredAdvance = primary.advance;
        this._others.splice(0);
        for (const caret of placed) this._others.push($eq.withPatch(caret, { selection: this.onElementEdges(caret.selection) }));
        this.merge();
    }

    raise(change: CodeChange, moved: boolean = true) {
        this._unheard.push(change);
        this._unheardMoved = this._unheardMoved || moved;
        if (this._raising) return;
        this._raising = true;
        try {
            for (let i = 0; i < this._unheard.length; i++) this.changed?.(this._unheard[i]);
        } finally {
            this._unheard.splice(0);
            this._raising = false;
        }
        if (!this._unheardMoved) return;
        this._unheardMoved = false;
        this.selectionChanged?.(this._selection);
    }

    heard(made: CodeEdit[], before: CodeRange) {
        let heard: CodeEdit[] = [];
        for (const edit of made) heard.push($eq.withPatch(edit, { selectionBefore: before, selectionAfter: this._selection }));
        return heard;
    }

    insert(text: string) {
        return this.atEveryCaret(() => this.edit(this._selection, text, false));
    }

    type(c: string) {
        return this.atEveryCaret(() => this.typeAtCaret(c));
    }

    typeAtCaret(c: string) {
        if (this.readOnly) return false;
        let rules = this.rules;
        if (!this._selection.isEmpty) {
            for (const [open, close] of rules.brackets) {
                if (c !== open) continue;
                let text = this._document.textIn(this._selection);
                return this.edit(this._selection, open + text + close, true);
            }
            for (const quote of rules.quotes) {
                if (c !== quote) continue;
                let text = this._document.textIn(this._selection);
                return this.edit(this._selection, quote + text + quote, true);
            }
        }
        let line = this._document.line(this.caret.line);
        let after = this.caret.column < line.length ? line[this.caret.column] : '\u0000';
        for (const [, close] of rules.brackets) {
            if (c === close && after === close) {
                this.select(new CodeRange($eq.withPatch(this.caret, { column: this.caret.column + 1 })), false);
                return true;
            }
        }
        for (const quote of rules.quotes) {
            if (c === quote && after === quote) {
                this.select(new CodeRange($eq.withPatch(this.caret, { column: this.caret.column + 1 })), false);
                return true;
            }
        }
        for (const [open, close] of rules.brackets) {
            if (c !== open) continue;
            if (after === '\u0000' || $eq.text.isWhiteSpace(after) || rules.brackets.some((p) => p[1] === after)) {
                if (!this.edit(this._selection, `${open}${close}`, true)) return false;
                this.select(new CodeRange($eq.withPatch(this.caret, { column: this.caret.column - 1 })), false);
                return true;
            }
        }
        for (const quote of rules.quotes) {
            if (c !== quote) continue;
            let before = this.caret.column > 0 ? line[this.caret.column - 1] : '\u0000';
            if (CodeDocument.isWordChar(before) || CodeDocument.isWordChar(after)) break;
            if (after === '\u0000' || $eq.text.isWhiteSpace(after)) {
                if (!this.edit(this._selection, `${quote}${quote}`, true)) return false;
                this.select(new CodeRange($eq.withPatch(this.caret, { column: this.caret.column - 1 })), false);
                return true;
            }
        }
        if (this._selection.isEmpty && rules.outdentOn.includes(c)) {
            let indent = line.slice(0, this.caret.column);
            if (indent.length > 0 && $eq.text.trim(indent).length === 0) {
                let off = this.stepOff(indent);
                return this.edit(new CodeRange(new CodePosition(this.caret.line, 0), this.caret), indent.slice(off) + c, true);
            }
        }
        return this.edit(this._selection, String(c), true);
    }

    insertNewLine() {
        return this.atEveryCaret(this.newLineAtCaret.bind(this));
    }

    newLineAtCaret() {
        if (this.readOnly) return false;
        let rules = this.rules;
        let line = this._document.line(this.caret.line);
        let indent = this._document.indentOf(this.caret.line);
        let step = this.indentStep;
        let afterCaret = this.caret.column < line.length ? $eq.text.trimStart(line.slice(this.caret.column)) : '';
        let opens = this.opensBlock(line.slice(0, Math.min(this.caret.column, line.length)));
        let closesNext = afterCaret.length > 0 && rules.outdentOn.includes(afterCaret[0]);
        if (opens && closesNext) {
            if (!this.edit(this._selection, `\n${indent}${step}\n${indent}`, false)) return false;
            this.select(new CodeRange(new CodePosition(this.caret.line - 1, indent.length + step.length)), false);
            return true;
        }
        return this.edit(this._selection, '\n' + indent + (opens ? step : ''), false);
    }

    opensBlock(text: string) {
        let trimmed = $eq.text.trimEnd(text);
        return trimmed.length > 0 && this.rules.indentAfter.includes(trimmed[trimmed.length - 1]);
    }

    deleteBackward(motion: CodeMotionValue = 'character') {
        return this.atEveryCaret(() => this.deleteBackwardAtCaret(motion));
    }

    deleteBackwardAtCaret(motion: CodeMotionValue) {
        if (this.readOnly) return false;
        if (!this._selection.isEmpty) return this.edit(this._selection, '', false);
        if (motion === 'word') {
            let start = this.moveTo(this.caret, 'word', 'backward');
            return this.edit(new CodeRange(start, this.caret), '', false);
        }
        if (motion === 'lineBoundary' && this.caret.column > 0) return this.edit(new CodeRange($eq.withPatch(this.caret, { column: 0 }), this.caret), '', false);
        let line = this._document.line(this.caret.line);
        let indent = this._document.indentOf(this.caret.line).length;
        if (this.caret.column > 0 && this.caret.column <= indent && this.rules.insertSpaces) {
            let width = this.rules.indentWidth;
            let cells = this.cellsOf(this.caret.line);
            let cell = cells.cellOf(this.caret.column);
            let stop = $eq.num.intRem(cell, width) === 0 ? cell - width : cell - $eq.num.intRem(cell, width);
            return this.edit(new CodeRange($eq.withPatch(this.caret, { column: cells.columnAt(Math.fround(stop)) }), this.caret), '', false);
        }
        if (this.caret.column > 0 && this.caret.column < line.length) {
            let before = line[this.caret.column - 1];
            let after = line[this.caret.column];
            let paired = this.rules.brackets.some((p) => p[0] === before && p[1] === after) || this.rules.quotes.includes(before) && before === after;
            if (paired) {
                return this.edit(new CodeRange($eq.withPatch(this.caret, { column: this.caret.column - 1 }), $eq.withPatch(this.caret, { column: this.caret.column + 1 })), '', false);
            }
        }
        let previous = this.before(this.caret);
        return !$eq.equals(previous, this.caret) && this.edit(new CodeRange(previous, this.caret), '', false);
    }

    deleteForward(motion: CodeMotionValue = 'character') {
        return this.atEveryCaret(() => this.deleteForwardAtCaret(motion));
    }

    deleteForwardAtCaret(motion: CodeMotionValue) {
        if (this.readOnly) return false;
        if (!this._selection.isEmpty) return this.edit(this._selection, '', false);
        let to = motion === 'word' ? this.moveTo(this.caret, 'word', 'forward') : this.after(this.caret);
        let ahead: any; 
        if ((ahead = this._ahead) != null && CodePosition.opGreaterThan(to, ahead)) to = CodePosition.opLessThan(ahead, this.caret) ? this.caret : ahead;
        return !$eq.equals(to, this.caret) && this.edit(new CodeRange(this.caret, to), '', false);
    }

    indent() {
        if (this.readOnly) return false;
        if (!this._selection.isEmpty) return this.shiftLines(true);
        for (const other of this._others) if (!other.selection.isEmpty) return this.shiftLines(true);
        return this.atEveryCaret(this.indentAtCaret.bind(this));
    }

    indentAtCaret() {
        if (!this.rules.insertSpaces) return this.edit(this._selection, '\t', false);
        let width = this.rules.indentWidth;
        let cell = this.cellsOf(this.caret.line).cellOf(this.caret.column);
        return this.edit(this._selection, ' '.repeat(width - $eq.num.intRem(cell, width)), false);
    }

    outdent() {
        return !this.readOnly && this.shiftLines(false);
    }

    shiftLines(add: boolean) {
        this.atEveryBlock(CodeEditorController.selectedLinesOf, false, (carets) => this.shiftBlock(carets, add));
        return true;
    }

    shiftBlock(carets: CodeCaret[], add: boolean) {
        let [first, last] = CodeEditorController.blockLines(carets);
        let step = this.indentStep;
        let changes: number[] = [];
        let changed = false;
        for (let line = first; line <= last; line++) {
            let text = this._document.line(line);
            let change = add ? text.length === 0 ? 0 : step.length : this.stepOff(text);
            changes.push(change);
            changed = changed || change > 0;
        }
        if (!changed) return null;
        for (let line = last; line >= first; line--) {
            let change = changes[line - first];
            if (change === 0) continue;
            let start = new CodePosition(line, 0);
            if (add) this.edit(new CodeRange(start), step, false); else this.edit(new CodeRange(start, new CodePosition(line, change)), '', false);
        }
        let left: CodeCaret[] = [];
        for (const caret of carets) {
            left.push($eq.withPatch(caret, { selection: new CodeRange(CodeEditorController.shiftedBy(caret.selection.anchor, first, last, changes, add), CodeEditorController.shiftedBy(caret.selection.focus, first, last, changes, add)) }));
        }
        return left;
    }

    static shiftedBy(position: CodePosition, first: number, last: number, changes: number[], add: boolean) {
        if (position.line < first || position.line > last) return position;
        let change = changes[position.line - first];
        return new CodePosition(position.line, add ? CodeEditorController.shifted(position.column, 0, 0, change) : CodeEditorController.shifted(position.column, 0, change, 0));
    }

    static shifted(column: number, at: number, removed: number, inserted: number) {
        return column <= at ? column : column < at + removed ? at : column - removed + inserted;
    }

    stepOff(text: string) {
        if (text.length > 0 && text[0] === '\t') return 1;
        let off = 0;
        while (off < text.length && off < this.rules.indentWidth && text[off] === ' ') off++;
        return off;
    }

    toggleLineComment() {
        let marker: any; 
        if (this.readOnly || !((marker = this.rules.lineComment) != null)) return false;
        this.atEveryBlock(CodeEditorController.selectedLinesOf, false, (carets) => this.commentBlock(carets, marker));
        return true;
    }

    commentBlock(carets: CodeCaret[], marker: string) {
        let [first, last] = CodeEditorController.blockLines(carets);
        let allCommented = true;
        for (let line = first; line <= last; line++) {
            let text = $eq.text.trimStart(this._document.line(line));
            if (text.length === 0) continue;
            if (!$eq.text.startsWith(text, marker, 'ordinal')) {
                allCommented = false;
                break;
            }
        }
        let ats: number[] = [];
        let removals: number[] = [];
        let insertions: number[] = [];
        for (let line = first; line <= last; line++) {
            let text = this._document.line(line);
            if (allCommented) {
                let at = $eq.text.indexOf(text, marker, 'ordinal');
                if (at < 0) {
                    ats.push(0);
                    removals.push(0);
                    insertions.push(0);
                    continue;
                }
                let after = at + marker.length;
                if (after < text.length && text[after] === ' ') after++;
                ats.push(at);
                removals.push(after - at);
                insertions.push(0);
            } else {
                ats.push(text.length === 0 ? 0 : this._document.indentOf(line).length);
                removals.push(0);
                insertions.push(marker.length + 1);
            }
        }
        let changed = false;
        for (let line = last; line >= first; line--) {
            let index = line - first;
            if (removals[index] === 0 && insertions[index] === 0) continue;
            changed = true;
            let at = new CodePosition(line, ats[index]);
            this.edit(new CodeRange(at, $eq.withPatch(at, { column: ats[index] + removals[index] })), insertions[index] > 0 ? marker + ' ' : '', false);
        }
        if (!changed) return null;
        let left: CodeCaret[] = [];
        for (const caret of carets) {
            left.push($eq.withPatch(caret, { selection: new CodeRange(CodeEditorController.commented(caret.selection.anchor, first, last, ats, removals, insertions), CodeEditorController.commented(caret.selection.focus, first, last, ats, removals, insertions)) }));
        }
        return left;
    }

    static commented(position: CodePosition, first: number, last: number, ats: number[], removals: number[], insertions: number[]) {
        if (position.line < first || position.line > last) return position;
        let index = position.line - first;
        return new CodePosition(position.line, CodeEditorController.shifted(position.column, ats[index], removals[index], insertions[index]));
    }

    move(motion: CodeMotionValue, direction: CodeDirectionValue, extend: boolean = false, pageLines: number = 20) {
        return this.atEveryCaret(() => {
            this.moveCaret(motion, direction, extend, pageLines);
            return true;
        });
    }

    moveCaret(motion: CodeMotionValue, direction: CodeDirectionValue, extend: boolean, pageLines: number) {
        if (!extend && !this._selection.isEmpty && motion === 'character') {
            this.select(new CodeRange(direction === 'forward' ? this._selection.end : this._selection.start), false);
            return;
        }
        let target = this.moveTo(this.caret, motion, direction, pageLines);
        this.select(extend ? $eq.withPatch(this._selection, { focus: target }) : new CodeRange(target), (motion === 'line' || motion === 'page'));
    }

    moveTo(from: CodePosition, motion: CodeMotionValue, direction: CodeDirectionValue, pageLines: number = 20) {
        let forward = direction === 'forward';
        switch (motion) {
            case 'character':
                this._desiredAdvance = -1;
                return forward ? this.after(from) : this.before(from);
            case 'word':
                this._desiredAdvance = -1;
                return this.wordStep(from, forward);
            case 'line':
                {
                    if (this._desiredAdvance < 0) this._desiredAdvance = this.advanceOf(from.line, from.column);
                    let line = this.visibleLineFrom(from.line, forward ? 1 : -1);
                    return new CodePosition(line, this.columnAtAdvance(line, this._desiredAdvance));
                }
            case 'page':
                {
                    if (this._desiredAdvance < 0) this._desiredAdvance = this.advanceOf(from.line, from.column);
                    let line = this.visibleLineFrom(from.line, forward ? pageLines : -pageLines);
                    return new CodePosition(line, this.columnAtAdvance(line, this._desiredAdvance));
                }
            case 'lineBoundary':
                this._desiredAdvance = -1;
                return forward ? this._document.lineEnd(from) : this._document.lineStart(from);
            default:
                this._desiredAdvance = -1;
                return forward ? this._document.end : CodePosition.start;
        }
    }

    visibleLineFrom(line: number, steps: number) {
        let last = this._document.lineCount - 1;
        let rows: any; 
        if (!((rows = this.grid.rows) != null)) return Math.min(Math.max(line + steps, 0), last);
        let direction = steps < 0 ? -1 : 1;
        let here = line;
        let left = Math.abs(steps);
        for (let next = line + direction; left > 0 && next >= 0 && next <= last; next += direction) {
            if (!rows.isVisible(next)) continue;
            here = next;
            left--;
        }
        return here;
    }

    wordStep(from: CodePosition, forward: boolean) {
        let here = this._document.clamp(from);
        let line = this._document.line(here.line);
        let cells = this.cellsOf(here.line);
        if (forward) {
            if (here.column >= line.length) return this.after(here);
            let i = here.column;
            if (CodeDocument.isWordChar(line[i])) while (i < line.length && CodeDocument.isWordChar(line[i])) i = cells.next(i); else if (!$eq.text.isWhiteSpace(line[i])) while (i < line.length && !CodeDocument.isWordChar(line[i]) && !$eq.text.isWhiteSpace(line[i])) i = cells.next(i);
            while (i < line.length && $eq.text.isWhiteSpace(line[i])) i = cells.next(i);
            return $eq.withPatch(here, { column: i });
        }
        if (here.column === 0) return this.before(here);
        let back = here.column;
        while (back > 0 && $eq.text.isWhiteSpace(line[cells.previous(back)])) back = cells.previous(back);
        if (back > 0 && CodeDocument.isWordChar(line[cells.previous(back)])) while (back > 0 && CodeDocument.isWordChar(line[cells.previous(back)])) back = cells.previous(back); else while (back > 0 && !CodeDocument.isWordChar(line[cells.previous(back)]) && !$eq.text.isWhiteSpace(line[cells.previous(back)])) back = cells.previous(back);
        return $eq.withPatch(here, { column: back });
    }

    selectAll() {
        return this.selection = new CodeRange(CodePosition.start, this._document.end);
    }

    selectWord(at: CodePosition) {
        return this.selection = this.wordRange(at);
    }

    wordRange(at: CodePosition) {
        let word = this._document.wordAt(at);
        if (word.isEmpty) return word;
        let cells = this.cellsOf(word.start.line);
        return new CodeRange($eq.withPatch(word.start, { column: cells.elementAt(cells.indexOf(word.start.column)).start }), $eq.withPatch(word.end, { column: cells.next(word.end.column - 1) }));
    }

    selectLine(line: number) {
        let last = Math.min(Math.max(line, 0), this._document.lineCount - 1);
        this.selection = new CodeRange(new CodePosition(last, 0), last + 1 < this._document.lineCount ? new CodePosition(last + 1, 0) : new CodePosition(last, this._document.line(last).length));
    }

    copyText() {
        if (this._others.length > 0) return this.copyEvery();
        this._copied = null;
        if (!this._selection.isEmpty) {
            this._wholeLineCopy = null;
            return this._document.textIn(this._selection);
        }
        let line = this._document.line(this.caret.line) + '\n';
        this._wholeLineCopy = line;
        return line;
    }

    copyEvery() {
        let carets = ($o => ($o.$))(this.caretsInOrder());
        let lines = true;
        for (const caret of carets) if (!caret.selection.isEmpty) lines = false;
        let pieces: string[] = [];
        for (const caret of carets) pieces.push(lines ? this._document.line(caret.selection.focus.line) : this._document.textIn(caret.selection));
        let text = lines ? $eq.text.join('\n', pieces) + '\n' : $eq.text.join('\n', pieces);
        this._copied = pieces;
        this._copiedText = text;
        this._wholeLineCopy = lines ? text : null;
        return text;
    }

    cut() {
        let text = this.copyText();
        this.atEveryCaret(() => {
            if (this._selection.isEmpty) this.selectLine(this.caret.line);
            return this.edit(this._selection, '', false);
        });
        return text;
    }

    paste(text: string) {
        if (this.readOnly || text.length === 0) return false;
        this.tabMovesFocus = false;
        this.endComposition();
        if (this._others.length > 0) return this.pasteEvery(text);
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

    pasteEvery(text: string) {
        let primary: any; 
        let carets = ($o => (primary = $o.primary, $o.$))(this.caretsInOrder());
        let normalized = CodeDocument.fromText(text).text;
        let pieces: string[] | null = null;
        let copied: any; 
        if ((copied = this._copied) != null && normalized === this._copiedText && copied.length === carets.length) pieces = copied; else {
            let body = normalized.endsWith('\n') ? normalized.slice(0, -1) : normalized;
            let lines = CodeDocument.fromText(body).lines;
            if ($eq.collections.count(lines) === carets.length) {
                pieces = [];
                for (const line of lines) pieces.push(line);
            }
        }
        let whole: any; 
        if (!(pieces == null) && (whole = this._wholeLineCopy) != null && normalized === whole) {
            let piece: any = $eq.collections.dictionary(null, true);
            for (let i = 0; i < carets.length; i++) $eq.mapSet(piece, carets[i].selection, pieces[i]);
            return this.atEveryBlock(CodeEditorController.selectedLinesOf, false, (block) => {
                let first = CodeEditorController.blockLines(block)[0];
                let text = $eq.text.stringBuilder();
                for (const caret of block) text.append($eq.mapGet(piece, caret.selection)).append('\n');
                this.edit(new CodeRange(new CodePosition(first, 0)), text.toString(), false);
                return CodeEditorController.movedBy(block, block.length);
            });
        }
        let units: CodeCaret[][] = [];
        for (const caret of carets) units.push([caret]);
        return this.atEvery(units, primary, 0, (unit: number) => {
            this._selection = units[unit][0].selection;
            this._desiredAdvance = -1;
            let text = pieces == null ? normalized : pieces[unit];
            return [this.edit(this._selection, text, false), [new CodeCaret(this._selection, -1)]];
        });
    }

    replaceAroundEveryCaret(before: number, after: number, text: string) {
        return this.atEveryCaret(() => {
            let caret = this.caret;
            let line = this._document.line(caret.line);
            let range = new CodeRange($eq.withPatch(caret, { column: Math.max(0, caret.column - before) }), $eq.withPatch(caret, { column: Math.min(line.length, caret.column + after) }));
            if (this._document.textIn(range) !== text) return this.edit(range, text, false);
            this.select(new CodeRange(range.end), false);
            return true;
        });
    }

    addCursor(direction: CodeDirectionValue) {
        let carets = ($o => ($o.$))(this.caretsInOrder());
        let up = direction === 'backward';
        let added: CodeCaret[] = [];
        for (const caret of carets) {
            let from = caret.selection.focus;
            let line = this.visibleLineFrom(from.line, up ? -1 : 1);
            if (line === from.line) continue;
            let advance = caret.advance >= 0 ? caret.advance : this.advanceOf(from.line, from.column);
            added.push(new CodeCaret(new CodeRange(new CodePosition(line, this.columnAtAdvance(line, advance))), advance));
        }
        if (added.length === 0) return false;
        let primary = up ? added[0] : added[added.length - 1];
        this._others.splice(0);
        for (const caret of carets) this._others.push(caret);
        for (const caret of added) if (!$eq.equals(caret, primary)) this._others.push(caret);
        this.placed(primary);
        return true;
    }

    addNextOccurrence() {
        if (this._selection.isEmpty) {
            let at: any; 
            let carets = ($o => (at = $o.primary, $o.$))(this.caretsInOrder());
            let words: CodeCaret[] = [];
            for (const caret of carets) words.push(caret.selection.isEmpty ? new CodeCaret(this.wordRange(caret.selection.focus), -1) : caret);
            let primary = words[at];
            if (primary.selection.isEmpty) return false;
            this._others.splice(0);
            for (let i = 0; i < words.length; i++) if (i !== at) this._others.push(words[i]);
            this.placed(primary);
            this._occurrence = this._document.textIn(primary.selection);
            return true;
        }
        let needle = this._document.textIn(this._selection);
        let whole = this._occurrence === needle;
        let matches = this.occurrences(needle, whole);
        let taken: Set<CodeRange> = $eq.collections.hashSet(true);
        for (const selection of this.selections) $eq.collections.setAdd(taken, new CodeRange(selection.start, selection.end));
        let next: CodeRange | null = null;
        for (const match of matches) {
            if (CodePosition.opLessThan(match.start, this._selection.end) || taken.has(match)) continue;
            next = match;
            break;
        }
        if (next == null) {
            for (const match of matches) {
                if (taken.has(match)) continue;
                next = match;
                break;
            }
        }
        let found: any; 
        if (!((found = next) != null)) return true;
        this._others.push(new CodeCaret(this._selection, this._desiredAdvance));
        this.placed(new CodeCaret(found, -1));
        this._occurrence = whole ? needle : null;
        return true;
    }

    selectAllOccurrences() {
        let target = this._selection.isEmpty ? this.wordRange(this.caret) : new CodeRange(this._selection.start, this._selection.end);
        if (target.isEmpty) return false;
        let needle = this._document.textIn(target);
        let whole = this._selection.isEmpty || this._occurrence === needle;
        let matches = this.occurrences(needle, whole);
        if (matches.length === 0) return false;
        let primary = 0;
        for (let i = 0; i < matches.length; i++) {
            if (!$eq.equals(matches[i], target)) continue;
            primary = i;
            break;
        }
        this._others.splice(0);
        for (let i = 0; i < matches.length; i++) if (i !== primary) this._others.push(new CodeCaret(matches[i], -1));
        this.placed(new CodeCaret(matches[primary], -1));
        this._occurrence = whole ? needle : null;
        return true;
    }

    occurrences(needle: string, whole: boolean) {
        let occurrences: CodeRange[] = [];
        for (const match of this.findAll(needle, true)) if (!whole || this.whole(match)) occurrences.push(match);
        return occurrences;
    }

    whole(match: CodeRange) {
        let line = this._document.line(match.start.line);
        let start = match.start.column;
        let end = match.end.column;
        let cutsStart = start > 0 && start < line.length && CodeDocument.isWordChar(line[start - 1]) && CodeDocument.isWordChar(line[start]);
        let cutsEnd = end > 0 && end < line.length && CodeDocument.isWordChar(line[end - 1]) && CodeDocument.isWordChar(line[end]);
        return !cutsStart && !cutsEnd;
    }

    removeSecondaryCursors() {
        if (this._others.length === 0) return false;
        this._others.splice(0);
        this.history.break();
        this._revealVersion++;
        this.selectionChanged?.(this._selection);
        return true;
    }

    placed(primary: CodeCaret) {
        this._selection = this.onElementEdges(primary.selection);
        this._desiredAdvance = primary.advance;
        this.merge();
        this.history.break();
        this._revealVersion++;
        this.selectionChanged?.(this._selection);
    }

    toggleCaret(at: CodePosition) {
        let primary: any; 
        let carets = ($o => (primary = $o.primary, $o.$))(this.caretsInOrder());
        if (carets.length > 1) {
            for (let i = 0; i < carets.length; i++) {
                let selection = carets[i].selection;
                if (CodePosition.opLessThan(at, selection.start) || CodePosition.opLessThan(selection.end, at)) continue;
                carets.splice(i, 1);
                let next = i === primary ? i > 0 ? i - 1 : 0 : primary > i ? primary - 1 : primary;
                this._others.splice(0);
                for (let k = 0; k < carets.length; k++) if (k !== next) this._others.push(carets[k]);
                this.placed(carets[next]);
                return;
            }
        }
        this._others.push(new CodeCaret(this._selection, this._desiredAdvance));
        this.placed(new CodeCaret(new CodeRange(at), -1));
    }

    selectColumn(line: number, advance: number) {
        let start: any; 
        if (!((start = this._column) != null)) return;
        let anchorLine = start.selection.anchor.line;
        let anchor = start.advance;
        let focus = this.grid.measure == null ? $eq.math.roundSingle(advance) : advance;
        let first = Math.min(anchorLine, line);
        let last = Math.max(anchorLine, line);
        let left = Math.min(anchor, focus);
        let reached = false;
        for (let at = first; at <= last; at++) { let rows: any; if ((!((rows = this.grid.rows) != null) || rows.isVisible(at)) && this.widthOf(at) >= left) reached = true; }
        let column: CodeCaret[] = [];
        let primary = -1;
        for (let at = first; at <= last; at++) {
            let rows: any; 
            if ((rows = this.grid.rows) != null && !rows.isVisible(at)) continue;
            if (reached && this.widthOf(at) < left) continue;
            if (at === line) primary = column.length;
            column.push(new CodeCaret(new CodeRange(new CodePosition(at, this.columnAtAdvance(at, anchor)), new CodePosition(at, this.columnAtAdvance(at, focus))), focus));
        }
        if (column.length === 0) return;
        if (primary < 0) primary = line >= anchorLine ? column.length - 1 : 0;
        this._others.splice(0);
        for (let i = 0; i < column.length; i++) if (i !== primary) this._others.push(column[i]);
        this.placed(column[primary]);
    }

    selectedLines(): [number, number] {
        return CodeEditorController.selectedLinesOf(this._selection);
    }

    static selectedLinesOf(range: CodeRange): [number, number] {
        let first = range.start.line;
        let last = range.end.line;
        if (last > first && range.end.column === 0) last--;
        return [first, last];
    }

    wholeLines(first: number, last: number) {
        return new CodeRange(new CodePosition(first, 0), new CodePosition(last, this._document.line(last).length));
    }

    linesOf(first: number, last: number) {
        let lines: string[] = [];
        for (let line = first; line <= last; line++) lines.push(this._document.line(line));
        return lines;
    }

    selectionMovedBy(lines: number) {
        return new CodeRange($eq.withPatch(this._selection.anchor, { line: this._selection.anchor.line + lines }), $eq.withPatch(this._selection.focus, { line: this._selection.focus.line + lines }));
    }

    moveLines(direction: CodeDirectionValue) {
        if (this.readOnly) return false;
        this.endComposition();
        let up = direction === 'backward';
        return this.atEveryBlock(CodeEditorController.selectedLinesOf, true, (carets) => this.moveBlock(carets, up));
    }

    moveBlock(carets: CodeCaret[], up: boolean) {
        let [first, last] = CodeEditorController.blockLines(carets);
        if (up ? first === 0 : carets[carets.length - 1].selection.end.line === this._document.lineCount - 1) return null;
        if (up) {
            let passed = this._document.line(first - 1);
            let atEnd = last + 1 === this._document.lineCount;
            let at = atEnd ? new CodePosition(last, this._document.line(last).length) : new CodePosition(last + 1, 0);
            let written = atEnd ? '\n' + passed : passed + '\n';
            this.edit(new CodeRange(at), written, false);
            this.edit(new CodeRange(new CodePosition(first - 1, 0), new CodePosition(first, 0)), '', false);
        } else {
            let below = this._document.line(last + 1);
            let taken = last + 2 === this._document.lineCount ? new CodeRange(new CodePosition(last, this._document.line(last).length), new CodePosition(last + 1, below.length)) : new CodeRange(new CodePosition(last + 1, 0), new CodePosition(last + 2, 0));
            this.edit(taken, '', false);
            this.edit(new CodeRange(new CodePosition(first, 0)), below + '\n', false);
        }
        return CodeEditorController.movedBy(carets, up ? -1 : 1);
    }

    static movedBy(carets: CodeCaret[], lines: number) {
        let moved: CodeCaret[] = [];
        for (const caret of carets) {
            let selection = caret.selection;
            moved.push($eq.withPatch(caret, { selection: new CodeRange($eq.withPatch(selection.anchor, { line: selection.anchor.line + lines }), $eq.withPatch(selection.focus, { line: selection.focus.line + lines })) }));
        }
        return moved;
    }

    copyLines(direction: CodeDirectionValue) {
        if (this.readOnly) return false;
        this.endComposition();
        let up = direction === 'backward';
        return this.atEveryBlock(CodeEditorController.selectedLinesOf, false, (carets) => {
            let [first, last] = CodeEditorController.blockLines(carets);
            let block = $eq.text.join('\n', this.linesOf(first, last));
            if (up) {
                this.edit(new CodeRange(new CodePosition(first, 0)), block + '\n', false);
                return carets;
            }
            this.edit(new CodeRange(new CodePosition(last, this._document.line(last).length)), '\n' + block, false);
            return CodeEditorController.movedBy(carets, last - first + 1);
        });
    }

    deleteLines() {
        if (this.readOnly) return false;
        this.endComposition();
        return this.atEveryBlock(CodeEditorController.selectedLinesOf, false, (carets) => {
            let [first, last] = CodeEditorController.blockLines(carets);
            let column = carets[0].selection.focus.column;
            let left = null;
            if (last < this._document.lineCount - 1) {
                let next = this._document.line(last + 1);
                if (!this.edit(new CodeRange(new CodePosition(first, 0), new CodePosition(last + 1, 0)), '', false)) return null;
                left = new CodeRange(new CodePosition(first, Math.min(column, next.length)));
            } else if (first > 0) {
                let before = this._document.line(first - 1);
                if (!this.edit(new CodeRange(new CodePosition(first - 1, before.length), new CodePosition(last, this._document.line(last).length)), '', false)) return null;
                left = new CodeRange(new CodePosition(first - 1, Math.min(column, before.length)));
            } else {
                if (!this.edit(this.wholeLines(first, last), '', false)) return null;
                left = new CodeRange(CodePosition.start);
            }
            return [new CodeCaret(left, -1)];
        });
    }

    joinLines() {
        if (this.readOnly) return false;
        this.endComposition();
        return this.atEveryBlock(CodeEditorController.joinedLinesOf, false, (carets) => {
            this._selection = carets.length === 1 ? carets[0].selection : new CodeRange(carets[0].selection.start, carets[carets.length - 1].selection.end);
            return this.joinSelected() ? [new CodeCaret(this._selection, -1)] : null;
        });
    }

    static joinedLinesOf(range: CodeRange): [number, number] {
        return [range.start.line, range.end.line > range.start.line ? range.end.line : range.start.line + 1];
    }

    joinSelected() {
        let [first, last] = this.joinedLines();
        if (last === first) {
            if (first === this._document.lineCount - 1) return false;
            last = first + 1;
        }
        let parts = [this._document.line(first)];
        let length = parts[0].length;
        let met = 0;
        let end = -1;
        let cut = -1;
        let appended = false;
        for (let line = first + 1; line <= last; line++) {
            let text = this._document.line(line);
            let rest = $eq.text.trimStart(text, [' ', '\t']);
            if (rest.length === 0) {
                met = 0;
                continue;
            }
            let space = length > 0;
            let tail = parts[parts.length - 1];
            if (space && (($v130683) => ($v130683 === ' ' || $v130683 === '\t'))(tail[tail.length - 1])) {
                space = false;
                let kept = $eq.text.trimEnd(tail, [' ', '\t']);
                parts[parts.length - 1] = kept;
                length -= tail.length - kept.length;
                parts.push(' ');
                length++;
                if (!appended) cut = length - 1;
            }
            if (space) {
                parts.push(' ');
                length++;
            }
            parts.push(rest);
            length += rest.length;
            appended = true;
            met = rest.length + (space ? 1 : 0);
            if (line === this._selection.end.line) end = length - rest.length + Math.max(0, this._selection.end.column - (text.length - rest.length));
        }
        let joined = [...parts].join('');
        let after = null;
        if (this._selection.isEmpty) {
            after = new CodeRange(new CodePosition(first, joined.length - met));
        } else if (this._selection.start.line === this._selection.end.line) {
            after = new CodeRange(new CodePosition(first, CodeEditorController.collapsed(this._selection.anchor.column, cut)), new CodePosition(first, CodeEditorController.collapsed(this._selection.focus.column, cut)));
        } else {
            let from = new CodePosition(first, CodeEditorController.collapsed(this._selection.start.column, cut));
            let to = new CodePosition(first, end < 0 ? joined.length : end);
            after = $eq.equals(this._selection.anchor, this._selection.start) ? new CodeRange(from, to) : new CodeRange(to, from);
        }
        return this.edit(this.wholeLines(first, last), joined, false, after);
    }

    static collapsed(column: number, cut: number) {
        return cut >= 0 && column > cut ? Math.min(column, cut + 1) : column;
    }

    sortLines(descending: boolean) {
        if (this.readOnly) return false;
        this.endComposition();
        return this.atEveryBlock(CodeEditorController.selectedLinesOf, false, (carets) => {
            let [first, last] = CodeEditorController.blockLines(carets);
            if (this.linesInOrder(first, last, descending)) return null;
            let sorted = this.linesOf(first, last);
            $eq.collections.listSortBy(sorted, (a: string, b: string) => descending ? CodeEditorController.compareLines(b, a) : CodeEditorController.compareLines(a, b), 'System.Comparison`1[System.String]');
            return this.edit(this.wholeLines(first, last), $eq.text.join('\n', sorted), false) ? carets : null;
        });
    }

    linesInOrder(first: number, last: number, descending: boolean) {
        for (let line = first; line < last; line++) {
            let order = CodeEditorController.compareLines(this._document.line(line), this._document.line(line + 1));
            if (descending ? order < 0 : order > 0) return false;
        }
        return true;
    }

    static compareLines(a: string, b: string) {
        let aside = $eq.text.compare(a, b, 'ordinalIgnoreCase');
        return aside !== 0 ? aside : $eq.text.compare(a, b, 'ordinal');
    }

    insertLine(direction: CodeDirectionValue) {
        if (this.readOnly) return false;
        this.endComposition();
        return this.atEveryCaret(() => this.insertLineAtCaret(direction));
    }

    insertLineAtCaret(direction: CodeDirectionValue) {
        let line = this.caret.line;
        let indent = this._document.indentOf(line);
        if (direction === 'backward') return this.edit(new CodeRange(new CodePosition(line, 0)), indent + '\n', false, new CodeRange(new CodePosition(line, indent.length)));
        let text = this._document.line(line);
        let opened = indent + (this.opensBlock(text) ? this.indentStep : '');
        return this.edit(new CodeRange(new CodePosition(line, text.length)), '\n' + opened, false, new CodeRange(new CodePosition(line + 1, opened.length)));
    }

    undo() {
        if (this.readOnly) return false;
        this.endComposition();
        let selections: any; let made: any; 
        if (($o => (selections = $o.selections, made = $o.made, $o.$))(this.history.undo(this._document)) == null) return false;
        this.replay(made, selections);
        return true;
    }

    redo() {
        if (this.readOnly) return false;
        this.endComposition();
        let selections: any; let made: any; 
        if (($o => (selections = $o.selections, made = $o.made, $o.$))(this.history.redo(this._document)) == null) return false;
        this.replay(made, selections);
        return true;
    }

    replay(made: CodeEdit[], selections: CodeRange[]) {
        let before = this._selection;
        for (const edit of made) {
            this._document = ($o => ($o.$))(this._document.replace(edit.range, edit.insertedText));
            this.took(edit);
        }
        this._selection = this.onElementEdges($eq.collections.item(selections, 0));
        this._others.splice(0);
        for (let i = 1; i < $eq.collections.count(selections); i++) this._others.push(new CodeCaret(this.onElementEdges($eq.collections.item(selections, i)), -1));
        this.merge();
        this._revealVersion++;
        this._desiredAdvance = -1;
        this.raise(new CodeChange(this.heard(made, before)));
    }

    took(edit: CodeEdit) {
        let line = edit.range.start.line;
        let linesInserted = edit.insertedRange.end.line - line;
        let linesRemoved = edit.range.end.line - line;
        this.highlighter.lineChanged(this._document, line, linesInserted, linesRemoved);
        this.widthsChanged(line, linesInserted, linesRemoved);
        this.diagnostics.carry(edit);
    }

    findAll(needle: string, matchCase: boolean = false) {
        let matches: CodeRange[] = [];
        if (needle.length === 0) return matches;
        let pin = matchCase ? needle : $eq.text.toLower(needle, true);
        for (let line = 0; line < this._document.lineCount; line++) {
            let raw = this._document.line(line);
            let text = matchCase ? raw : $eq.text.toLower(raw, true);
            let at = $eq.text.indexOf(text, pin, 'ordinal');
            while (at >= 0) {
                matches.push(new CodeRange(new CodePosition(line, at), new CodePosition(line, at + needle.length)));
                at = at + pin.length <= text.length ? $eq.text.indexOf(text, pin, at + pin.length, 'ordinal') : -1;
            }
        }
        return matches;
    }

    findNext(needle: string, matchCase: boolean = false, backward: boolean = false) {
        return this.nextOf(this.findAll(needle, matchCase), backward);
    }

    nextOf(matches: CodeRange[], backward: boolean = false) {
        if ($eq.collections.count(matches) === 0) return null;
        let low = 0;
        let high = $eq.collections.count(matches);
        if (backward) {
            while (low < high) {
                let middle = Math.trunc((low + high) / 2);
                if (CodePosition.opLessOrEqual($eq.collections.item(matches, middle).end, this._selection.start)) low = middle + 1; else high = middle;
            }
            return low > 0 ? $eq.collections.item(matches, low - 1) : $eq.collections.item(matches, $eq.collections.count(matches) - 1);
        }
        while (low < high) {
            let middle = Math.trunc((low + high) / 2);
            if (CodePosition.opLessThan($eq.collections.item(matches, middle).start, this._selection.end)) low = middle + 1; else high = middle;
        }
        return low < $eq.collections.count(matches) ? $eq.collections.item(matches, low) : $eq.collections.item(matches, 0);
    }

    bracketAtCaret(): [CodePosition, CodePosition] | null {
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
        let line = from.line;
        let column = from.column;
        while (true) {
            let text = this._document.line(line);
            while (forward ? column < text.length : column >= 0) {
                if (column < text.length) {
                    let c = text[column];
                    if (c === same) depth++; else if (c === other) {
                        depth--;
                        if (depth === 0) return new CodePosition(line, column);
                    }
                }
                column += forward ? 1 : -1;
            }
            line += forward ? 1 : -1;
            if (line < 0 || line >= this._document.lineCount) return null;
            column = forward ? 0 : this._document.line(line).length - 1;
        }
    }

    toString(): string {
        return 'eQuantic.UI.Code.CodeEditorController';
    }
}

