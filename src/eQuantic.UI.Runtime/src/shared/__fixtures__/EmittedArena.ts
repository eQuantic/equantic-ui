import { $eq } from "@equantic/runtime";
import { EmittedPointer } from "./EmittedPointer";

export class EmittedArena {
    constructor() {
        this._arenas = $eq.collections.dictionary();
    }

    _arenas!: any;

    count(pointer: number) {
        let arena!: EmittedPointer; 
        if (!(($0) => ($0.has(pointer) ? ((arena = $0.get(pointer)), true) : ((arena = null!), false)))(this._arenas)) return 0;
        let total = 0;
        for (const member of $eq.collections.listWalk(arena.members)) total = total + member.length | 0;
        return total;
    }

    toJSON() {
        return $eq.json(this, []);
    }

    toString(): string {
        return 'eQuantic.UI.Web.Tests.Fixtures.EmittedArena';
    }
}

