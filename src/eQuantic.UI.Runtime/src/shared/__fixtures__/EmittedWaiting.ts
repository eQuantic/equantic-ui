import { $eq, DateTime, Decimal, TimeSpan } from "@equantic/runtime";
import { EmittedPointer } from "./EmittedPointer";

export class EmittedWaiting {
    constructor() {
        this._queue = $eq.collections.queue<EmittedPointer>();
        this._stack = $eq.collections.stack<EmittedPointer>();
        this._list = $eq.collections.linkedList<EmittedPointer>();
        this._sorted = $eq.collections.sortedSet<number>(null, 'value');
        this._set = $eq.collections.hashSet<EmittedPointer>('item');
        this._pending = $eq.collections.queue<EmittedPointer>();
        this._ranks = $eq.collections.sortedSet<number>([3, 1], 'value');
        this._seen = $eq.collections.hashSetOf<EmittedPointer>([], 'item');
        this._next = null;
    }

    _queue!: any;
    _stack!: any;
    _list!: any;
    _sorted!: any;
    _set!: Set<EmittedPointer>;
    _pending!: any;
    _ranks!: any;
    _seen!: Set<EmittedPointer>;
    _next!: EmittedPointer | null;

    count() {
        return (((this._queue.count + this._stack.count | 0) + this._list.count | 0) + this._sorted.count | 0) + this._set.size | 0;
    }

    spread<T>(item: T) {
        let byDay: any = $eq.collections.dictionary<DateTime, Decimal>(null, true);
        let spans: Set<TimeSpan> = $eq.collections.hashSet<TimeSpan>(true);
        let ids = $eq.collections.sortedSet<string>();
        let maybe = $eq.collections.queue<EmittedPointer | null>();
        let kept = $eq.collections.stack<any>();
        kept.push(item);
        maybe.enqueue(null);
        $eq.mapSet(byDay, $eq.time.dateTime.unixEpoch(), $eq.num.dec("1.5"));
        $eq.collections.setAdd(spans, $eq.time.timeSpan.zero);
        ids.add('00000000-0000-0000-0000-000000000000');
        return (((byDay.size + spans.size | 0) + ids.count | 0) + maybe.count | 0) + kept.count | 0;
    }

    advance() {
        let queue = $eq.collections.queue<EmittedPointer>();
        let stack = $eq.collections.stack<EmittedPointer>();
        let list = $eq.collections.linkedList<EmittedPointer>();
        let sorted = $eq.collections.sortedSet<number>(null, 'value');
        let set: Set<EmittedPointer> = $eq.collections.hashSet<EmittedPointer>('item');
        let map: any = $eq.collections.dictionary<number, EmittedPointer>();
        let node = $eq.collections.linkedListNode<EmittedPointer>(new EmittedPointer());
        let undone = $eq.collections.stack<EmittedPointer>();
        let ranks = $eq.collections.sortedSet<number>([2, 1], 'value');
        let seen = $eq.collections.hashSetOf<EmittedPointer>([node.value], 'item');
        let held = node.value;
        let first: EmittedPointer | null = (list.first?.value ?? null);
        let least = sorted.count > 0 ? sorted.min ?? 0 : (ranks.min ?? 0) + (this._ranks.max ?? 0) | 0;
        $eq.collections.setAdd(set, held);
        return (($0) => ($0 !== undefined ? ((this._next = $0), true) : ((this._next = null!), false)))(queue.tryDequeue()) || (($0) => ($0 !== undefined ? ((this._next = $0), true) : ((this._next = null!), false)))(stack.tryPop()) || (map.has(least) ? ((this._next = map.get(least)), true) : ((this._next = null!), false)) || first != null || (($0) => ($0 !== undefined ? ((this._next = $0), true) : ((this._next = null!), false)))(this._queue.tryPeek()) || (($0) => ($0 !== undefined ? ((this._next = $0), true) : ((this._next = null!), false)))(undone.tryPop()) || (($0) => ($0 !== undefined ? ((this._next = $0), true) : ((this._next = null!), false)))(this._pending.tryDequeue()) || seen.has(held) || this._seen.size > 0;
    }

    toJSON() {
        return $eq.json(this, []);
    }

    toString(): string {
        return 'eQuantic.UI.Web.Tests.Fixtures.EmittedWaiting';
    }
}

