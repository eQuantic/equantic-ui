import { Point, PointerKindValue, PointerPhaseValue, Rect, ScrollAxisValue, Size, TableOffset, TableScrollRequest, TableSpans } from "../runtime-exports";

export class TableScroll {
    constructor() {
        this._across = 0;
        this._down = 0;
        this._targetAcross = null;
        this._targetDown = null;
        this._lastTime = NaN;
        this._viewport = new Size();
        this._gridAcross = 0;
        this._gridDown = 0;
        this._pinnedAcross = 0;
        this._pinnedDown = 0;
        this._reduceMotion = false;
        this._barDown = false;
        this._barAcross = false;
        this._held = null;
        this._honoured = null;
        this._dragging = null;
        this._grab = 0;
        this._panning = false;
        this._panAt = new Point();
        this._panTime = 0;
        this._velocityAcross = 0;
        this._velocityDown = 0;
    }

    _across!: number;
    _down!: number;
    _targetAcross!: number | null;
    _targetDown!: number | null;
    _lastTime!: number;
    _viewport!: Size;
    _gridAcross!: number;
    _gridDown!: number;
    _pinnedAcross!: number;
    _pinnedDown!: number;
    _reduceMotion!: boolean;
    _barDown!: boolean;
    _barAcross!: boolean;
    _held!: number | null;
    _honoured!: number | null;
    _dragging!: ScrollAxisValue | null;
    _grab!: number;
    _panning!: boolean;
    _panAt!: Point;
    _panTime!: number;
    _velocityAcross!: number;
    _velocityDown!: number;
    static thickness: number = 12;
    static minThumb: number = 24;
    static smoothingPerMs: number = 0.022;
    static restingDp: number = 0.5;
    static flingReach: number = 90;
    static flingFloor: number = 0.05;
    static flingWindow: number = 80;
    static arrowStep: number = 40;

    get offset(): TableOffset {
        return new TableOffset(this._across, this._down);
    }

    get viewport(): Size {
        return this._viewport;
    }

    get maxAcross(): number {
        return Math.max(0, this._gridAcross - this._viewport.width);
    }

    get maxDown(): number {
        return Math.max(0, this._gridDown - this._viewport.height);
    }

    arrange(window: Size, gridAcross: number, gridDown: number, pinnedAcross: number, pinnedDown: number, reduceMotion: boolean) {
        this._gridAcross = Math.max(0, gridAcross);
        this._gridDown = Math.max(0, gridDown);
        this._pinnedAcross = Math.max(0, pinnedAcross);
        this._pinnedDown = Math.max(0, pinnedDown);
        this._reduceMotion = reduceMotion;
        let down = this._gridDown > window.height;
        let across = this._gridAcross > window.width;
        if (down && !across) across = this._gridAcross > Math.fround(window.width - TableScroll.thickness);
        if (across && !down) down = this._gridDown > Math.fround(window.height - TableScroll.thickness);
        this._barDown = down;
        this._barAcross = across;
        this._viewport = new Size(Math.max(0, Math.fround(window.width - (down ? TableScroll.thickness : 0))), Math.max(0, Math.fround(window.height - (across ? TableScroll.thickness : 0))));
        this._across = Math.min(Math.max(this._across, 0), this.maxAcross);
        this._down = Math.min(Math.max(this._down, 0), this.maxDown);
        let targetAcross: any; 
        if ((targetAcross = this._targetAcross) != null) this._targetAcross = Math.min(Math.max(targetAcross, 0), this.maxAcross);
        let targetDown: any; 
        if ((targetDown = this._targetDown) != null) this._targetDown = Math.min(Math.max(targetDown, 0), this.maxDown);
        if (reduceMotion) this.land();
        return this._viewport;
    }

    wheel(across: number, down: number, stepped: boolean, modifiers: number) {
        this.letGo();
        let turned = (modifiers & 1) !== 0 && across === 0 && down !== 0;
        if (turned) {
            across = down;
            down = 0;
        }
        let glide = stepped && !this._reduceMotion;
        let fromAcross = glide ? this._targetAcross ?? this._across : this._across;
        let fromDown = glide ? this._targetDown ?? this._down : this._down;
        let toAcross = Math.min(Math.max(fromAcross + across, 0), this.maxAcross);
        let toDown = Math.min(Math.max(fromDown + down, 0), this.maxDown);
        if (glide) {
            if (toAcross !== fromAcross) this.glideAcross(toAcross);
            if (toDown !== fromDown) this.glideDown(toDown);
        } else {
            this._targetAcross = null;
            this._targetDown = null;
            this._across = toAcross;
            this._down = toDown;
        }
        let leftAcross = across - (toAcross - fromAcross);
        let leftDown = down - (toDown - fromDown);
        return turned ? new TableOffset(0, leftAcross) : new TableOffset(leftAcross, leftDown);
    }

    pointer(phase: PointerPhaseValue, position: Point, kind: PointerKindValue, time: number) {
        switch (phase) {
            case 'down':
                return this.press(position, kind, time);
            case 'move':
                return this.drag(position, time);
            case 'up':
                return this.release(time, true);
            case 'cancel':
                return this.release(time, false);
            default:
                return false;
        }
    }

    advance(time: number) {
        let elapsed = Number.isNaN(this._lastTime) ? 0 : Math.max(0, time - this._lastTime);
        this._lastTime = time;
        if (this._targetAcross == null && this._targetDown == null) {
            this._lastTime = NaN;
            return false;
        }
        if (this._reduceMotion) {
            this.land();
            return false;
        }
        let step = 1 - Math.pow(1 - TableScroll.smoothingPerMs, elapsed);
        let targetAcross: any; 
        if ((targetAcross = this._targetAcross) != null) {
            this._across += (targetAcross - this._across) * step;
            if (Math.abs(targetAcross - this._across) <= TableScroll.restingDp) {
                this._across = targetAcross;
                this._targetAcross = null;
            }
        }
        let targetDown: any; 
        if ((targetDown = this._targetDown) != null) {
            this._down += (targetDown - this._down) * step;
            if (Math.abs(targetDown - this._down) <= TableScroll.restingDp) {
                this._down = targetDown;
                this._targetDown = null;
            }
        }
        let moving = !(this._targetAcross == null) || !(this._targetDown == null);
        if (!moving) this._lastTime = NaN;
        return moving;
    }

    trackOf(axis: ScrollAxisValue) {
        if (axis === 'horizontal') return this._barAcross ? new Rect(0, this._viewport.height, this._viewport.width, TableScroll.thickness) : null;
        return this._barDown ? new Rect(this._viewport.width, 0, TableScroll.thickness, this._viewport.height) : null;
    }

    thumbOf(axis: ScrollAxisValue) {
        let track: any; 
        if (!((track = this.trackOf(axis)) != null)) return null;
        let horizontal = axis === 'horizontal';
        let trackLength = horizontal ? track.width : track.height;
        let shown = Math.max(0, (horizontal ? this._viewport.width : this._viewport.height) - (horizontal ? this._pinnedAcross : this._pinnedDown));
        let content = Math.max(shown, horizontal ? this._gridAcross - this._pinnedAcross : this._gridDown - this._pinnedDown);
        let length = content <= 0 ? trackLength : trackLength * shown / content;
        length = Math.min(trackLength, Math.max(TableScroll.minThumb, length));
        let max = horizontal ? this.maxAcross : this.maxDown;
        let travel = max <= 0 ? 0 : (trackLength - length) * ((horizontal ? this._across : this._down) / max);
        return horizontal ? new Rect(Math.fround(track.x + Math.fround(travel)), track.y, Math.fround(length), track.height) : new Rect(track.x, Math.fround(track.y + Math.fround(travel)), track.width, Math.fround(length));
    }

    apply(request: TableScrollRequest | null, rows: TableSpans, columns: TableSpans, pinnedRows: number, pinnedColumns: number) {
        if (request == null || request.version === this._honoured) return false;
        this._held = request.version;
        let across = this._across;
        let down = this._down;
        let range: any; 
        if ((range = request.reveal) != null) {
            down = TableScroll.reveal(down, rows, range.row, range.rows, pinnedRows, this._pinnedDown, this._viewport.height);
            across = TableScroll.reveal(across, columns, range.column, range.columns, pinnedColumns, this._pinnedAcross, this._viewport.width);
        } else { let offset: any; if ((offset = request.offset) != null) {
            across = offset.across;
            down = offset.down;
        } }
        across = Math.min(Math.max(across, 0), this.maxAcross);
        down = Math.min(Math.max(down, 0), this.maxDown);
        this._targetAcross = null;
        this._targetDown = null;
        if (across === this._across && down === this._down) return false;
        this._across = across;
        this._down = down;
        return true;
    }

    reveal(area: Rect, across: boolean, down: boolean) {
        this.letGo();
        let toAcross = across ? Math.min(Math.max(this._across + TableScroll.into(area.x, Math.fround(area.x + area.width), this._pinnedAcross, this._viewport.width), 0), this.maxAcross) : this._across;
        let toDown = down ? Math.min(Math.max(this._down + TableScroll.into(area.y, Math.fround(area.y + area.height), this._pinnedDown, this._viewport.height), 0), this.maxDown) : this._down;
        if (toAcross === this._across && toDown === this._down) return false;
        this._targetAcross = null;
        this._targetDown = null;
        this._across = toAcross;
        this._down = toDown;
        return true;
    }

    key(key: string, modifiers: number) {
        let both = (modifiers & (8 | 4)) !== 0;
        let pageDown = Math.max(TableScroll.arrowStep, this._viewport.height - this._pinnedDown);
        let [across, down, stepped] = (() => { const $s = key; if ($s === 'ArrowDown') return [0.0, TableScroll.arrowStep, true]; if ($s === 'ArrowUp') return [0.0, -TableScroll.arrowStep, true]; if ($s === 'ArrowRight') return [TableScroll.arrowStep, 0.0, true]; if ($s === 'ArrowLeft') return [-TableScroll.arrowStep, 0.0, true]; if ($s === 'PageDown') return [0.0, pageDown, true]; if ($s === 'PageUp') return [0.0, -pageDown, true]; if ($s === 'Home') return [both ? -(this._targetAcross ?? this._across) : 0.0, -(this._targetDown ?? this._down), false]; if ($s === 'End') return [both ? this.maxAcross - (this._targetAcross ?? this._across) : 0.0, this.maxDown - (this._targetDown ?? this._down), false]; return [0.0, 0.0, false]; })();
        if (across === 0 && down === 0) return false;
        let left = this.wheel(across, down, stepped, 0);
        return left.across !== across || left.down !== down;
    }

    static into(start: number, end: number, from: number, to: number) {
        if (end - start > to - from || start < from) return start - from;
        if (end > to) return end - to;
        return 0;
    }

    static reveal(offset: number, spans: TableSpans, first: number, count: number, pinned: number, pinnedExtent: number, window: number) {
        if (spans.count === 0 || first < pinned) return offset;
        let start = spans.offsetOf(Math.min(Math.max(first, 0), spans.count));
        let end = spans.offsetOf(Math.min(Math.max(first + Math.max(1, count), 0), spans.count));
        if (end - start > window - pinnedExtent || start < offset + pinnedExtent) return start - pinnedExtent;
        if (end > offset + window) return end - window;
        return offset;
    }

    letGo() {
        let version: any; 
        if (!((version = this._held) != null)) return;
        this._honoured = version;
        this._held = null;
    }

    press(at: Point, kind: PointerKindValue, time: number) {
        this._dragging = null;
        this._panning = false;
        for (const axis of (['vertical', 'horizontal'] as ScrollAxisValue[])) {
            let track: any; 
            if (!((track = this.trackOf(axis)) != null) || !TableScroll.contains(track, at)) continue;
            this.letGo();
            let thumb: any; 
            if ((thumb = this.thumbOf(axis)) != null && TableScroll.contains(thumb, at)) {
                this._dragging = axis;
                this._grab = axis === 'vertical' ? Math.fround(at.y - thumb.y) : Math.fround(at.x - thumb.x);
                return true;
            }
            let knob: any; 
            let thumbStart = (knob = this.thumbOf(axis)) != null ? axis === 'vertical' ? knob.y : knob.x : 0;
            let back = (axis === 'vertical' ? at.y : at.x) < thumbStart;
            let page = axis === 'vertical' ? this._viewport.height - this._pinnedDown : this._viewport.width - this._pinnedAcross;
            let delta = back ? -page : page;
            this.wheel(axis === 'horizontal' ? delta : 0, axis === 'vertical' ? delta : 0, true, 0);
            return true;
        }
        if (kind === 'mouse') return false;
        this.letGo();
        this._panning = true;
        this._panAt = at;
        this._panTime = time;
        this._velocityAcross = 0;
        this._velocityDown = 0;
        this._targetAcross = null;
        this._targetDown = null;
        return true;
    }

    drag(at: Point, time: number) {
        let axis: any; 
        if ((axis = this._dragging) != null) {
            let track: any; let thumb: any; 
            if (!((track = this.trackOf(axis)) != null) || !((thumb = this.thumbOf(axis)) != null)) return false;
            let vertical = axis === 'vertical';
            let room = vertical ? Math.fround(track.height - thumb.height) : Math.fround(track.width - thumb.width);
            let travel = (vertical ? Math.fround(at.y - track.y) : Math.fround(at.x - track.x)) - this._grab;
            let fraction = room <= 0 ? 0 : Math.min(Math.max(travel / room, 0), 1);
            if (vertical) {
                this._down = fraction * this.maxDown;
                this._targetDown = null;
            } else {
                this._across = fraction * this.maxAcross;
                this._targetAcross = null;
            }
            return true;
        }
        if (!this._panning) return false;
        let elapsed = time - this._panTime;
        let beforeAcross = this._across;
        let beforeDown = this._down;
        this._across = Math.min(Math.max(this._across - Math.fround(at.x - this._panAt.x), 0), this.maxAcross);
        this._down = Math.min(Math.max(this._down - Math.fround(at.y - this._panAt.y), 0), this.maxDown);
        if (elapsed > 0) {
            this._velocityAcross = 0.8 * ((this._across - beforeAcross) / elapsed) + 0.2 * this._velocityAcross;
            this._velocityDown = 0.8 * ((this._down - beforeDown) / elapsed) + 0.2 * this._velocityDown;
            this._panTime = time;
        }
        this._panAt = at;
        return true;
    }

    release(time: number, fling: boolean) {
        if (!(this._dragging == null)) {
            this._dragging = null;
            return true;
        }
        if (!this._panning) return false;
        this._panning = false;
        if (!fling || this._reduceMotion || time - this._panTime > TableScroll.flingWindow) return true;
        if (Math.abs(this._velocityAcross) >= TableScroll.flingFloor) this.glideAcross(Math.min(Math.max(this._across + this._velocityAcross * TableScroll.flingReach, 0), this.maxAcross));
        if (Math.abs(this._velocityDown) >= TableScroll.flingFloor) this.glideDown(Math.min(Math.max(this._down + this._velocityDown * TableScroll.flingReach, 0), this.maxDown));
        return true;
    }

    glideAcross(target: number) {
        if (this._targetAcross == null && this._targetDown == null) this._lastTime = NaN;
        this._targetAcross = target;
    }

    glideDown(target: number) {
        if (this._targetAcross == null && this._targetDown == null) this._lastTime = NaN;
        this._targetDown = target;
    }

    land() {
        let across: any; 
        if ((across = this._targetAcross) != null) this._across = across;
        let down: any; 
        if ((down = this._targetDown) != null) this._down = down;
        this._targetAcross = null;
        this._targetDown = null;
        this._lastTime = NaN;
    }

    static contains(box: Rect, at: Point) {
        return at.x >= box.x && at.x < Math.fround(box.x + box.width) && at.y >= box.y && at.y < Math.fround(box.y + box.height);
    }

    toString(): string {
        return 'eQuantic.UI.Components.TableScroll';
    }
}

