import { EmittedCard } from "./EmittedCard";
import { EmittedFramed } from "./EmittedFramed";
import { EmittedRow } from "./EmittedRow";
import { EmittedShape } from "./EmittedShape";
import { EmittedSquare } from "./EmittedSquare";

export class EmittedShapes {
    static sideOf(shape: EmittedShape) {
        return (shape as EmittedSquare).side;
    }

    static inside(shape: EmittedShape) {
        return (shape as EmittedCard).child;
    }

    static count(shape: EmittedShape) {
        return (shape as EmittedRow).children.length;
    }

    static framed(shape: EmittedShape) {
        return (shape as EmittedFramed).child;
    }
}

