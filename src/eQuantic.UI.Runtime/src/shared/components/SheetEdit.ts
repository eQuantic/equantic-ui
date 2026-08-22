import { SheetCellSnapshot, SheetEditKindValue, SheetRange } from "../runtime-exports";
export class SheetEdit {
    constructor(props?: any) {
        if (props && typeof props === 'object') Object.assign(this, props);
    }

    declare kind: SheetEditKindValue;
    before: SheetCellSnapshot[] = [];
    after: SheetCellSnapshot[] = [];
    at: number = 0;
    count: number = 0;
    removed: SheetCellSnapshot[] = [];
    oldSize: number = 0;
    newSize: number = 0;
    declare selectionBefore: SheetRange;
    declare selectionAfter: SheetRange;
}

