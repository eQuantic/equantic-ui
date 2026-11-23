import { $eq, GestureArena, GestureArenaEntry, GestureDeadline, GestureRecognizer, GestureRecognizerFactory, GestureRecognizerMember, PointerEvent } from "../runtime-exports";

export class GestureRouter {
    constructor() {
        this._routes = $eq.collections.dictionary();
        this._kept = $eq.collections.dictionary(null, 'own');
        this._makers = $eq.collections.dictionary(null, 'own');
        this._deadlines = [];
        this.arena = new GestureArena();
    }

    _routes!: any;
    _kept!: any;
    _makers!: any;
    _deadlines!: GestureDeadline[];
    arena!: GestureArena;

    get nextDeadlineMs(): number | null {
        return (this.earliest()?.dueMs ?? null);
    }

    recognizers(position: any, gestures: GestureRecognizerFactory[]) {
        let kept: any; let makers: any; 
        if (!(($0) => ($0.has(position) ? ((kept = $0.get(position)), true) : ((kept = null), false)))(this._kept) || !(($0) => ($0.has(position) ? ((makers = $0.get(position)), true) : ((makers = null), false)))(this._makers)) {
            kept = [];
            makers = [];
            $eq.mapSet(this._kept, position, kept);
            $eq.mapSet(this._makers, position, makers);
        }
        for (let i = 0; i < $eq.collections.count(gestures); i = i + 1 | 0) {
            let factory = $eq.collections.item(gestures, i);
            if (i < kept.length && !$eq.types.same($eq.types.of(factory), $eq.types.of(makers[i]))) {
                this.retire(kept[i]);
                $eq.collections.listSet(kept, i, GestureRouter.made(factory, position));
            } else if (i >= kept.length) {
                $eq.collections.listAdd(kept, GestureRouter.made(factory, position));
                $eq.collections.listAdd(makers, factory);
            }
            $eq.collections.listSet(makers, i, factory);
            factory.initialize(kept[i]);
        }
        while (kept.length > $eq.collections.count(gestures)) {
            this.retire(kept[kept.length - 1 | 0]);
            $eq.collections.listRemoveAt(kept, kept.length - 1 | 0);
            $eq.collections.listRemoveAt(makers, makers.length - 1 | 0);
        }
        return kept;
    }

    forget(position: any) {
        let kept: any; 
        if (!(($0) => ($0.has(position) ? ((kept = $0.get(position)), true) : ((kept = null), false)))(this._kept)) return;
        this._kept.delete(position);
        this._makers.delete(position);
        for (const recognizer of $eq.collections.listWalk(kept)) this.retire(recognizer);
    }

    down(down: PointerEvent, members: any[], inBoxOf: ((value: any) => PointerEvent) | null = null) {
        let pointer = down.pointer;
        if (this.arena.isOpen(pointer) || this._routes.has(pointer)) this.end(pointer, true);
        let routed: GestureRecognizer[] = [];
        $eq.mapSet(this._routes, pointer, routed);
        let entries: (GestureArenaEntry | null)[] = ($eq.collections.listCapacity($eq.collections.count(members)), []);
        for (const member of $eq.collections.sequenceWalk(members)) {
            let recognizer: any; 
            if ((member instanceof GestureRecognizer && (recognizer = member, true))) {
                let local = GestureRouter.inBox(recognizer, down, inBoxOf);
                if (recognizer.retired || !recognizer.isPointerAllowed(local)) {
                    $eq.collections.listAdd(entries, null);
                    continue;
                }
                let entry = this.arena.add(pointer, new GestureRecognizerMember(recognizer));
                $eq.collections.listAdd(routed, recognizer);
                $eq.collections.listAdd(entries, entry);
                recognizer.join(this, entry, local);
            } else {
                $eq.collections.listAdd(entries, this.arena.add(pointer, member));
            }
        }
        this.arena.close(pointer);
        return entries;
    }

    route(ev: PointerEvent, inBoxOf: ((value: any) => PointerEvent) | null = null) {
        let pointer = ev.pointer;
        let routed: any; 
        if ((($0) => ($0.has(pointer) ? ((routed = $0.get(pointer)), true) : ((routed = null), false)))(this._routes)) {
            for (const recognizer of $eq.collections.listWalk($eq.collections.listCopy(routed))) if (recognizer.tracks(pointer)) recognizer.hear(GestureRouter.inBox(recognizer, ev, inBoxOf));
        }
        if (ev.phase === 'up') this.end(pointer, false); else if (ev.phase === 'cancel') this.end(pointer, true);
    }

    tick(nowMs: number) {
        for (const deadline of $eq.collections.listWalk(this._deadlines)) deadline.dueMs ?? (deadline.dueMs = nowMs + deadline.milliseconds);
        for (let due: any; (due = this.earliest()) != null && $eq.nullable.cmp(due.dueMs, nowMs, ($a, $b) => $a <= $b);) {
            $eq.collections.remove(this._deadlines, due, 'item');
            due.recognizer.exceed(due.pointer);
        }
    }

    startDeadline(recognizer: GestureRecognizer, pointer: number, milliseconds: number) {
        $eq.collections.removeAll(this._deadlines, (deadline) => (deadline.recognizer === recognizer));
        $eq.collections.listAdd(this._deadlines, new GestureDeadline(recognizer, pointer, milliseconds));
    }

    dropDeadline(recognizer: GestureRecognizer, pointer: number) {
        return $eq.collections.removeAll(this._deadlines, (deadline) => (deadline.recognizer === recognizer) && deadline.pointer === pointer);
    }

    earliest() {
        let earliest: GestureDeadline | null = null;
        for (const deadline of $eq.collections.listWalk(this._deadlines)) { let due: any; if ((due = deadline.dueMs) != null && (earliest == null || $eq.nullable.cmp(due, earliest.dueMs, ($a, $b) => $a < $b))) earliest = deadline; }
        return earliest;
    }

    end(pointer: number, cancelled: boolean) {
        let routed: any; 
        if ((($0) => ($0.has(pointer) ? ((routed = $0.get(pointer)), true) : ((routed = null), false)))(this._routes)) {
            this._routes.delete(pointer);
            for (const recognizer of $eq.collections.listWalk(routed)) recognizer.ended(pointer);
        }
        if (cancelled) this.arena.cancel(pointer); else this.arena.sweep(pointer);
    }

    static made(factory: GestureRecognizerFactory, position: any) {
        let made = factory.create();
        made.position = position;
        return made;
    }

    retire(recognizer: GestureRecognizer) {
        recognizer.retire();
        $eq.collections.removeAll(this._deadlines, (deadline) => (deadline.recognizer === recognizer));
        for (const routed of this._routes.values()) $eq.collections.remove(routed, recognizer, 'item');
    }

    static inBox(recognizer: GestureRecognizer, ev: PointerEvent, inBoxOf: ((value: any) => PointerEvent) | null) {
        let position: any; 
        return (position = recognizer.position) != null && !(inBoxOf == null) ? inBoxOf(position) : ev;
    }

    toString(): string {
        return 'eQuantic.UI.Primitives.GestureRouter';
    }
}

