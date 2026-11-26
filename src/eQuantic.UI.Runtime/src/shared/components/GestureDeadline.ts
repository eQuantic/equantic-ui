import { GestureRecognizer } from "../runtime-exports";

export class GestureDeadline {
    constructor(recognizer: GestureRecognizer, pointer: number, milliseconds: number) {
        this.recognizer = null!;
        this.pointer = 0;
        this.milliseconds = 0;
        this.dueMs = null;
        this.recognizer = recognizer;
        this.pointer = pointer;
        this.milliseconds = milliseconds;
    }

    recognizer!: GestureRecognizer;
    pointer!: number;
    milliseconds!: number;
    dueMs!: number | null;

    toString(): string {
        return 'eQuantic.UI.Primitives.GestureDeadline';
    }
}

