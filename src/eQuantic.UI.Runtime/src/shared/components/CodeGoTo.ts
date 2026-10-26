import { $eq, CancellationToken, CancellationTokenSource, CodeEditorController, CodeFuzzyMatch, CodeGoToEntry, CodeGoToLine, CodeGoToModeValue, CodePosition, CodeRange, CodeSymbol, ProviderRequest, Rect, UiThread } from "../runtime-exports";

export class CodeGoTo {
    constructor(editor: CodeEditorController) {
        this._editor = null!;
        this._open = false;
        this._query = '';
        this._mode = 'none';
        this._line = null;
        this._lineTarget = null;
        this._entries = [];
        this._selected = -1;
        this._preview = null;
        this._revealed = null;
        this._view = new Rect();
        this._symbols = [];
        this._containers = [];
        this._asking = false;
        this._enclose = false;
        this._generation = 0;
        this._request = null;
        this.pageSize = 10;
        this.changed = null;
        this._editor = editor;
    }

    _editor!: CodeEditorController;
    _open!: boolean;
    _query!: string;
    _mode!: CodeGoToModeValue;
    _line!: CodeGoToLine | null;
    _lineTarget!: CodePosition | null;
    _entries!: CodeGoToEntry[];
    _selected!: number;
    _preview!: CodeRange | null;
    _revealed!: CodePosition | null;
    _view!: Rect;
    _symbols!: CodeSymbol[];
    _containers!: (string | null)[];
    _asking!: boolean;
    _enclose!: boolean;
    _generation!: number;
    _request!: CancellationTokenSource | null;
    pageSize!: number;
    changed!: (() => void) | null;

    get isOpen(): boolean {
        return this._open;
    }

    get query(): string {
        return this._query;
    }

    set query(value: string) {
        if (!this._open || value === this._query) return;
        this.read(value, false);
    }

    get mode(): CodeGoToModeValue {
        return this._mode;
    }

    get line(): CodeGoToLine | null {
        return this._line;
    }

    get entries(): CodeGoToEntry[] {
        return this._entries;
    }

    get selected(): number {
        return this._selected;
    }

    get isAsking(): boolean {
        return this._asking;
    }

    get preview(): CodeRange | null {
        return this._preview;
    }

    open(mode: CodeGoToModeValue) {
        if (!this._open) {
            this._open = true;
            this._view = this._editor.view;
            this._editor.completion.dismiss();
            this._editor.hover.dismiss();
            this._editor.signatureHelp.dismiss();
            if (!(this._editor.composition == null)) this._editor.setComposition('');
        }
        this.read((() => { const $s = mode; if ($s === 'line') return ':'; if ($s === 'symbol') return '@'; return ''; })(), true);
    }

    select(index: number) {
        if (!this._open || this._entries.length === 0) return false;
        let next = Math.min(Math.max(index, 0), this._entries.length - 1);
        if (next === this._selected) return false;
        this._selected = next;
        this.previewed();
        this.changed?.();
        return true;
    }

    selectNext() {
        return this._entries.length > 1 && this.select($eq.num.intRem(this._selected + 1, this._entries.length));
    }

    selectPrevious() {
        return this._entries.length > 1 && this.select($eq.num.intRem(this._selected - 1 + this._entries.length, this._entries.length));
    }

    selectNextPage() {
        return this.select(this._selected + Math.max(1, this.pageSize));
    }

    selectPreviousPage() {
        return this.select(this._selected - Math.max(1, this.pageSize));
    }

    accept() {
        if (!this._open) return false;
        let target = this._mode === 'line' ? this._lineTarget : this._mode === 'symbol' && this._selected >= 0 ? this._entries[this._selected].target : null;
        let to: any; 
        if (!((to = target) != null)) return false;
        this.end();
        this._editor.jumpList.jump(() => this._editor.selection = new CodeRange(to));
        this._editor.revealInCenter(to);
        this._editor.requestFocus();
        this.changed?.();
        return true;
    }

    close() {
        if (!this._open) return false;
        this.end();
        this._editor.scrollTo(this._view);
        this._editor.requestFocus();
        this.changed?.();
        return true;
    }

    key(key: string, modifiers: number) {
        if ((modifiers & (4 | 8 | 2)) !== 0) return false;
        switch (key) {
            case 'Enter':
                this.accept();
                return true;
            case 'Escape':
                this.close();
                return true;
            case 'ArrowDown':
                this.selectNext();
                return true;
            case 'ArrowUp':
                this.selectPrevious();
                return true;
            case 'PageDown':
                this.selectNextPage();
                return true;
            case 'PageUp':
                this.selectPreviousPage();
                return true;
            case 'Backspace':
                if (this._query.length > 0) this.query = $eq.text.substring(this._query, 0, this._query.length - 1);
                return true;
            default:
                return false;
        }
    }

    type(text: string) {
        return this.query = this._query + $eq.text.replace($eq.text.replace(text, '\r', '', 'ordinal'), '\n', '', 'ordinal');
    }

    dismiss() {
        if (!this._open) return false;
        this.end();
        this.changed?.();
        return true;
    }

    reset() {
        if (!this._open) return;
        this.end();
        this.changed?.();
    }

    end() {
        this._open = false;
        this._generation++;
        ProviderRequest.cancel(this._request, null);
        this._request = null;
        this._asking = false;
        this._query = '';
        this._mode = 'none';
        this._line = null;
        this._lineTarget = null;
        this._entries = [];
        this._selected = -1;
        this._preview = null;
        this._revealed = null;
        this._symbols.splice(0);
        this._containers.splice(0);
    }

    read(query: string, opening: boolean) {
        let was: CodeGoToModeValue = this._mode;
        this._query = query;
        this._mode = query.startsWith(':') ? 'line' : query.startsWith('@') ? 'symbol' : 'none';
        if (this._mode === 'symbol' && (opening || was !== 'symbol')) {
            this._enclose = true;
            this.ask();
        } else {
            this._enclose = false;
            if (this._mode !== 'symbol') {
                this._generation++;
                ProviderRequest.cancel(this._request, null);
                this._request = null;
                this._asking = false;
                this._symbols.splice(0);
                this._containers.splice(0);
            }
        }
        this.refresh();
    }

    refresh() {
        this._line = null;
        this._lineTarget = null;
        this._entries = [];
        this._selected = -1;
        if (this._mode === 'line') this.readLine(); else if (this._mode === 'symbol') this.filter();
        this.previewed();
        this.changed?.();
    }

    readLine() {
        let text = $eq.text.substring($eq.text.trim(this._query), 1);
        let parts: string[] = [];
        let start = 0;
        for (let i = 0; i < text.length; i++) {
            if (text[i] !== ',' && text[i] !== ':' && text[i] !== '#') continue;
            parts.push($eq.text.substring(text, start, i - start));
            start = i + 1;
        }
        parts.push($eq.text.substring(text, start));
        let typed: any; 
        if (!((typed = CodeGoTo.parseInt($eq.text.trim(parts[0]))) != null)) return;
        let count = this._editor.document.lineCount;
        let line = (Math.trunc(Math.min(Math.max(1, typed >= 0 ? typed : count + 1 + typed), count)) | 0);
        let cells = this._editor.cellsOf(line - 1);
        let columns = cells.width + 1;
        let column: any; 
        if (parts.length < 2 || !((column = CodeGoTo.parseInt($eq.text.trim(parts[1]))) != null)) {
            this._line = new CodeGoToLine(line, null, parts.length >= 2, columns);
            this._lineTarget = new CodePosition(line - 1, 0);
            return;
        }
        let visible = (Math.trunc(Math.min(Math.max(1, column >= 0 ? column : columns + column), columns)) | 0);
        this._line = new CodeGoToLine(line, visible, true, columns);
        this._lineTarget = new CodePosition(line - 1, cells.columnAt(Math.fround(visible - 1)));
    }

    static parseInt(text: string) {
        let i = 0;
        let negative = false;
        if (i < text.length && (text[i] === '+' || text[i] === '-')) {
            negative = text[i] === '-';
            i++;
        }
        let first = i;
        let value = 0;
        while (i < text.length && text[i] >= '0' && text[i] <= '9') {
            value = value * 10 + (text[i].charCodeAt(0) - 48);
            i++;
        }
        if (i === first) return null;
        return negative ? -value : value;
    }

    ask() {
        let generation = ++this._generation;
        ProviderRequest.cancel(this._request, null);
        this._symbols.splice(0);
        this._containers.splice(0);
        let request = $eq.cancellation.source();
        this._request = request;
        this._asking = true;
        this.askAsync(generation, request.token);
    }

    async askAsync(generation: number, cancellation: CancellationToken) {
        let answer = null;
        try {
            answer = await this._editor.symbols.requestAsync(cancellation);
        } catch ($e: any) {
            if ($eq.exceptions.is($e, 'System.OperationCanceledException') && $eq.exceptions.filter(() => cancellation.isCancellationRequested)) {
                return;
            } else {
                throw $e;
            }
        }
        UiThread.run(() => {
            if (generation !== this._generation) return;
            this._asking = false;
            this.flatten(answer);
            this.refresh();
        });
    }

    flatten(answer: CodeSymbol[]) {
        let symbols: CodeSymbol[] = [];
        let containers: (string | null)[] = [];
        CodeGoTo.walk(answer, null, symbols, containers);
        let order: number[] = [];
        for (let i = 0; i < symbols.length; i++) order.push(i);
        $eq.collections.listSortBy(order, (a: number, b: number) => {
            let start = symbols[a].range.start.compareTo(symbols[b].range.start);
            if (start !== 0) return start;
            let end = symbols[b].range.end.compareTo(symbols[a].range.end);
            return end !== 0 ? end : Math.sign(a - b);
        }, 'System.Comparison`1[System.Int32]');
        this._symbols.splice(0);
        this._containers.splice(0);
        for (const i of order) {
            this._symbols.push(symbols[i]);
            this._containers.push(containers[i]);
        }
    }

    static walk(level: CodeSymbol[], container: string | null, symbols: CodeSymbol[], containers: (string | null)[]) {
        for (const symbol of level) {
            symbols.push(symbol);
            containers.push(container);
            CodeGoTo.walk(symbol.children, symbol.name, symbols, containers);
        }
    }

    filter() {
        let query = $eq.text.trim($eq.text.substring(this._query, 1));
        if (query.startsWith(':')) query = $eq.text.trim($eq.text.substring(query, 1));
        let words: string[] = [];
        for (const word of $eq.text.split(query, ' ')) if (word.length > 0) words.push(word);
        let found: CodeGoToEntry[] = [];
        let scores: number[] = [];
        for (let index = 0; index < this._symbols.length; index++) {
            let symbol = this._symbols[index];
            let name = $eq.text.trim(symbol.name);
            let container = this._containers[index];
            let matches: number[] = [];
            let containerMatches: number[] = [];
            let score = 0;
            if (words.length > 0) {
                let whole = words.length > 1 ? CodeFuzzyMatch.of($eq.text.join('', words), name, true) : null;
                if (!(whole == null)) {
                    score = whole.score;
                    matches = whole.positions;
                } else {
                    let named: any; 
                    if (!((named = CodeFuzzyMatch.of(words[0], name, true)) != null)) continue;
                    score = named.score;
                    matches = named.positions;
                    if (words.length > 1) {
                        if (container == null) continue;
                        let held: number[] = [];
                        let all = true;
                        for (let w = 1; w < words.length && all; w++) {
                            let part: any; 
                            if ((part = CodeFuzzyMatch.of(words[w], container, true)) != null) {
                                score += part.score;
                                for (const at of part.positions) if (!held.includes(at)) held.push(at);
                            } else all = false;
                        }
                        if (!all) continue;
                        $eq.collections.listSort(held, $eq.collections.order('value', 'comparable'));
                        containerMatches = held;
                    }
                }
            }
            let $n49: any; 
            found.push(($n49 = new CodeGoToEntry(name, symbol.kind, symbol.range, symbol.selectionRange.start), $n49.container = container, $n49.deprecated = symbol.deprecated, $n49.matches = matches, $n49.containerMatches = containerMatches, $n49));
            scores.push(score);
        }
        let order: number[] = [];
        for (let i = 0; i < found.length; i++) order.push(i);
        if (words.length > 0) $eq.collections.listSortBy(order, (a: number, b: number) => scores[a] !== scores[b] ? Math.sign(scores[b] - scores[a]) : Math.sign(a - b), 'System.Comparison`1[System.Int32]');
        for (const i of order) this._entries.push(found[i]);
        if (this._entries.length === 0) return;
        this._selected = 0;
        if (!this._enclose || words.length > 0) return;
        let caret = this._editor.selection.focus;
        for (let i = this._entries.length - 1; i >= 0; i--) {
            let range = this._entries[i].range;
            if (range.start.compareTo(caret) <= 0 && caret.compareTo(range.end) <= 0) {
                this._selected = i;
                break;
            }
        }
    }

    previewed() {
        let at: CodePosition | null = null;
        let line: any; 
        if (this._mode === 'line' && (line = this._lineTarget) != null) {
            at = line;
            this._preview = new CodeRange(line);
        } else if (this._mode === 'symbol' && this._selected >= 0) {
            at = this._entries[this._selected].target;
            this._preview = this._entries[this._selected].range;
        } else this._preview = null;
        let shown: any; 
        if (!((shown = at) != null) || $eq.equals(this._revealed, shown)) return;
        this._revealed = shown;
        this._editor.revealInCenter(shown);
    }

    toString(): string {
        return 'eQuantic.UI.Code.CodeGoTo';
    }
}

