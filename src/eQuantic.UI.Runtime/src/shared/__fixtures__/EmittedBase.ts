import { $eq } from "@equantic/runtime";

export class EmittedBase {
    constructor(name: string = null!) {
        this.name = name;
    }

    name!: string;

    toJSON() {
        return $eq.json(this, ['name']);
    }

    toString(): string {
        return 'eQuantic.UI.Web.Tests.Fixtures.EmittedBase';
    }
}

