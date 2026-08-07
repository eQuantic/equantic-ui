import { $eq, CellRef } from "@equantic/runtime";

export class SheetCellSnapshot { declare cell: CellRef; declare value: string; constructor(cell: any = null, value: any = null) { this.cell = cell; this.value = value; } equals(o: unknown) { return o instanceof SheetCellSnapshot && $eq.equals(this.cell, o.cell) && $eq.equals(this.value, o.value); } with(patch: any) { return new SheetCellSnapshot(('cell' in patch ? patch.cell : this.cell), ('value' in patch ? patch.value : this.value)); } toString() { return `SheetCellSnapshot { Cell = ${this.cell}, Value = ${this.value} }`; } }
