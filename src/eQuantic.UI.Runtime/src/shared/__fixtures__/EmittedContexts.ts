import { BuildContext, ComponentContext } from "@equantic/runtime";

export class EmittedContexts {
    constructor() {
        this._context = null;
    }

    _context!: BuildContext | null;

    for(theme: any) {
        return this._context ?? (this._context = new ComponentContext(theme));
    }

    toString(): string {
        return 'eQuantic.UI.Web.Tests.Fixtures.EmittedContexts';
    }
}

