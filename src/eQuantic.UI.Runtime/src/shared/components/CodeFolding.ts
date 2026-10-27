import { $eq, CancellationToken, CancellationTokenSource, CodeChange, CodeCollapse, CodeDocument, CodeEditorController, CodeFold, CodePosition, CodeRange, CodeRows, IndentationFoldProvider, ProviderRequest, TimeSpan, UiThread } from "../runtime-exports";

export class CodeFolding {
    constructor(editor: CodeEditorController) {
        this._editor = null!;
        this._regions = [];
        this._folded = [];
        this._rows = null;
        this._collapses = [];
        this._generation = 0;
        this._request = null;
        this.providers = [];
        this.delay = $eq.time.timeSpan.fromMilliseconds(200n);
        this.changed = null;
        this.failed = null;
        this._editor = editor;
        editor.changed = $eq.delegates.combine(editor.changed, this.edited.bind(this));
        editor.selectionChanged = $eq.delegates.combine(editor.selectionChanged, this.selected.bind(this));
    }

    _editor!: CodeEditorController;
    _regions!: CodeFold[];
    _folded!: boolean[];
    _rows!: CodeRows | null;
    _collapses!: CodeCollapse[];
    _generation!: number;
    _request!: CancellationTokenSource | null;
    providers!: any[];
    delay!: TimeSpan;
    changed!: (() => void) | null;
    failed!: ((exception: Error) => void) | null;
    static lineEnd: number = 1048576;

    get regions(): CodeFold[] {
        return this._regions;
    }

    get collapses(): CodeCollapse[] {
        return this._collapses;
    }

    get rows(): CodeRows | null {
        return this._rows;
    }

    isFolded(line: number) {
        let at = this.indexOf(line);
        return at >= 0 && this._folded[at];
    }

    isHidden(line: number) {
        return this.headerHiding(line) >= 0;
    }

    regionAt(line: number) {
        let at = this.innermost(line, null);
        return at >= 0 ? this._regions[at] : null;
    }

    check() {
        return this.ask(false);
    }

    fold(line: number) {
        return this.setInnermost(line, true);
    }

    unfold(line: number) {
        return this.setInnermost(line, false);
    }

    foldRecursively(line: number) {
        return this.setWithin(line, true);
    }

    unfoldRecursively(line: number) {
        return this.setWithin(line, false);
    }

    toggle(line: number) {
        let at = this.innermost(line, null);
        if (at < 0) return false;
        this._folded[at] = !this._folded[at];
        this.folded();
        return true;
    }

    foldAll() {
        return this.setAll(true);
    }

    unfoldAll() {
        return this.setAll(false);
    }

    canFold(line: number) {
        return this.innermost(line, false) >= 0;
    }

    canUnfold(line: number) {
        return this.innermost(line, true) >= 0;
    }

    canToggle(line: number) {
        return this.innermost(line, null) >= 0;
    }

    any(folded: boolean) {
        for (const state of this._folded) if (state === folded) return true;
        return false;
    }

    setInnermost(line: number, folded: boolean) {
        let at = this.innermost(line, !folded);
        if (at < 0) return false;
        this._folded[at] = folded;
        this.folded();
        return true;
    }

    setWithin(line: number, folded: boolean) {
        let at = this.innermost(line, null);
        if (at < 0) return false;
        let outer = this._regions[at];
        let changed = false;
        for (let i = at; i < this._regions.length && this._regions[i].startLine <= outer.endLine; i++) {
            if (this._regions[i].endLine > outer.endLine || this._folded[i] === folded) continue;
            this._folded[i] = folded;
            changed = true;
        }
        if (changed) this.folded();
        return changed;
    }

    setAll(folded: boolean) {
        let changed = false;
        for (let i = 0; i < this._folded.length; i++) {
            if (this._folded[i] === folded) continue;
            this._folded[i] = folded;
            changed = true;
        }
        if (changed) this.folded();
        return changed;
    }

    innermost(line: number, folded: boolean | null) {
        let found = -1;
        for (let i = 0; i < this._regions.length && this._regions[i].startLine <= line; i++) {
            if (line > this._regions[i].endLine) continue;
            let state: any; 
            if ((state = folded) != null && this._folded[i] !== state) continue;
            found = i;
        }
        return found;
    }

    static hides(region: CodeFold, lines: number[]) {
        for (const line of lines) if (region.startLine < line && line <= region.endLine) return true;
        return false;
    }

    indexOf(line: number) {
        let low = 0;
        let high = this._regions.length - 1;
        while (low <= high) {
            let middle = low + Math.trunc((high - low) / 2);
            let header = this._regions[middle].startLine;
            if (header === line) return middle;
            if (header < line) low = middle + 1; else high = middle - 1;
        }
        return -1;
    }

    folded() {
        this.apply();
        let selections: CodeRange[] = $eq.collections.listCopy(this._editor.selections);
        let moved = false;
        for (let i = 0; i < selections.length; i++) {
            let header = this.headerHiding(selections[i].focus.line);
            if (header < 0) continue;
            selections[i] = new CodeRange(new CodePosition(header, this._editor.document.line(header).length));
            moved = true;
        }
        if (moved) this._editor.selections = selections;
    }

    headerHiding(line: number) {
        for (let i = 0; i < this._regions.length; i++) if (this._folded[i] && this._regions[i].startLine < line && line <= this._regions[i].endLine) return this._regions[i].startLine;
        return -1;
    }

    apply() {
        this.rebuild();
        this._editor.foldsChanged();
        this.changed?.();
    }

    rebuild() {
        let collapses: CodeCollapse[] = [];
        let hiddenTo = -1;
        for (let i = 0; i < this._regions.length; i++) {
            let region = this._regions[i];
            if (!this._folded[i] || region.startLine < hiddenTo) continue;
            collapses.push(new CodeCollapse(region.startLine + 1, region.endLine, false));
            hiddenTo = region.endLine + 1;
        }
        this._collapses = collapses;
        this._rows = collapses.length === 0 ? null : new CodeRows(this._editor.document.lineCount, [], collapses);
    }

    edited(change: CodeChange) {
        if (change.replaced) {
            this._regions = [];
            this._folded = [];
            this.apply();
            this.check();
            return;
        }
        let regions: CodeFold[] = [];
        let folded: boolean[] = [];
        for (let i = 0; i < this._regions.length; i++) {
            let region = this._regions[i];
            let isFolded = this._folded[i];
            let kept = true;
            for (const edit of change.edits) {
                let from = edit.range.start.line;
                let to = edit.range.end.line;
                if (isFolded && from <= region.endLine && to > region.startLine) isFolded = false;
                let carried = edit.carry(new CodeRange(new CodePosition(region.startLine, 0), new CodePosition(region.endLine, CodeFolding.lineEnd)));
                let header = new CodePosition(region.startLine, 0);
                if (from < region.startLine && to >= region.startLine && (CodePosition.opGreaterThan(edit.range.end, header) || carried.start.column !== 0) || $eq.equals(edit.range.start, header) && to > region.startLine) {
                    kept = false;
                    break;
                }
                region = $eq.withPatch(region, { startLine: carried.start.line, endLine: carried.end.line });
            }
            if (!kept || region.endLine <= region.startLine) continue;
            regions.push(region);
            folded.push(isFolded);
        }
        this._regions = regions;
        this._folded = folded;
        this.apply();
        if (this._editor.hidesFolds) this.ask(true);
    }

    selected(__: CodeRange) {
        let changed = false;
        for (const selection of this._editor.selections) {
            if (this.reveal(selection.anchor.line)) changed = true;
            if (this.reveal(selection.focus.line)) changed = true;
        }
        if (changed) this.apply();
    }

    reveal(line: number) {
        let changed = false;
        for (let i = 0; i < this._regions.length; i++) {
            if (!this._folded[i] || line <= this._regions[i].startLine || line > this._regions[i].endLine) continue;
            this._folded[i] = false;
            changed = true;
        }
        return changed;
    }

    async ask(wait: boolean) {
        let generation = ++this._generation;
        ProviderRequest.cancel(this._request, this.failed);
        let request = $eq.cancellation.source();
        this._request = request;
        if (wait && (this.delay.compareTo($eq.time.timeSpan.zero) > 0)) {
            try {
                await $eq.task.delay(this.delay, request.token);
            } catch ($e: any) {
                if ($eq.exceptions.is($e, 'System.OperationCanceledException')) {
                    return;
                } else {
                    throw $e;
                }
            }
        }
        let document = this._editor.document;
        let providers: any[] = $eq.collections.listCopy(this.providers);
        if (providers.length === 0) providers.push(new IndentationFoldProvider());
        let asked = [];
        for (const provider of providers) asked.push(this.askOne(provider, document, request.token));
        let answers = [];
        for (const one of asked) {
            let answer: any; 
            if (!((answer = await one) != null)) return;
            answers.push(answer);
        }
        UiThread.run(() => {
            if (generation !== this._generation) return;
            this.answered(answers, document);
        });
    }

    async askOne(provider: any, document: CodeDocument, cancellation: CancellationToken) {
        try {
            return await provider.foldsAsync(document, cancellation);
        } catch ($e: any) {
            let error = $e;
            if ($eq.exceptions.is($e, 'System.OperationCanceledException') && $eq.exceptions.filter(() => cancellation.isCancellationRequested)) {
                return null;
            } else {
                UiThread.run(() => this.failed?.(error));
                return null;
            }
        }
    }

    answered(answers: CodeFold[][], document: CodeDocument) {
        let blocked: number[] = [];
        for (const selection of this._editor.selections) {
            blocked.push(selection.anchor.line);
            blocked.push(selection.focus.line);
        }
        let all: CodeFold[] = [];
        for (const answer of answers) for (const region of answer) if (region.startLine >= 0 && region.endLine < document.lineCount && region.endLine > region.startLine) all.push(region);
        $eq.collections.listSortBy(all, (a, b) => a.startLine !== b.startLine ? Math.sign(a.startLine - b.startLine) : Math.sign(b.endLine - a.endLine), 'System.Comparison`1[eQuantic.UI.Code.CodeFold]');
        let regions: CodeFold[] = [];
        let folded: boolean[] = [];
        let open: CodeFold[] = [];
        for (const region of all) {
            while (open.length > 0 && open[open.length - 1].endLine < region.startLine) open.splice(open.length - 1, 1);
            if (open.length > 0) {
                let holding = open[open.length - 1];
                if (region.startLine === holding.startLine || region.endLine > holding.endLine) continue;
            }
            open.push(region);
            regions.push(region);
            folded.push(this.isFolded(region.startLine) && !CodeFolding.hides(region, blocked));
        }
        this._regions = regions;
        this._folded = folded;
        this.apply();
    }

    toString(): string {
        return 'eQuantic.UI.Code.CodeFolding';
    }
}

