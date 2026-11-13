import { EmittedShape } from "./EmittedShape";

export abstract class EmittedFlex extends EmittedShape {
    constructor() {
        const $$children: EmittedShape[] = [];
        super();
        this.children = $$children;
    }

    children!: EmittedShape[];

    toString(): string {
        return 'eQuantic.UI.Web.Tests.Fixtures.EmittedFlex';
    }
}

