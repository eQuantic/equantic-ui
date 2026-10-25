import { $eq, CodeChange, CodeEditorController, CodeRange } from "../runtime-exports";

export class CodeJumpList {
    constructor(editor: CodeEditorController) {
        this._editor = null!;
        this._places = [];
        this._index = -1;
        this._heard = CodeRange.$zero();
        this._walking = false;
        this._jumping = false;
        this._editor = editor;
        this._heard = editor.selection;
        this._places.push(this._heard);
        this._index = 0;
        editor.changed = $eq.delegates.combine(editor.changed, this.carry.bind(this));
        editor.selectionChanged = $eq.delegates.combine(editor.selectionChanged, this.heard.bind(this));
    }

    _editor!: CodeEditorController;
    _places!: CodeRange[];
    _index!: number;
    _heard!: CodeRange;
    _walking!: boolean;
    _jumping!: boolean;
    static distance: number = 10;
    static capacity: number = 50;

    get places(): CodeRange[] {
        return this._places;
    }

    get index(): number {
        return this._index;
    }

    get canGoBack(): boolean {
        return this._index > 0;
    }

    get canGoForward(): boolean {
        return this._index < this._places.length - 1;
    }

    back() {
        return this.walk(this._index - 1);
    }

    forward() {
        return this.walk(this._index + 1);
    }

    jump(move: () => void) {
        this._jumping = true;
        try {
            move();
        } finally {
            this._jumping = false;
        }
    }

    reset() {
        this._places.splice(0);
        this._heard = this._editor.selection;
        this._places.push(this._heard);
        this._index = 0;
    }

    walk(to: number) {
        if (to < 0 || to >= this._places.length) return false;
        this._index = to;
        let place = this._places[to];
        this._walking = true;
        try {
            this._editor.selection = place;
        } finally {
            this._walking = false;
        }
        this._heard = this._editor.selection;
        this._editor.revealInCenterIfOutsideView(place.focus);
        return true;
    }

    heard(selection: CodeRange) {
        if (this._walking) return;
        let lines = Math.abs(selection.start.line - this._heard.start.line);
        let newPlace = lines >= CodeJumpList.distance || this._jumping && lines > 0;
        this._heard = selection;
        if (newPlace && this._places[this._index].start.line !== selection.start.line) this.add(selection); else this._places[this._index] = selection;
    }

    add(place: CodeRange) {
        if (this._index < this._places.length - 1) this._places.splice(this._index + 1, this._places.length - this._index - 1);
        this._places.push(place);
        if (this._places.length > CodeJumpList.capacity) this._places.splice(0, 1);
        this._index = this._places.length - 1;
    }

    carry(change: CodeChange) {
        if (change.replaced) return;
        for (const edit of change.edits) {
            for (let i = 0; i < this._places.length; i++) this._places[i] = edit.carry(this._places[i]);
            this._heard = edit.carry(this._heard);
        }
    }

    toString(): string {
        return 'eQuantic.UI.Code.CodeJumpList';
    }
}

