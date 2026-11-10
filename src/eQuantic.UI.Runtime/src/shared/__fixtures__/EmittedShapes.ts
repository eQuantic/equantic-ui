import { EmittedShape } from "./EmittedShape";
import { EmittedSquare } from "./EmittedSquare";

export class EmittedShapes {
    static sideOf(shape: EmittedShape) {
        return (shape as EmittedSquare).side;
    }
}

