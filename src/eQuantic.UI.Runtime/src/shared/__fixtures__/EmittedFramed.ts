import { EmittedShape } from "./EmittedShape";

export abstract class EmittedFramed extends EmittedShape {
    constructor() {
        super();
        this.child = null;
    }

    child!: EmittedShape | null;

    toString(): string {
        return 'eQuantic.UI.Web.Tests.Fixtures.EmittedFramed';
    }
}

