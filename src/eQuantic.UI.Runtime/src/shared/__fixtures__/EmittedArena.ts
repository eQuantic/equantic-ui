import { $eq } from "@equantic/runtime";
import { EmittedPointer } from "./EmittedPointer";

export class EmittedArena {
    constructor() {
        this._arenas = $eq.collections.dictionary();
        this._routes = $eq.collections.dictionary();
    }

    _arenas!: any;
    _routes!: any;

    count(pointer: number) {
        let arena!: EmittedPointer; 
        if (!(($0) => ($0.has(pointer) ? ((arena = $0.get(pointer)), true) : ((arena = null!), false)))(this._arenas)) return 0;
        let total = 0;
        for (const member of $eq.collections.listWalk(arena.members)) total = total + member.length | 0;
        return total;
    }

    find(pointer: number) {
        let found: any;
        const $r = (() => {
            let arena!: EmittedPointer; 
            found = (($0) => ($0.has(pointer) ? ((arena = $0.get(pointer)), true) : ((arena = null!), false)))(this._arenas) ? arena : null;
            return found != null;
        })();
        return { $: $r, found };
    }

    forget(pointer: number) {
        let found!: EmittedPointer | null; 
        if (($o => (found = $o.found, $o.$))(this.find(pointer))) found = null;
        return found == null ? 0 : 1;
    }

    routed(pointer: number) {
        let total = 0;
        let routed!: EmittedPointer[]; 
        if ((($0) => ($0.has(pointer) ? ((routed = $0.get(pointer)), true) : ((routed = null!), false)))(this._routes)) for (const route of $eq.collections.listWalk(routed)) total = total + route.members.length | 0;
        return total;
    }

    toJSON() {
        return $eq.json(this, []);
    }

    toString(): string {
        return 'eQuantic.UI.Web.Tests.Fixtures.EmittedArena';
    }
}

