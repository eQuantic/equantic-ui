import { $eq, CodeDocument, CodeEdit, CodePosition } from "@equantic/runtime";
export class CodeHistory {
    constructor(props?: any) {
        this._past = []; this._future = []; this._runEnd = new CodePosition(-1, -1);  if (props && typeof props === 'object') Object.assign(this, props);
    }

    _past: CodeEdit[];
    _future: CodeEdit[];
    _runEnd: CodePosition;
    limit: number = 500;

    get canUndo(): boolean {
        return this._past.length > 0;
    }

    get canRedo(): boolean {
        return this._future.length > 0;
    }

    record(edit: CodeEdit) {
        this._future.splice(0);
        if (edit.isSimpleInsert && this._past.length > 0 && $eq.equals(edit.range.start, this._runEnd)) {
            let previous = this._past[this._past.length - 1];
            if (previous.isSimpleInsert) {
                this._past[this._past.length - 1] = $eq.withPatch(previous, { insertedText: previous.insertedText + edit.insertedText, selectionAfter: edit.selectionAfter });
                this._runEnd = edit.insertedRange.end;
                return;
            }
        }
        this._past.push(edit);
        if (this._past.length > this.limit) this._past.splice(0, 1);
        this._runEnd = edit.isSimpleInsert ? edit.insertedRange.end : new CodePosition(-1, -1);
    }

    break() {
        return this._runEnd = new CodePosition(-1, -1);
    }

    undo(document: CodeDocument) {
        let selection; const $r = (() => { selection = undefined;
        if (this._past.length === 0) return null;
        let edit = this._past[this._past.length - 1];
        this._past.splice(this._past.length - 1, 1);
        this._future.push(edit);
        this.break();
        let next = ($o => ($o.$))(document.replace(edit.insertedRange, edit.removedText));
        selection = edit.selectionBefore;
        return next; })(); return { $: $r, selection };
    }

    redo(document: CodeDocument) {
        let selection; const $r = (() => { selection = undefined;
        if (this._future.length === 0) return null;
        let edit = this._future[this._future.length - 1];
        this._future.splice(this._future.length - 1, 1);
        this._past.push(edit);
        this.break();
        let next = ($o => ($o.$))(document.replace(edit.range, edit.insertedText));
        selection = edit.selectionAfter;
        return next; })(); return { $: $r, selection };
    }

    clear() {
        this._past.splice(0);
        this._future.splice(0);
        this.break();
    }
}

