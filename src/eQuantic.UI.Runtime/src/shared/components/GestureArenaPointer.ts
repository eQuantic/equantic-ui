import { $GestureArenaPhaseValue } from "../runtime-exports";

export class GestureArenaPointer {
    constructor(pointer: number) {
        this.pointer = 0;
        this.members = [];
        this.phase = 'open';
        this.eagerWinner = null;
        this.pointer = pointer;
    }

    pointer!: number;
    members!: any[];
    phase!: $GestureArenaPhaseValue;
    eagerWinner!: any;

    toString(): string {
        return 'eQuantic.UI.Primitives.GestureArenaPointer';
    }
}

