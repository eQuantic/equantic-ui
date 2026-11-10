import { EmittedBase } from "./EmittedBase";

export class EmittedBag extends EmittedBase {
    constructor(name: string) {
        const $$_items: (string | null)[] = [];
        super(name);
        this._items = $$_items;
    }

    _items!: (string | null)[];

    get count(): number {
        return this._items.length;
    }

    toString(): string {
        return 'eQuantic.UI.Web.Tests.Fixtures.EmittedBag';
    }
}

