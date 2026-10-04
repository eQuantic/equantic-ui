import { $eq, VisualNode } from "../runtime-exports";

export class DataRow { declare key: string; declare cells: VisualNode[]; constructor(key: any = null, cells: any = null) { this.key = key; this.cells = cells; } equals(o: unknown) { return o instanceof DataRow && o.constructor === this.constructor && $eq.equals(this.key, o.key) && $eq.equals(this.cells, o.cells); } with(patch: any): DataRow { return $eq.withPatch(this, patch); } toString() { return `DataRow { Key = ${this.key}, Cells = ${this.cells} }`; } }
