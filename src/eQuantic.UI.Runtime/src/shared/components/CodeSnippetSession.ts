import { $eq, CodeChange, CodeEditorController, CodePosition, CodeRange, CodeSnippetStop } from "../runtime-exports";

export class CodeSnippetSession {
    constructor(editor: CodeEditorController) {
        this._editor = null!;
        this._stops = null;
        this._current = -1;
        this._reached = '';
        this._placing = false;
        this.changed = null;
        this._editor = editor;
        editor.changed = $eq.delegates.combine(editor.changed, this.carry.bind(this));
        editor.selectionChanged = $eq.delegates.combine(editor.selectionChanged, (_) => this.followed());
    }

    _editor!: CodeEditorController;
    _stops!: CodeSnippetStop[] | null;
    _current!: number;
    _reached!: string;
    _placing!: boolean;
    changed!: (() => void) | null;

    get isActive(): boolean {
        return !(this._stops == null);
    }

    get hasPrevious(): boolean {
        return !(this._stops == null) && this.previousOf(this._current) >= 0;
    }

    next() {
        if (this._stops == null) return false;
        this.leaving();
        let next = this._current + 1;
        while (next < this._stops.length - 1 && CodeSnippetSession.skipped(this._stops[next])) next++;
        this.reach(next);
        return true;
    }

    previous() {
        if (this._stops == null || this.previousOf(this._current) < 0) return false;
        this.leaving();
        let previous = this.previousOf(this._current);
        if (previous >= 0) this.reach(previous);
        return true;
    }

    leave() {
        if (this._stops == null) return false;
        this.end();
        return true;
    }

    placeholdersIn(first: number, last: number) {
        let placeholders: CodeRange[] = [];
        if (this._stops == null) return placeholders;
        for (const stop of this._stops) {
            if (stop.final || stop.dropped) continue;
            for (const range of stop.ranges) {
                if (range.isEmpty || range.end.line < first || range.start.line > last) continue;
                placeholders.push(range);
            }
        }
        return placeholders;
    }

    start(stops: CodeSnippetStop[]) {
        this.end();
        this._stops = stops;
        this._current = 0;
        this._reached = this.textOf(stops[0]);
        this.changed?.();
        this.offerChoices(stops[0]);
    }

    choose(text: string) {
        if (this._stops == null) return false;
        this._placing = true;
        try {
            return this._editor.replaceEvery($eq.collections.listCopy(this._stops[this._current].ranges), text);
        } finally {
            this._placing = false;
        }
    }

    end() {
        if (this._stops == null) return;
        this._stops = null;
        this._current = -1;
        this._editor.completion.dropChoices();
        this.changed?.();
    }

    previousOf(index: number) {
        if (this._stops == null) return -1;
        for (let i = index - 1; i >= 0; i--) if (!CodeSnippetSession.skipped(this._stops[i])) return i;
        return -1;
    }

    static skipped(stop: CodeSnippetStop) {
        return stop.dropped || stop.ranges.length === 0;
    }

    reach(index: number) {
        if (this._stops == null) return;
        let stop = this._stops[index];
        this._current = index;
        this._reached = this.textOf(stop);
        this._editor.completion.dropChoices();
        this._placing = true;
        try {
            this._editor.selections = stop.ranges;
        } finally {
            this._placing = false;
        }
        if (stop.final) {
            this.end();
            return;
        }
        this.changed?.();
        this.offerChoices(stop);
    }

    offerChoices(stop: CodeSnippetStop) {
        let options: any; 
        if (((stop.choices != null && $eq.collections.count(stop.choices) > 0) && (options = stop.choices, true))) this._editor.completion.offerChoices(stop.ranges[0], options);
    }

    leaving() {
        if (this._stops == null) return;
        let leaving = this._stops[this._current];
        if (this.textOf(leaving) === this._reached) return;
        for (const stop of this._stops) if (!stop.final && this.nestedIn(stop, leaving.index)) stop.dropped = true;
    }

    nestedIn(stop: CodeSnippetStop, index: number) {
        let parent = stop.parent;
        for (let depth = 0; parent >= 0 && !(this._stops == null) && depth < this._stops.length; depth++) {
            if (parent === index) return true;
            let next = -1;
            for (const other of this._stops) if (other.index === parent) next = other.parent;
            parent = next;
        }
        return false;
    }

    textOf(stop: CodeSnippetStop) {
        return stop.ranges.length === 0 ? '' : this._editor.document.textIn(stop.ranges[0]);
    }

    carry(change: CodeChange) {
        if (this._stops == null) return;
        if (change.replaced) {
            this.end();
            return;
        }
        for (const edit of change.edits) {
            for (let s = 0; s < this._stops.length; s++) {
                let ranges = this._stops[s].ranges;
                for (let i = 0; i < ranges.length; i++) {
                    let range = ranges[i];
                    ranges[i] = s === this._current ? new CodeRange(edit.carryAnchor(range.start), edit.carryReach(range.end)) : edit.carry(range);
                }
                if (s === this._current) CodeSnippetSession.separate(ranges);
            }
        }
    }

    static separate(ranges: CodeRange[]) {
        for (let i = 0; i < ranges.length; i++) {
            for (let j = 0; j < ranges.length; j++) {
                let range = ranges[i];
                let before = ranges[j];
                if (i === j || !(CodePosition.opLessThan(before.start, range.start) && CodePosition.opLessThan(range.start, before.end))) continue;
                ranges[i] = new CodeRange(CodePosition.opLessThan(before.end, range.end) ? before.end : range.end, range.end);
            }
        }
    }

    followed() {
        if (this._stops == null || this._placing) return;
        if (!this.withinAStop()) this.end();
    }

    withinAStop() {
        if (this._stops == null) return false;
        let selections = this._editor.selections;
        for (const stop of this._stops) {
            if (stop.final || stop.dropped) continue;
            let all = true;
            for (const selection of selections) {
                let inside = false;
                for (const range of stop.ranges) if (CodePosition.opGreaterOrEqual(selection.start, range.start) && CodePosition.opLessOrEqual(selection.end, range.end)) inside = true;
                if (!inside) {
                    all = false;
                    break;
                }
            }
            if (all) return true;
        }
        return false;
    }

    toString(): string {
        return 'eQuantic.UI.Code.CodeSnippetSession';
    }
}

