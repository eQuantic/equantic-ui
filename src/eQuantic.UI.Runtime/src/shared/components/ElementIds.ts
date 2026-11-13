export class ElementIds {
    constructor() {
        this._last = 0;
    }

    _last!: number;

    next() {
        return ++this._last;
    }

    toString(): string {
        return 'eQuantic.UI.Primitives.ElementIds';
    }
}

