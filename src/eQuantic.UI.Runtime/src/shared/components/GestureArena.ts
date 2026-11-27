import { $eq, $GestureDispositionValue, GestureArenaEntry, GestureArenaPointer } from "../runtime-exports";

export class GestureArena {
    constructor() {
        this._arenas = $eq.collections.dictionary<number, GestureArenaPointer>();
    }

    _arenas!: any;

    add(pointer: number, member: any) {
        let arena!: GestureArenaPointer; 
        if (!(($0) => ($0.has(pointer) ? ((arena = $0.get(pointer)), true) : ((arena = null!), false)))(this._arenas)) {
            arena = new GestureArenaPointer(pointer);
            $eq.mapSet(this._arenas, pointer, arena);
        } else if (arena.phase !== 'open') {
            throw $eq.exceptions.create(['System.InvalidOperationException', 'System.SystemException', 'System.Exception'], `The arena of pointer ${$eq.text.format(pointer, null, undefined, undefined, 'int32')} is closed: no member joins it any more.`);
        }
        $eq.collections.listAdd(arena.members, member);
        return new GestureArenaEntry(this, arena, member);
    }

    isOpen(pointer: number) {
        return this._arenas.has(pointer);
    }

    close(pointer: number) {
        let arena!: GestureArenaPointer; 
        if (!(($0) => ($0.has(pointer) ? ((arena = $0.get(pointer)), true) : ((arena = null!), false)))(this._arenas) || arena.phase !== 'open') return;
        arena.phase = 'closed';
        this.tryToDecide(arena);
    }

    sweep(pointer: number) {
        let arena!: GestureArenaPointer; 
        if (!(($0) => ($0.has(pointer) ? ((arena = $0.get(pointer)), true) : ((arena = null!), false)))(this._arenas)) return;
        this.decide(arena);
        if (arena.members.length === 0) return;
        arena.members[0].acceptGesture(pointer);
        for (let i = 1; i < arena.members.length; i = i + 1 | 0) arena.members[i].rejectGesture(pointer);
    }

    cancel(pointer: number) {
        let arena!: GestureArenaPointer; 
        if (!(($0) => ($0.has(pointer) ? ((arena = $0.get(pointer)), true) : ((arena = null!), false)))(this._arenas)) return;
        this.decide(arena);
        for (const member of $eq.collections.listWalk(arena.members)) member.rejectGesture(pointer);
    }

    resolve(arena: GestureArenaPointer, member: any, disposition: $GestureDispositionValue) {
        if (arena.phase === 'decided') return;
        let at = GestureArena.indexOf(arena.members, member);
        if (at < 0) return;
        if (disposition === 'rejected') {
            $eq.collections.listRemoveAt(arena.members, at);
            if ((arena.eagerWinner === member)) arena.eagerWinner = null;
            member.rejectGesture(arena.pointer);
            if (arena.phase === 'closed') this.tryToDecide(arena);
            return;
        }
        if (arena.phase === 'open') {
            arena.eagerWinner ?? (arena.eagerWinner = member);
            return;
        }
        this.decideFor(arena, member);
    }

    tryToDecide(arena: GestureArenaPointer) {
        if (arena.members.length === 0) {
            this.decide(arena);
            return;
        }
        if (arena.members.length === 1) {
            this.decide(arena);
            arena.members[0].acceptGesture(arena.pointer);
            return;
        }
        let eager: any; 
        if ((eager = arena.eagerWinner) != null) this.decideFor(arena, eager);
    }

    decideFor(arena: GestureArenaPointer, winner: any) {
        this.decide(arena);
        for (const member of $eq.collections.listWalk(arena.members)) if (!(member === winner)) member.rejectGesture(arena.pointer);
        winner.acceptGesture(arena.pointer);
    }

    decide(arena: GestureArenaPointer) {
        arena.phase = 'decided';
        let current!: GestureArenaPointer; 
        if ((($0, $1) => ($0.has($1) ? ((current = $0.get($1)), true) : ((current = null!), false)))(this._arenas, arena.pointer) && (current === arena)) this._arenas.delete(arena.pointer);
    }

    static indexOf(members: any[], member: any) {
        for (let i = 0; i < members.length; i = i + 1 | 0) if ((members[i] === member)) return i;
        return -1;
    }

    toString(): string {
        return 'eQuantic.UI.Primitives.GestureArena';
    }
}

