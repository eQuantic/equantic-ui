import { $eq } from "@equantic/runtime";

export class EmittedItem {
    constructor(depth: number = 0) {
        this.depth = depth;
    }

    depth!: number;

    toJSON() {
        return $eq.json(this, ['depth']);
    }

    toString(): string {
        return 'eQuantic.UI.Web.Tests.Fixtures.EmittedItem';
    }
}

