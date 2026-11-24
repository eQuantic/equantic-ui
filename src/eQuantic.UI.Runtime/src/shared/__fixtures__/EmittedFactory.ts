import { $eq } from "@equantic/runtime";

export class EmittedFactory<T> {
    constructor(create: () => T, initialize: (t: T) => void) {
        this._create = null!;
        this._initialize = null!;
        this._create = create;
        this._initialize = initialize;
    }

    _create!: () => T;
    _initialize!: (t: T) => void;

    build() {
        let made = this._create();
        this._initialize(made);
        return made;
    }

    toJSON() {
        return $eq.json(this, []);
    }
}

