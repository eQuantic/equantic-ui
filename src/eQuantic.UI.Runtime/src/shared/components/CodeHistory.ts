import { $eq, CodeDocument, CodeEdit, CodePosition, CodeRange, CodeStep } from "../runtime-exports";

export class CodeHistory {
    constructor() {
        this._past = [];
        this._future = [];
        this._running = false;
        this.limit = 500;
    }

    _past!: CodeStep[];
    _future!: CodeStep[];
    _running!: boolean;
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
        if (this._running && this._past.length > 0 && CodeHistory.continues(this._past[this._past.length - 1], step)) {
            this._past[this._past.length - 1] = CodeHistory.joined(this._past[this._past.length - 1], step);
            return;
        }
        this._past.push(step);
        if (this._past.length > this.limit) this._past.splice(0, 1);
        this._running = CodeHistory.runs(step);
    }

    static runs(step: CodeStep) {
        if ($eq.collections.count(step.edits) !== $eq.collections.count(step.before)) return false;
        for (const edit of step.edits) if (!edit.typed || edit.breaksLine) return false;
        return true;
    }

    static continues(previous: CodeStep, step: CodeStep) {
        let count = $eq.collections.count(step.edits);
        if (count !== $eq.collections.count(previous.edits) || count !== $eq.collections.count(step.before) || count !== $eq.collections.count(previous.after)) return false;
        for (let i = 0; i < count; i++) if (!$eq.equals($eq.collections.item(step.before, i), $eq.collections.item(previous.after, i)) || !$eq.collections.item(step.before, i).isEmpty) return false;
        let carets: CodePosition[] = [];
        for (const caret of step.before) carets.push(caret.focus);
        $eq.collections.listSort(carets, $eq.collections.order('comparable', 'comparable'));
        for (let k = 0; k < count; k++) {
            let edit = $eq.collections.item(step.edits, k);
            if (!edit.typed || !edit.isSimpleInsert || !$eq.equals(edit.range.start, carets[count - 1 - k])) return false;
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
        return this._running = false;
    }

    undo(document: CodeDocument) {
        let selections: any, made: any;
        const $r = (() => {
            selections = [];
            made = [];
            if (this._past.length === 0) return null;
            let step = this._past[this._past.length - 1];
            this._past.splice(this._past.length - 1, 1);
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
            let step = this._future[this._future.length - 1];
            this._future.splice(this._future.length - 1, 1);
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

