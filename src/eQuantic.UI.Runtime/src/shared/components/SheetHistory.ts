import { $eq, SheetEdit } from "../runtime-exports";

export class SheetHistory {
    constructor() {
        this._undo = [];
        this._redo = [];
    }

    _undo!: SheetEdit[];
    _redo!: SheetEdit[];

    get canUndo(): boolean {
        return this._undo.length > 0;
    }

    get canRedo(): boolean {
        return this._redo.length > 0;
    }

    push(edit: SheetEdit) {
        $eq.collections.listAdd(this._undo, edit);
        $eq.collections.listClear(this._redo);
    }

    popUndo() {
        if (this._undo.length === 0) return null;
        let edit = (($0) => $0[$0.length - 1])(this._undo);
        $eq.collections.listRemoveAt(this._undo, this._undo.length - 1);
        $eq.collections.listAdd(this._redo, edit);
        return edit;
    }

    popRedo() {
        if (this._redo.length === 0) return null;
        let edit = (($0) => $0[$0.length - 1])(this._redo);
        $eq.collections.listRemoveAt(this._redo, this._redo.length - 1);
        $eq.collections.listAdd(this._undo, edit);
        return edit;
    }

    clear() {
        $eq.collections.listClear(this._undo);
        $eq.collections.listClear(this._redo);
    }

    toString(): string {
        return 'eQuantic.UI.Sheet.SheetHistory';
    }
}

