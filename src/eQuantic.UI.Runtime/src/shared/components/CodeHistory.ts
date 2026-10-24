import { $eq, CodeDocument, CodeEdit, CodePosition, CodeRange } from "../runtime-exports";

export class CodeHistory {
    constructor() {
        this._past = [];
        this._future = [];
        this._runEnd = new CodePosition(-1, -1);
        this.limit = 500;
    }

    _past!: CodeEdit[][];
    _future!: CodeEdit[][];
    _runEnd!: CodePosition;
    limit!: number;

    get canUndo(): boolean {
        return this._past.length > 0;
    }

    get canRedo(): boolean {
        return this._future.length > 0;
    }

    record(edits: CodeEdit[]) {
        if ($eq.collections.count(edits) === 0) return;
        this._future.splice(0);
        let edit = $eq.collections.item(edits, 0);
        if ($eq.collections.count(edits) === 1 && edit.typed && edit.isSimpleInsert && this._past.length > 0 && $eq.equals(edit.range.start, this._runEnd)) {
            let step = this._past[this._past.length - 1];
            let previous: any; 
            if ($eq.collections.count(step) === 1 && (($v2184) => (($v2184 != null && $v2184.typed === true && $v2184.breaksLine === false) && (previous = $v2184, true)))($eq.collections.item(step, 0))) {
                this._past[this._past.length - 1] = [$eq.withPatch(previous, { insertedText: previous.insertedText + edit.insertedText, selectionAfter: edit.selectionAfter })];
                this._runEnd = edit.insertedRange.end;
                return;
            }
        }
        this._past.push(edits);
        if (this._past.length > this.limit) this._past.splice(0, 1);
        this._runEnd = $eq.collections.count(edits) === 1 && edit.typed && !edit.breaksLine ? edit.insertedRange.end : new CodePosition(-1, -1);
    }

    break() {
        return this._runEnd = new CodePosition(-1, -1);
    }

    undo(document: CodeDocument) {
        let selection: any, made: any;
        const $r = (() => {
            selection = CodeRange.$zero();
            made = [];
            if (this._past.length === 0) return null;
            let step = this._past[this._past.length - 1];
            this._past.splice(this._past.length - 1, 1);
            this._future.push(step);
            this.break();
            let next = document;
            let undone: CodeEdit[] = [];
            for (let i = $eq.collections.count(step) - 1; i >= 0; i--) {
                let edit = $eq.collections.item(step, i);
                let replaced = new CodeRange(next.clamp(edit.insertedRange.start), next.clamp(edit.insertedRange.end));
                let removed = next.textIn(replaced);
                let end: any; 
                next = ($o => (end = $o.caret, $o.$))(next.replace(replaced, edit.removedText));
                undone.push(new CodeEdit(replaced, removed, next.textIn(new CodeRange(replaced.start, end)), edit.selectionAfter, edit.selectionBefore, false));
            }
            made = undone;
            selection = $eq.collections.item(step, 0).selectionBefore;
            return next;
        })();
        return { $: $r, selection, made };
    }

    redo(document: CodeDocument) {
        let selection: any, made: any;
        const $r = (() => {
            selection = CodeRange.$zero();
            made = [];
            if (this._future.length === 0) return null;
            let step = this._future[this._future.length - 1];
            this._future.splice(this._future.length - 1, 1);
            this._past.push(step);
            this.break();
            let next = document;
            let redone: CodeEdit[] = [];
            for (const edit of step) {
                let replaced = new CodeRange(next.clamp(edit.range.start), next.clamp(edit.range.end));
                let removed = next.textIn(replaced);
                let end: any; 
                next = ($o => (end = $o.caret, $o.$))(next.replace(replaced, edit.insertedText));
                redone.push(new CodeEdit(replaced, removed, next.textIn(new CodeRange(replaced.start, end)), edit.selectionBefore, edit.selectionAfter, false));
            }
            made = redone;
            selection = $eq.collections.item(step, $eq.collections.count(step) - 1).selectionAfter;
            return next;
        })();
        return { $: $r, selection, made };
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

