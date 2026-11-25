export class ElementIds {
    constructor() {
        this._last = 0;
    }

    _last!: number;

    next() {
        return this._last = this._last + 1 | 0;
    }

    toString(): string {
        return 'eQuantic.UI.Primitives.ElementIds';
    }
}

