export class EmittedChain {
    constructor() {
        this.parent = null;
    }

    parent!: EmittedChain | null;

    depth() {
        let depth = 0;
        for (let at: EmittedChain | null = this; at != null; at = at.parent) depth++;
        return depth;
    }

    toString(): string {
        return 'eQuantic.UI.Web.Tests.Fixtures.EmittedChain';
    }
}

