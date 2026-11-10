export class EmittedBase {
    constructor(name: string) {
        this.name = null!;
        this.name = name;
    }

    name!: string;

    toString(): string {
        return 'eQuantic.UI.Web.Tests.Fixtures.EmittedBase';
    }
}

