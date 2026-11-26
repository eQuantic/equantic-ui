import { $eq } from "@equantic/runtime";
import { EmittedShapeBase } from "./EmittedShapeBase";

export class EmittedMeasured<T extends EmittedShapeBase> {
    constructor(shape: T) {
        this._shape = null!;
        this._shape = shape;
    }

    _shape!: T;

    area() {
        return this._shape.area;
    }

    toJSON() {
        return $eq.json(this, []);
    }
}

