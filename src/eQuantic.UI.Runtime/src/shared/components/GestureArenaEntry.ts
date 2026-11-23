import { GestureArena, GestureArenaPointer, GestureDispositionValue } from "../runtime-exports";

export class GestureArenaEntry {
    constructor(arena: GestureArena, pointer: GestureArenaPointer, member: any) {
        this._arena = null!;
        this._pointer = null!;
        this._member = null;
        this._arena = arena;
        this._pointer = pointer;
        this._member = member;
    }

    _arena!: GestureArena;
    _pointer!: GestureArenaPointer;
    _member!: any;

    get pointer(): number {
        return this._pointer.pointer;
    }

    resolve(disposition: GestureDispositionValue) {
        return this._arena.resolve(this._pointer, this._member, disposition);
    }

    toString(): string {
        return 'eQuantic.UI.Primitives.GestureArenaEntry';
    }
}

