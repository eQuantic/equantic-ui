import { GestureRecognizer } from "../runtime-exports";

export abstract class GestureRecognizerFactory {
    constructor() {}

    abstract create(): GestureRecognizer;
    abstract initialize(recognizer: GestureRecognizer): void;
    abstract makes(recognizer: GestureRecognizer): boolean;

    toString(): string {
        return 'eQuantic.UI.Primitives.GestureRecognizerFactory';
    }
}

