import { $eq } from "../runtime-exports";

export class SortPlacement {
    static tiltPerSpeed: number = 8;
    static tiltMax: number = 12;
    static tiltRestMs: number = 80;
    static pickUpTravel: number = 5;
    static holdMs: number = 250;
    static edgeZone: number = 32;
    static edgeStep: number = 14;
    static liftedOpacity: number = 0.4;
    static copyOpacity: number = 0.92;
    static copyShadowOffsetY: number = 12;
    static copyShadowBlur: number = 28;
    static copyShadowAlpha: number = 0.28;

    static place(starts: number[], extents: number[], at: number, nests: boolean): [number, number] {
        for (let i = 0; i < $eq.collections.count(starts); i++) {
            let start = $eq.collections.item(starts, i);
            let size = $eq.collections.item(extents, i);
            if (nests && at >= Math.fround(start + Math.fround(size / 4)) && at <= Math.fround(start + Math.fround(Math.fround(size * 3) / 4))) return [i, i];
            if (at < Math.fround(start + Math.fround(size / 2))) return [i, -1];
        }
        return [$eq.collections.count(starts), -1];
    }

    static tilt(across: number, milliseconds: number) {
        if (milliseconds <= 0) return null;
        let angle = Math.fround(-Math.fround(across / milliseconds) * SortPlacement.tiltPerSpeed);
        if (angle > SortPlacement.tiltMax) return SortPlacement.tiltMax;
        if (angle < -SortPlacement.tiltMax) return -SortPlacement.tiltMax;
        return angle === 0 ? 0 : angle;
    }

    static edgeScroll(at: number, start: number, end: number) {
        if (at < Math.fround(start + SortPlacement.edgeZone)) return -Math.ceil(Math.fround(Math.fround(Math.fround(Math.fround(start + SortPlacement.edgeZone) - at) / SortPlacement.edgeZone) * SortPlacement.edgeStep));
        if (at > Math.fround(end - SortPlacement.edgeZone)) return Math.ceil(Math.fround(Math.fround(Math.fround(at - Math.fround(end - SortPlacement.edgeZone)) / SortPlacement.edgeZone) * SortPlacement.edgeStep));
        return 0;
    }

    static step(index: number, step: number, count: number) {
        return Math.max(0, Math.min(index + step, count - 1));
    }
}

