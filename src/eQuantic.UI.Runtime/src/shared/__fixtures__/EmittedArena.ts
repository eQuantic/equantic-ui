import { $eq } from "@equantic/runtime";
import { EmittedPointer } from "./EmittedPointer";

export class EmittedArena {
    constructor() {
        this._arenas = $eq.collections.dictionary<number, EmittedPointer>();
        this._routes = $eq.collections.dictionary<number, any>();
        this._last = null;
    }

    _arenas!: any;
    _routes!: any;
    _last!: EmittedPointer | null;

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

    drop(pointer: number) {
        let seen!: EmittedPointer | null; 
        if ((($0) => ($0.has(pointer) ? ((seen = $0.get(pointer)), true) : ((seen = null!), false)))(this._arenas)) seen = null;
        return seen == null ? 0 : 1;
    }

    remember(pointer: number) {
        return (($0) => ($0.has(pointer) ? ((this._last = $0.get(pointer)), true) : ((this._last = null!), false)))(this._arenas);
    }

    toJSON() {
        return $eq.json(this, []);
    }

    toString(): string {
        return 'eQuantic.UI.Web.Tests.Fixtures.EmittedArena';
    }
}

