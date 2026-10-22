import { TableSpans } from "../runtime-exports";

export class LeadingSpans extends TableSpans {
    constructor(rest: TableSpans, lead: number) {
        super();
        this._rest = null!;
        this._lead = 0;
        this._rest = rest;
        this._lead = lead;
    }

    _rest!: TableSpans;
    _lead!: number;

    get rest(): TableSpans {
        return this._rest;
    }

    get lead(): number {
        return this._lead;
    }

    get count(): number {
        return this._rest.count + 1;
    }

    get total(): number {
        return this._lead + this._rest.total;
    }

    extentOf(index: number) {
        return index <= 0 ? this._lead : this._rest.extentOf(index - 1);
    }

    offsetOf(index: number) {
        return index <= 0 ? 0 : this._lead + this._rest.offsetOf(index - 1);
    }

    indexAt(offset: number) {
        return offset < this._lead || this._rest.count === 0 ? 0 : this._rest.indexAt(offset - this._lead) + 1;
    }
}

