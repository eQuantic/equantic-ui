import { $eq, CodeDocument, CodeEdit, CodePosition, CodeRange, CodeStep } from "../runtime-exports";

export class CodeHistory {
    constructor() {
        this._past = [];
        this._future = [];
        this._runEnds = null;
        this.limit = 500;
    }

    _past!: CodeStep[];
    _future!: CodeStep[];
    _runEnds!: CodePosition[] | null;
    limit!: number;

    get canUndo(): boolean {
        return this._past.length > 0;
    }

    get canRedo(): boolean {
        return this._future.length > 0;
    }

    record(step: CodeStep) {
        if ($eq.collections.count(step.edits) === 0) return;
        this._future.splice(0);
        if (this._runEnds != null && this._past.length > 0 && CodeHistory.continues(this._runEnds, step)) {
            (($0) => ($0[$0.length - 1] = CodeHistory.joined((($0) => $0[$0.length - 1])(this._past), step)))(this._past);
            this._runEnds = CodeHistory.ends((($0) => $0[$0.length - 1])(this._past));
            return;
        }
        this._past.push(step);
        if (this._past.length > this.limit) $eq.collections.listRemoveAt(this._past, 0);
        this._runEnds = CodeHistory.runs(step) ? CodeHistory.ends(step) : null;
    }

    static runs(step: CodeStep) {
        if ($eq.collections.count(step.edits) !== $eq.collections.count(step.before)) return false;
        for (const edit of step.edits) if (!edit.typed || edit.breaksLine) return false;
        return true;
    }

    static ends(step: CodeStep) {
        let ends: CodePosition[] = [];
        for (let k = 0; k < $eq.collections.count(step.edits); k++) {
            let end = new CodeRange($eq.collections.item(step.edits, k).insertedRange.end);
            for (let later = k + 1; later < $eq.collections.count(step.edits); later++) end = $eq.collections.item(step.edits, later).carry(end);
            if (k > 0 && CodePosition.opGreaterOrEqual(end.end, ends[k - 1])) return null;
            ends.push(end.end);
        }
        return ends;
    }

    static continues(ends: CodePosition[], step: CodeStep) {
        if ($eq.collections.count(step.edits) !== ends.length) return false;
        for (let k = 0; k < ends.length; k++) {
            let edit = $eq.collections.item(step.edits, k);
            if (!edit.typed || !edit.isSimpleInsert || !$eq.equals(edit.range.start, ends[k])) return false;
        }
        return true;
    }

    static joined(previous: CodeStep, step: CodeStep) {
        let edits: CodeEdit[] = [];
        for (let k = 0; k < $eq.collections.count(step.edits); k++) {
            let before = $eq.collections.item(previous.edits, k);
            let edit = $eq.collections.item(step.edits, k);
            edits.push($eq.withPatch(before, { insertedText: before.insertedText + edit.insertedText, selectionAfter: edit.selectionAfter }));
        }
        return new CodeStep(edits, previous.before, step.after);
    }

    break() {
        return this._runEnds = null;
    }

    undo(document: CodeDocument) {
        let selections: any, made: any;
        const $r = (() => {
            selections = [];
            made = [];
            if (this._past.length === 0) return null;
            let step = (($0) => $0[$0.length - 1])(this._past);
            $eq.collections.listRemoveAt(this._past, this._past.length - 1);
            this._future.push(step);
            this.break();
            let next = document;
            let undone: CodeEdit[] = [];
            for (let i = $eq.collections.count(step.edits) - 1; i >= 0; i--) {
                let edit = $eq.collections.item(step.edits, i);
                let replaced = new CodeRange(next.clamp(edit.insertedRange.start), next.clamp(edit.insertedRange.end));
                let removed = next.textIn(replaced);
                let end: any; 
                next = ($o => (end = $o.caret, $o.$))(next.replace(replaced, edit.removedText));
                undone.push(new CodeEdit(replaced, removed, next.textIn(new CodeRange(replaced.start, end)), edit.selectionAfter, edit.selectionBefore, false));
            }
            made = undone;
            selections = step.before;
            return next;
        })();
        return { $: $r, selections, made };
    }

    redo(document: CodeDocument) {
        let selections: any, made: any;
        const $r = (() => {
            selections = [];
            made = [];
            if (this._future.length === 0) return null;
            let step = (($0) => $0[$0.length - 1])(this._future);
            $eq.collections.listRemoveAt(this._future, this._future.length - 1);
            this._past.push(step);
            this.break();
            let next = document;
            let redone: CodeEdit[] = [];
            for (const edit of step.edits) {
                let replaced = new CodeRange(next.clamp(edit.range.start), next.clamp(edit.range.end));
                let removed = next.textIn(replaced);
                let end: any; 
                next = ($o => (end = $o.caret, $o.$))(next.replace(replaced, edit.insertedText));
                redone.push(new CodeEdit(replaced, removed, next.textIn(new CodeRange(replaced.start, end)), edit.selectionBefore, edit.selectionAfter, false));
            }
            made = redone;
            selections = step.after;
            return next;
        })();
        return { $: $r, selections, made };
    }

    clear() {
        this._past.splice(0);
        this._future.splice(0);
        this.break();
    }

    toString(): string {
        return 'eQuantic.UI.Code.CodeHistory';
    }
}

