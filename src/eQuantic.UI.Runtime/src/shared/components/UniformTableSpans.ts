import { $eq, TableSpans } from "../runtime-exports";

export class UniformTableSpans extends TableSpans {
    constructor(count: number, extent: number) {
        super();
        this.$count = 0;
        this.extent = 0;
        this.count = Math.max(0, count);
        this.extent = Math.max(0, extent);
    }

    $count!: number;
    extent!: number;

    get count(): number {
        return this.$count;
    }

    set count(value: number) {
        this.$count = value;
    }

    get total(): number {
        return this.count * this.extent;
    }

    extentOf(_index: number) {
        return this.extent;
    }

    offsetOf(index: number) {
        return Math.min(Math.max(index, 0), this.count) * this.extent;
    }

    indexAt(offset: number) {
        if (this.count === 0 || this.extent <= 0) return 0;
        return $eq.num.saturate(Math.min(Math.max(Math.floor(offset / this.extent), 0), this.count - 1));
    }

    toJSON() {
        return $eq.json(this);
    }
}

