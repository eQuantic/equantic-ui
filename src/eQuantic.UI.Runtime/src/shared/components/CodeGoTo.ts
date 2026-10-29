import { $eq, CancellationToken, CancellationTokenSource, CodeChange, CodeDefinitionKindValue, CodeDocument, CodeEditorController, CodeFuzzyMatch, CodeGoToEntry, CodeGoToLine, CodeGoToModeValue, CodeGoToNotFound, CodeLocation, CodePosition, CodeRange, CodeSymbol, ProviderRequest, Rect, UiThread } from "../runtime-exports";

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
        this._places = [];
        this._placeAt = -1;
        this._nearest = -1;
        this._entryPlaces = [];
        this._keepPlace = -1;
        this._kind = 'definition';
        this._notFound = null;
        this._placesGeneration = 0;
        this._placesRequest = null;
        this.pageSize = 10;
        this.changed = null;
        this._editor = editor;
        editor.changed = $eq.delegates.combine(editor.changed, this.carry.bind(this));
        editor.selectionChanged = $eq.delegates.combine(editor.selectionChanged, (_) => this.moved());
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
    _places!: CodeLocation[];
    _placeAt!: number;
    _nearest!: number;
    _entryPlaces!: number[];
    _keepPlace!: number;
    _kind!: CodeDefinitionKindValue;
    _notFound!: CodeGoToNotFound | null;
    _placesGeneration!: number;
    _placesRequest!: CancellationTokenSource | null;
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

    get definitionKind(): CodeDefinitionKindValue {
        return this._kind;
    }

    get places(): CodeLocation[] {
        return this._places;
    }

    get placeIndex(): number {
        return this._placeAt;
    }

    get notFound(): CodeGoToNotFound | null {
        return this._notFound;
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
        this.begin();
        if (CodeGoTo.lists(this._mode)) this._mode = 'none';
        this.read((() => { const $s = mode; if ($s === 'line') return ':'; if ($s === 'symbol') return '@'; return ''; })(), true);
    }

    begin() {
        if (this._open) return;
        this._open = true;
        this._view = this._editor.view;
        this._editor.completion.dismiss();
        this._editor.hover.dismiss();
        this._editor.signatureHelp.dismiss();
        this.unsay();
        if (!(this._editor.composition == null)) this._editor.setComposition('');
    }

    static lists(mode: CodeGoToModeValue) {
        return mode === 'definitions' || mode === 'references';
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
        if (CodeGoTo.lists(this._mode)) {
            if (this._selected < 0) return false;
            let index = this._entryPlaces[this._selected];
            let view = this._view;
            this.end();
            this._placeAt = index;
            this.goToPlace(this._places[index], view);
            this._editor.requestFocus();
            this.changed?.();
            return true;
        }
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
            case 'F4':
                if (!CodeGoTo.lists(this._mode)) return false;
                if ((modifiers & 1) !== 0) this.selectPrevious(); else this.selectNext();
                return true;
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
        this._places.splice(0);
        this._placeAt = -1;
        this.stopAsking();
        let said = !(this._notFound == null);
        this._notFound = null;
        if (!this._open) {
            if (said) this.changed?.();
            return;
        }
        this.end();
        this.changed?.();
    }

    end() {
        this._open = false;
        this.stopSymbols();
        this._query = '';
        this._mode = 'none';
        this._line = null;
        this._lineTarget = null;
        this._entries = [];
        this._entryPlaces.splice(0);
        this._nearest = -1;
        this._selected = -1;
        this._preview = null;
        this._revealed = null;
    }

    stopSymbols() {
        this._generation++;
        ProviderRequest.cancel(this._request, null);
        this._request = null;
        this._asking = false;
        this._symbols.splice(0);
        this._containers.splice(0);
    }

    read(query: string, opening: boolean) {
        if (CodeGoTo.lists(this._mode)) {
            this._query = query;
            this._enclose = false;
            this.refresh();
            return;
        }
        let was: CodeGoToModeValue = this._mode;
        this._query = query;
        this._mode = query.startsWith(':') ? 'line' : query.startsWith('@') ? 'symbol' : 'none';
        if (this._mode === 'symbol' && (opening || was !== 'symbol')) {
            this._enclose = true;
            this.ask();
        } else {
            this._enclose = false;
            if (this._mode !== 'symbol') this.stopSymbols();
        }
        this.refresh();
    }

    refresh() {
        this._line = null;
        this._lineTarget = null;
        this._entries = [];
        this._entryPlaces.splice(0);
        this._selected = -1;
        if (this._mode === 'line') this.readLine(); else if (this._mode === 'symbol') this.filter(); else if (CodeGoTo.lists(this._mode)) this.filterPlaces();
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
            let $n8: any; 
            found.push(($n8 = new CodeGoToEntry(name, symbol.kind, symbol.range, symbol.selectionRange.start), $n8.container = container, $n8.deprecated = symbol.deprecated, $n8.matches = matches, $n8.containerMatches = containerMatches, $n8));
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
        } else if ((this._mode === 'symbol' || CodeGoTo.lists(this._mode)) && this._selected >= 0 && this._entries[this._selected].document == null) {
            at = this._entries[this._selected].target;
            this._preview = this._entries[this._selected].range;
        } else this._preview = null;
        let shown: any; 
        if (!((shown = at) != null) || $eq.equals(this._revealed, shown)) return;
        this._revealed = shown;
        this._editor.revealInCenter(shown);
    }

    definition(kind: CodeDefinitionKindValue) {
        return $eq.collections.count(this._editor.definitions.providers) > 0 && this.requestPlaces('definitions', kind, false);
    }

    references() {
        return $eq.collections.count(this._editor.references.providers) > 0 && this.requestPlaces('references', 'definition', false);
    }

    followLink() {
        return $eq.collections.count(this._editor.definitions.providers) > 0 && this.requestPlaces('definitions', 'definition', true);
    }

    nextPlace() {
        return this.walk(1);
    }

    previousPlace() {
        return this.walk(-1);
    }

    walk(step: number) {
        if (this._open && CodeGoTo.lists(this._mode)) {
            if (step > 0) this.selectNext(); else this.selectPrevious();
            return true;
        }
        if (this._places.length === 0) return false;
        let count = this._places.length;
        this._placeAt = this._placeAt < 0 ? step > 0 ? 0 : count - 1 : $eq.num.intRem($eq.num.intRem(this._placeAt + step, count) + count, count);
        this.goToPlace(this._places[this._placeAt], null);
        this.changed?.();
        return true;
    }

    requestPlaces(mode: CodeGoToModeValue, kind: CodeDefinitionKindValue, muted: boolean) {
        this.unsay();
        this.stopAsking();
        let generation = this._placesGeneration;
        let request = $eq.cancellation.source();
        this._placesRequest = request;
        let at = this._editor.selection.focus;
        this.askPlaces(mode, kind, at, muted, generation, request.token);
        return true;
    }

    async askPlaces(mode: CodeGoToModeValue, kind: CodeDefinitionKindValue, at: CodePosition, muted: boolean, generation: number, cancellation: CancellationToken) {
        let places = null;
        try {
            places = mode === 'references' ? await this._editor.references.requestAsync(at, cancellation) : await this._editor.definitions.requestAsync(at, kind, cancellation);
        } catch ($e: any) {
            if ($eq.exceptions.is($e, 'System.OperationCanceledException') && $eq.exceptions.filter(() => cancellation.isCancellationRequested)) {
                return;
            } else {
                throw $e;
            }
        }
        UiThread.run(() => {
            if (generation !== this._placesGeneration) return;
            this._placesRequest = null;
            this.answered(mode, kind, at, muted, places);
        });
    }

    answered(mode: CodeGoToModeValue, kind: CodeDefinitionKindValue, at: CodePosition, muted: boolean, places: CodeLocation[]) {
        if ($eq.collections.count(places) === 0) {
            if (muted) return;
            let word = this._editor.definitions.wordUnder(at) ?? (at.column > 0 ? this._editor.definitions.wordUnder($eq.withPatch(at, { column: at.column - 1 })) : null);
            let found: any; 
            this._notFound = new CodeGoToNotFound(mode, kind, at, (found = word) != null ? this._editor.document.textIn(found) : '');
            this.changed?.();
            return;
        }
        if ($eq.collections.count(places) === 1) {
            let only = $eq.collections.item(places, 0);
            let here = only.document == null && CodePosition.opLessOrEqual(only.range.start, at) && CodePosition.opLessOrEqual(at, only.range.end);
            if (here && mode === 'definitions' && kind !== 'implementation' && $eq.collections.count(this._editor.references.providers) > 0) {
                this.requestPlaces('references', kind, muted);
                return;
            }
            this.goToPlace(only, null);
            this.changed?.();
            return;
        }
        this.openPlaces(mode, kind, places, at);
    }

    openPlaces(mode: CodeGoToModeValue, kind: CodeDefinitionKindValue, places: CodeLocation[], at: CodePosition) {
        this.begin();
        this.stopSymbols();
        this._mode = mode;
        this._kind = kind;
        this._query = '';
        this._enclose = false;
        this._revealed = null;
        this._places.splice(0);
        $eq.collections.addRange(this._places, places);
        this._nearest = this.nearest(at);
        this._placeAt = this._nearest;
        this.refresh();
    }

    nearest(at: CodePosition) {
        let best = -1;
        let distance = 0;
        for (let i = 0; i < this._places.length; i++) {
            if (!(this._places[i].document == null)) continue;
            let start = this._places[i].range.start;
            let away = Math.abs(start.line - at.line) * 100 + Math.abs(start.column - at.column);
            if (best >= 0 && away >= distance) continue;
            best = i;
            distance = away;
        }
        return best >= 0 ? best : 0;
    }

    filterPlaces() {
        let query = $eq.text.trim(this._query);
        let document = this._editor.document;
        for (let i = 0; i < this._places.length; i++) {
            let entry = CodeGoTo.placeEntry(this._places[i], document);
            if (query.length > 0) {
                let match: any; 
                if (!((match = CodeFuzzyMatch.of(query, entry.name, true)) != null)) continue;
                entry = $eq.withPatch(entry, { matches: match.positions });
            }
            this._entries.push(entry);
            this._entryPlaces.push(i);
        }
        if (this._entries.length === 0) return;
        this._selected = 0;
        let kept = this._entryPlaces.indexOf(this._keepPlace);
        if (kept >= 0) {
            this._selected = kept;
            return;
        }
        if (query.length > 0) return;
        let nearest = this._entryPlaces.indexOf(this._nearest);
        if (nearest >= 0) this._selected = nearest;
    }

    static placeEntry(place: CodeLocation, document: CodeDocument) {
        let start = place.range.start;
        let where = $eq.text.format(start.line + 1, null, undefined, undefined, 'int32') + ':' + $eq.text.format(start.column + 1, null, undefined, undefined, 'int32');
        let other: any; 
        if ((other = place.document) != null) {
            let $n12: any; 
            return ($n12 = new CodeGoToEntry(CodeGoTo.nameOf(other), null, place.range, start), $n12.container = where, $n12.document = other, $n12);
        }
        let at = document.clamp(start);
        let text = document.line(at.line);
        let lead = 0;
        while (lead < text.length && $eq.text.isWhiteSpace(text[lead])) lead++;
        let name = $eq.text.trimEnd($eq.text.substring(text, lead));
        let end = place.range.end;
        let to = (end.line === at.line ? end.column : text.length) - lead;
        let marked: number[] = [];
        for (let column = Math.max(at.column, lead) - lead; column < to && column < name.length; column++) marked.push(column);
        let $n13: any; 
        return ($n13 = new CodeGoToEntry(name, null, place.range, start), $n13.container = where, $n13.matches = marked, $n13);
    }

    static nameOf(document: string) {
        let cut = Math.max(document.lastIndexOf('/'), document.lastIndexOf('\\'));
        return cut >= 0 && cut < document.length - 1 ? $eq.text.substring(document, cut + 1) : document;
    }

    goToPlace(place: CodeLocation, giveBack: Rect | null) {
        if (!(place.document == null)) {
            let view: any; 
            if ((view = giveBack) != null) this._editor.scrollTo(view);
            this._editor.requestOpen(place);
            return;
        }
        let to = this._editor.document.clamp(place.range.start);
        this._editor.jumpList.jump(() => this._editor.selection = new CodeRange(to));
        this._editor.revealNearTopIfOutsideView(to);
    }

    carry(change: CodeChange) {
        if (change.replaced) return;
        this.stopAsking();
        this.unsay();
        if (this._places.length === 0) return;
        for (const edit of change.edits) for (let i = 0; i < this._places.length; i++) if (this._places[i].document == null) this._places[i] = $eq.withPatch(this._places[i], { range: edit.carry(this._places[i].range) });
        if (!this._open || !CodeGoTo.lists(this._mode)) return;
        this._keepPlace = this._selected >= 0 ? this._entryPlaces[this._selected] : -1;
        this.refresh();
        this._keepPlace = -1;
    }

    moved() {
        this.stopAsking();
        this.unsay();
    }

    stopAsking() {
        this._placesGeneration++;
        ProviderRequest.cancel(this._placesRequest, null);
        this._placesRequest = null;
    }

    unsay() {
        if (this._notFound == null) return false;
        this._notFound = null;
        this.changed?.();
        return true;
    }

    toString(): string {
        return 'eQuantic.UI.Code.CodeGoTo';
    }
}

