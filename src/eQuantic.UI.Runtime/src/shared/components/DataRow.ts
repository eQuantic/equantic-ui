import { $eq } from "../runtime-exports";

export class DataRow { declare key: string | null; declare cells: any; constructor(key: any = null, cells: any = null) { this.key = key; this.cells = cells; } equals(o: unknown) { return o instanceof DataRow && $eq.equals(this.key, o.key) && $eq.equals(this.cells, o.cells); } with(patch: any) { return new DataRow(('key' in patch ? patch.key : this.key), ('cells' in patch ? patch.cells : this.cells)); } toString() { return `DataRow { Key = ${this.key}, Cells = ${this.cells} }`; } }
