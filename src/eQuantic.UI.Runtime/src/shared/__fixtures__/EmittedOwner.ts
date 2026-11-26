import { EmittedShapeBase } from "./EmittedShapeBase";

export class EmittedOwner<T extends EmittedShapeBase> {
    constructor() {}

    keep(value: any) {
        return (value as T);
    }
}

