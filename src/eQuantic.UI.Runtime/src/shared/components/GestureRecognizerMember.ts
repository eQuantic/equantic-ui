import { GestureRecognizer } from "../runtime-exports";

export class GestureRecognizerMember {
    static $interfaces = ['eQuantic.UI.Primitives.IGestureArenaMember'];

    constructor(recognizer: GestureRecognizer) {
        this._recognizer = null!;
        this._recognizer = recognizer;
    }

    _recognizer!: GestureRecognizer;

    acceptGesture(pointer: number) {
        return this._recognizer.won(pointer);
    }

    rejectGesture(pointer: number) {
        return this._recognizer.lost(pointer);
    }

    toString(): string {
        return 'eQuantic.UI.Primitives.GestureRecognizerMember';
    }
}

