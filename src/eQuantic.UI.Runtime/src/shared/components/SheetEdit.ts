import { SheetCellSnapshot, SheetEditKindValue, SheetRange } from "../runtime-exports";

export class SheetEdit {
    constructor() {
        this.kind = 'setCells';
        this.before = [];
        this.after = [];
        this.at = 0;
        this.count = 0;
        this.removed = [];
        this.oldSize = 0;
        this.newSize = 0;
        this.selectionBefore = SheetRange.$zero();
        this.selectionAfter = SheetRange.$zero();
    }

    kind!: SheetEditKindValue;
    before!: SheetCellSnapshot[];
    after!: SheetCellSnapshot[];
    at!: number;
    count!: number;
    removed!: SheetCellSnapshot[];
    oldSize!: number;
    newSize!: number;
    selectionBefore!: SheetRange;
    selectionAfter!: SheetRange;
}

