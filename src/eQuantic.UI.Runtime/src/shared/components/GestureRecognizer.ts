import { $eq, $GestureDispositionValue, GestureArenaEntry, GestureRouter, PointerEvent } from "../runtime-exports";

export abstract class GestureRecognizer {
    static $interfaces = ['eQuantic.UI.Primitives.IGestureArenaMember'];

    constructor() {
        this._entries = $eq.collections.dictionary<number, GestureArenaEntry>();
        this._tracked = [];
        this._router = null;
        this._primary = null;
        this.position = null;
        this.retired = false;
    }

    _entries!: any;
    _tracked!: number[];
    _router!: GestureRouter | null;
    _primary!: number | null;
    position!: any;
    retired!: boolean;

    get trackedPointers(): number[] {
        return this._tracked;
    }

    isPointerAllowed(_down: PointerEvent) {
        return true;
    }

    addAllowedPointer(_down: PointerEvent) {}

    abstract handleEvent(ev: PointerEvent): void;

    acceptGesture(_pointer: number) {}

    rejectGesture(_pointer: number) {}

    didExceedDeadline(_pointer: number) {}

    resolve(disposition: $GestureDispositionValue) {
        let entries: GestureArenaEntry[] = $eq.collections.listCopy(this._entries.values());
        for (const entry of $eq.collections.listWalk(entries)) entry.resolve(disposition);
    }

    resolvePointer(pointer: number, disposition: $GestureDispositionValue) {
        let entry!: GestureArenaEntry; 
        if ((($0) => ($0.has(pointer) ? ((entry = $0.get(pointer)), true) : ((entry = null!), false)))(this._entries)) entry.resolve(disposition);
    }

    stopTrackingPointer(pointer: number) {
        $eq.collections.remove(this._tracked, pointer);
        this._router?.dropDeadline(this, pointer);
    }

    startDeadline(milliseconds: number) {
        let router: any; let pointer: any; 
        if ((router = this._router) != null && (pointer = this._primary) != null) router.startDeadline(this, pointer, milliseconds);
    }

    join(router: GestureRouter, entry: GestureArenaEntry, down: PointerEvent) {
        this._router = router;
        $eq.mapSet(this._entries, down.pointer, entry);
        if (!this._tracked.includes(down.pointer)) $eq.collections.listAdd(this._tracked, down.pointer);
        this._primary = down.pointer;
        this.addAllowedPointer(down);
    }

    tracks(pointer: number) {
        return !this.retired && this._tracked.includes(pointer);
    }

    hear(ev: PointerEvent) {
        return this.handleEvent(ev);
    }

    exceed(pointer: number) {
        if (this.tracks(pointer)) this.didExceedDeadline(pointer);
    }

    won(pointer: number) {
        this._entries.delete(pointer);
        if (!this.retired) this.acceptGesture(pointer);
    }

    lost(pointer: number) {
        this._entries.delete(pointer);
        this.stopTrackingPointer(pointer);
        if (!this.retired) this.rejectGesture(pointer);
    }

    ended(pointer: number) {
        return this.stopTrackingPointer(pointer);
    }

    retire() {
        this.retired = true;
        this.resolve('rejected');
        for (const pointer of $eq.collections.listWalk($eq.collections.listCopy(this._tracked))) this.stopTrackingPointer(pointer);
    }

    toString(): string {
        return 'eQuantic.UI.Primitives.GestureRecognizer';
    }
}

