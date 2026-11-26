import { $eq } from "@equantic/runtime";
import { EmittedScopedBase } from "./EmittedScopedBase";
import { EmittedLease } from "./EmittedLease";
import { EmittedPointer } from "./EmittedPointer";

export class EmittedScopes extends EmittedScopedBase {
    constructor() {
        const $$_first: number = (() => { let first!: EmittedPointer; return ($o => (first = $o.found, $o.$))(EmittedScopes.find(0)) ? first.members.length : 0; })();
        let held!: EmittedPointer;
        super(($o => (held = $o.found, $o.$))(EmittedScopes.find(1)) ? held.members.length : 0);
        this._first = $$_first;
    }

    _first!: number;
    static $slots: any = null;
    static $failure: any = null;

    static $init(): any {
        if (EmittedScopes.$slots === null) {
            if (EmittedScopes.$failure !== null) throw EmittedScopes.$failure;
            let $slots: any = EmittedScopes.$slots = {};
            try {
                $slots.pointers = null;
                $slots.pointers = $eq.collections.dictionary();
            } catch ($error) {
                EmittedScopes.$slots = null;
                throw EmittedScopes.$failure = $eq.exceptions.typeInitialization('eQuantic.UI.Web.Tests.Fixtures.EmittedScopes', $error);
            }
        }
        return EmittedScopes.$slots;
    }

    static get pointers(): any {
        return EmittedScopes.$init().pointers;
    }

    static set pointers(value: any) {
        EmittedScopes.$init().pointers = value;
    }

    static find(id: number) {
        let found!: EmittedPointer;
        const $r = (() => {
            let pointer!: EmittedPointer; 
            found = (($0) => ($0.has(id) ? ((pointer = $0.get(id)), true) : ((pointer = null!), false)))(EmittedScopes.pointers) ? pointer : new EmittedPointer();
            return EmittedScopes.pointers.has(id);
        })();
        return { $: $r, found };
    }

    static lease(id: number) {
        let held!: EmittedPointer;
        const $r = (() => {
            ($o => (held = $o.found, $o.$))(EmittedScopes.find(id));
            let total = 0;
            for (const member of $eq.collections.listWalk(held.members)) total = total + member.length | 0;
            return total > 0 ? new EmittedLease() : new EmittedLease();
        })();
        return { $: $r, held };
    }

    while() {
        let total = 0;
        let id = 0;
        for (let next: EmittedPointer = undefined!; (($0, $1) => ($0.has($1) ? ((next = $0.get($1)), true) : ((next = null!), false)))(EmittedScopes.pointers, (($old) => (id = $old + 1 | 0, $old))(id));) for (const member of $eq.collections.listWalk(next.members)) total = total + member.length | 0;
        return total;
    }

    do() {
        let id = 0;
        for (let next: EmittedPointer = undefined!, $again = true; $again; $again = (($0) => ($0.has(id) ? ((next = $0.get(id)), true) : ((next = null!), false)))(EmittedScopes.pointers) && next.members.length > 0) {
            id = id + 1 | 0;
        }
        return id;
    }

    for() {
        let total = 0;
        for (let next: EmittedPointer = undefined!, id = 0; (($0) => ($0.has(id) ? ((next = $0.get(id)), true) : ((next = null!), false)))(EmittedScopes.pointers); id = id + 1 | 0) for (const member of $eq.collections.listWalk(next.members)) total = total + member.length | 0;
        return total;
    }

    using(id: number) {
        let total = 0;
        {
            let held!: EmittedPointer;
            const _disposable_36ede7 = ($o => (held = $o.held, $o.$))(EmittedScopes.lease(id));
            try {
                for (const member of $eq.collections.listWalk(held.members)) total = total + member.length | 0;
            } finally {
                if (_disposable_36ede7 && typeof _disposable_36ede7.dispose === 'function') {
                    _disposable_36ede7.dispose();
                }
            }
        }
        return total;
    }

    filter(thrown: Error) {
        let total = 0;
        try {
            throw thrown;
        } catch ($e: any) {
            let e = $e;
            let held!: EmittedPointer;
            if ($eq.exceptions.filter(() => (($0, $1) => ($0.has($1) ? ((held = $0.get($1)), true) : ((held = null!), false)))(EmittedScopes.pointers, e.message.length))) {
                for (const member of $eq.collections.listWalk(held.members)) total = total + member.length | 0;
            } else {
                throw $e;
            }
        }
        return total;
    }

    section(kind: number) {
        let total = 0;
        {
            let held!: EmittedPointer;
            switch (kind) {
                case 1:
                    (($0) => ($0.has(kind) ? ((held = $0.get(kind)), true) : ((held = null!), false)))(EmittedScopes.pointers);
                    for (const member of $eq.collections.listWalk((held!).members)) total = total + member.length | 0;
                    break;
            }
        }
        return total;
    }

    guarded(kind: number) {
        let total = 0;
        {
            let held: EmittedPointer = undefined!;
            const $s = kind;
            if (($s > 1 && ((($0) => ($0.has(kind) ? ((held = $0.get(kind)), true) : ((held = null!), false)))(EmittedScopes.pointers)))) {
                for (const member of $eq.collections.listWalk(held.members)) total = total + member.length | 0;
            }
        }
        return total;
    }

    arm(kind: number) {
        return (() => {let held!: EmittedPointer;  const $s = kind; if (($s > 0) && ((($0) => ($0.has(kind) ? ((held = $0.get(kind)), true) : ((held = null!), false)))(EmittedScopes.pointers))) return held.members.length + this._first | 0; return 0; })();
    }

    toJSON() {
        return $eq.json(this, ['count']);
    }

    toString(): string {
        return 'eQuantic.UI.Web.Tests.Fixtures.EmittedScopes';
    }
}

