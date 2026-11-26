import { $eq } from "@equantic/runtime";

export class ArenaMember {
    static $interfaces = ['eQuantic.UI.Primitives.IGestureArenaMember'];

    constructor(name: string, heard: string[]) {
        this._name = null!;
        this._heard = null!;
        this._name = name;
        this._heard = heard;
    }

    _name!: string;
    _heard!: string[];

    acceptGesture(pointer: number) {
        return $eq.collections.listAdd(this._heard, `${this._name}.accept ${$eq.text.format(pointer, null, undefined, undefined, 'int32')}`);
    }

    rejectGesture(pointer: number) {
        return $eq.collections.listAdd(this._heard, `${this._name}.reject ${$eq.text.format(pointer, null, undefined, undefined, 'int32')}`);
    }

    toJSON() {
        return $eq.json(this, []);
    }

    toString(): string {
        return 'eQuantic.UI.Web.Tests.Fixtures.ArenaMember';
    }
}

