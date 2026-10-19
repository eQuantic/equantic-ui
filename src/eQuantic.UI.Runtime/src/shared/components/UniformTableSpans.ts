export class UniformTableSpans {
    constructor(count: number, extent: number) {
        this.count = 0;
        this.extent = 0;
        this.count = Math.max(0, count);
        this.extent = Math.max(0, extent);
    }

    count!: number;
    extent!: number;

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
        return Math.min(Math.max((Math.trunc(Math.floor(offset / this.extent)) | 0), 0), this.count - 1);
    }

    toString(): string {
        return 'eQuantic.UI.Components.UniformTableSpans';
    }
}

