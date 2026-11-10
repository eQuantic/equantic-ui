export class EmittedItem {
    constructor(depth: number = 0) {
        this.depth = depth;
    }

    depth!: number;

    toString(): string {
        return 'eQuantic.UI.Web.Tests.Fixtures.EmittedItem';
    }
}

