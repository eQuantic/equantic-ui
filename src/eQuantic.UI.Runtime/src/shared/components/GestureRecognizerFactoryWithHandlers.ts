import { GestureRecognizer } from "../runtime-exports";
import { GestureRecognizerFactory } from "./GestureRecognizerFactory";

export class GestureRecognizerFactoryWithHandlers extends GestureRecognizerFactory {
    constructor(create: () => T, initialize: (t: T) => void) {
        super();
        this._create = null!;
        this._initialize = null!;
        this._create = create;
        this._initialize = initialize;
    }

    _create!: () => T;
    _initialize!: (t: T) => void;

    create() {
        return this._create();
    }

    initialize(recognizer: GestureRecognizer) {
        return this._initialize(recognizer);
    }
}

