import { $eq } from "@equantic/runtime";
import { EmittedShapeBase } from "./EmittedShapeBase";

export class EmittedHandler<T extends EmittedShapeBase> {
    constructor(use: (t: T) => void) {
        this._use = null!;
        this._use = use;
    }

    _use!: (t: T) => void;

    give(shape: EmittedShapeBase) {
        return this._use((shape as T));
    }

    toJSON() {
        return $eq.json(this, []);
    }
}

