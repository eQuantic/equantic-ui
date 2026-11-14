import { CellRef, SheetCellSnapshot, SheetEditKindValue, SheetRange, SheetSelection } from "../runtime-exports";

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
        this.selectionBefore = new SheetSelection(new SheetRange(new CellRef(0, 0)));
        this.selectionAfter = new SheetSelection(new SheetRange(new CellRef(0, 0)));
    }

    kind!: SheetEditKindValue;
    before!: SheetCellSnapshot[];
    after!: SheetCellSnapshot[];
    at!: number;
    count!: number;
    removed!: SheetCellSnapshot[];
    oldSize!: number;
    newSize!: number;
    selectionBefore!: SheetSelection;
    selectionAfter!: SheetSelection;

    toString(): string {
        return 'eQuantic.UI.Sheet.SheetEdit';
    }
}

