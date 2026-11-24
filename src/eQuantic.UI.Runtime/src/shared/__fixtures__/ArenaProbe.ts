import { $eq, $GestureDispositionValue, $PointerPhaseValue, GestureRecognizer, PointerEvent } from "@equantic/runtime";

export class ArenaProbe extends GestureRecognizer {
    static $interfaces = ['eQuantic.UI.Primitives.IGestureArenaMember'];

    constructor(name: string, heard: string[], deadlineMs: number = 0, atDeadline: $GestureDispositionValue | null = null, onAdd: $GestureDispositionValue | null = null, onMove: $GestureDispositionValue | null = null, primaryOnly: boolean = false, thenDeadlineMs: number = 0) {
        super();
        this._name = null!;
        this._heard = null!;
        this._deadlineMs = 0;
        this._atDeadline = null;
        this._onAdd = null;
        this._primaryOnly = false;
        this._onMove = null;
        this._thenDeadlineMs = 0;
        this.onDeadline = null;
        this._name = name;
        this._heard = heard;
        this._deadlineMs = deadlineMs;
        this._atDeadline = atDeadline;
        this._onAdd = onAdd;
        this._onMove = onMove;
        this._primaryOnly = primaryOnly;
        this._thenDeadlineMs = thenDeadlineMs;
    }

    _name!: string;
    _heard!: string[];
    _deadlineMs!: number;
    _atDeadline!: $GestureDispositionValue | null;
    _onAdd!: $GestureDispositionValue | null;
    _primaryOnly!: boolean;
    _onMove!: $GestureDispositionValue | null;
    _thenDeadlineMs!: number;
    onDeadline!: ((string: string) => void) | null;

    isPointerAllowed(down: PointerEvent) {
        return !this._primaryOnly || (down.buttons & 1) !== 0;
    }

    addAllowedPointer(down: PointerEvent) {
        this.write(`add ${$eq.text.format(down.pointer, null, undefined, undefined, 'int32')} ${$eq.text.format(down.position.x, null, undefined, undefined, 'single')},${$eq.text.format(down.position.y, null, undefined, undefined, 'single')}`);
        if (this._deadlineMs > 0) this.startDeadline(this._deadlineMs);
        let disposition: any; 
        if ((disposition = this._onAdd) != null) this.resolve(disposition);
    }

    handleEvent(ev: PointerEvent) {
        this.write(`${ArenaProbe.phaseOf(ev.phase)} ${$eq.text.format(ev.pointer, null, undefined, undefined, 'int32')} ${$eq.text.format(ev.position.x, null, undefined, undefined, 'single')},${$eq.text.format(ev.position.y, null, undefined, undefined, 'single')}`);
        let disposition: any; 
        if (ev.phase === 'move' && (disposition = this._onMove) != null) {
            this._onMove = null;
            this.resolvePointer(ev.pointer, disposition);
        }
    }

    acceptGesture(pointer: number) {
        return this.write(`accept ${$eq.text.format(pointer, null, undefined, undefined, 'int32')}`);
    }

    rejectGesture(pointer: number) {
        return this.write(`reject ${$eq.text.format(pointer, null, undefined, undefined, 'int32')}`);
    }

    didExceedDeadline(pointer: number) {
        this.write(`deadline ${$eq.text.format(pointer, null, undefined, undefined, 'int32')} tracking ${$eq.text.format($eq.collections.count(this.trackedPointers), null, undefined, undefined, 'int32')}`);
        this.onDeadline?.(this._name);
        if (this._thenDeadlineMs > 0) {
            let then = this._thenDeadlineMs;
            this._thenDeadlineMs = 0;
            this.startDeadline(then);
        }
        let disposition: any; 
        if ((disposition = this._atDeadline) != null) this.resolve(disposition);
    }

    say(disposition: $GestureDispositionValue) {
        return this.resolve(disposition);
    }

    stop(pointer: number) {
        return this.stopTrackingPointer(pointer);
    }

    write(what: string) {
        return $eq.collections.listAdd(this._heard, `${this._name}.${what}`);
    }

    static phaseOf(phase: $PointerPhaseValue) {
        return (() => { const $s = phase; if ($s === 'down') return 'down'; if ($s === 'move') return 'move'; if ($s === 'up') return 'up'; if ($s === 'cancel') return 'cancel'; return 'other'; })();
    }

    toString(): string {
        return 'eQuantic.UI.Web.Tests.Fixtures.ArenaProbe';
    }
}

