export abstract class EmittedScopedBase {
    constructor(count: number) {
        this.count = 0;
        this.count = count;
    }

    count!: number;

    toString(): string {
        return 'eQuantic.UI.Web.Tests.Fixtures.EmittedScopedBase';
    }
}

