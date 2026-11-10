export class EmittedBase {
    constructor(name: string = null!) {
        this.name = name;
    }

    name!: string;

    toString(): string {
        return 'eQuantic.UI.Web.Tests.Fixtures.EmittedBase';
    }
}

