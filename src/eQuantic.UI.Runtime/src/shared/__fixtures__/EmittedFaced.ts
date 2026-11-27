import { $eq } from "@equantic/runtime";
import { IEmittedShaped } from "./IEmittedShaped";

export class EmittedFaced<T extends IEmittedShaped> {
    constructor(shape: T) {
        this._shape = null!;
        this._shape = shape;
    }

    _shape!: T;

    area() {
        return this._shape.area;
    }

    text() {
        return this._shape.describe('cm') + ' ' + this._shape.label;
    }

    toJSON() {
        return $eq.json(this, []);
    }
}

