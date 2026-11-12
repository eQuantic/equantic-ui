import { $eq } from "@equantic/runtime";
import { EmittedShape } from "./EmittedShape";

export class EmittedSquare extends EmittedShape {
    constructor(side: number = 0) {
        const $$side: number = side;
        super();
        this.side = $$side;
    }

    side!: number;

    toJSON() {
        return $eq.json(this, ['side']);
    }

    toString(): string {
        return 'eQuantic.UI.Web.Tests.Fixtures.EmittedSquare';
    }
}

