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

    declare kind: SheetEditKindValue;
    declare before: SheetCellSnapshot[];
    declare after: SheetCellSnapshot[];
    declare at: number;
    declare count: number;
    declare removed: SheetCellSnapshot[];
    declare oldSize: number;
    declare newSize: number;
    declare selectionBefore: SheetRange;
    declare selectionAfter: SheetRange;
}

