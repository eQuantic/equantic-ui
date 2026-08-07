import { SheetEdit } from "@equantic/runtime";
export class SheetHistory {
    constructor(props?: any) { this._undo = []; this._redo = [];  if (props && typeof props === 'object') Object.assign(this, props); }
    _undo: SheetEdit[];
    _redo: SheetEdit[];
    get canUndo(): boolean { return this._undo.length > 0; }
    get canRedo(): boolean { return this._redo.length > 0; }
    push(edit: SheetEdit) { this._undo.push(edit);this._redo.splice(0); }
    popUndo() { if (this._undo.length === 0) return null;let edit = this._undo[this._undo.length - 1];this._undo.splice(this._undo.length - 1, 1);this._redo.push(edit);return edit; }
    popRedo() { if (this._redo.length === 0) return null;let edit = this._redo[this._redo.length - 1];this._redo.splice(this._redo.length - 1, 1);this._undo.push(edit);return edit; }
    clear() { this._undo.splice(0);this._redo.splice(0); }
}

