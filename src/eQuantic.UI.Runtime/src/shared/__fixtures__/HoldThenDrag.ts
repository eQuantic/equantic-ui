import { GestureRecognizer, Point, PointerEvent } from "@equantic/runtime";

export class HoldThenDrag extends GestureRecognizer {
    static $interfaces = ['eQuantic.UI.Primitives.IGestureArenaMember'];

    constructor() {
        super();
        this._pointer = null;
        this._start = new Point();
        this._heldLongEnough = false;
        this._won = false;
        this._dragging = false;
        this.onHeld = null;
        this.onMoved = null;
        this.onReleased = null;
        this.onCancelled = null;
    }

    _pointer!: number | null;
    _start!: Point;
    _heldLongEnough!: boolean;
    _won!: boolean;
    _dragging!: boolean;
    onHeld!: (() => void) | null;
    onMoved!: ((point: Point) => void) | null;
    onReleased!: ((point: Point) => void) | null;
    onCancelled!: (() => void) | null;

    isPointerAllowed(down: PointerEvent) {
        return this._pointer == null && (down.buttons & 1) !== 0;
    }

    addAllowedPointer(down: PointerEvent) {
        this._pointer = down.pointer;
        this._start = down.position;
        this._heldLongEnough = false;
        this._won = false;
        this._dragging = false;
        this.startDeadline(500);
    }

    handleEvent(ev: PointerEvent) {
        if (ev.pointer !== this._pointer) return;
        let offset = new Point(Math.fround(ev.position.x - this._start.x), Math.fround(ev.position.y - this._start.y));
        {
            const $s = ev.phase;
            if (($s === 'move' && (this._dragging))) {
                this.onMoved?.(offset);
            } else if (($s === 'move' && (!this._heldLongEnough && offset.length() > 12))) {
                this.resolve('rejected');
                this.stopTrackingPointer(ev.pointer);
                this.finish();
            } else if ($s === 'up') {
                if (this._dragging) this.onReleased?.(offset);
                this.finish();
            } else if ($s === 'cancel') {
                if (this._dragging) this.onCancelled?.();
                this.finish();
            }
        }
    }

    didExceedDeadline(pointer: number) {
        if (pointer !== this._pointer) return;
        this._heldLongEnough = true;
        if (this._won) this.lift(); else this.resolve('accepted');
    }

    acceptGesture(pointer: number) {
        if (pointer !== this._pointer) return;
        this._won = true;
        if (this._heldLongEnough) this.lift();
    }

    rejectGesture(pointer: number) {
        if (pointer !== this._pointer) return;
        if (this._dragging) this.onCancelled?.();
        this.finish();
    }

    lift() {
        if (this._dragging) return;
        this._dragging = true;
        this.onHeld?.();
    }

    finish() {
        this._pointer = null;
        this._dragging = false;
    }

    toString(): string {
        return 'eQuantic.Console.HoldThenDrag';
    }
}

