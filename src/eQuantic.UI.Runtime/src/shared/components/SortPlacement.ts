import { $eq, SortableTheme } from "../runtime-exports";

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
    static copyElevation: number = 4;

    static look(theme: SortableTheme | null, own: SortableTheme | null) {
        let look = SortableTheme.merge(theme, own);
        return new SortableTheme({ liftedOpacity: SortPlacement.opacity(look?.liftedOpacity ?? Math.fround(SortPlacement.liftedOpacity)), copyOpacity: SortPlacement.opacity(look?.copyOpacity ?? Math.fround(SortPlacement.copyOpacity)), copyElevation: look?.copyElevation ?? SortPlacement.copyElevation, maxLean: look?.maxLean ?? SortPlacement.tiltMax });
    }

    static opacity(value: number) {
        return Math.min(1, Math.max(0, value));
    }

    static place(starts: number[], extents: number[], at: number, nests: boolean): [number, number] {
        for (let i = 0; i < $eq.collections.count(starts); i++) {
            let start = $eq.collections.item(starts, i);
            let size = $eq.collections.item(extents, i);
            if (nests && at >= Math.fround(start + Math.fround(size / 4)) && at <= Math.fround(start + Math.fround(Math.fround(size * 3) / 4))) return [i, i];
            if (at < Math.fround(start + Math.fround(size / 2))) return [i, -1];
        }
        return [$eq.collections.count(starts), -1];
    }

    static tilt(across: number, milliseconds: number, most: number) {
        if (milliseconds <= 0) return null;
        if (most <= 0) return 0;
        let angle = Math.fround(-Math.fround(across / milliseconds) * SortPlacement.tiltPerSpeed);
        if (angle > most) return most;
        if (angle < -most) return -most;
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

    static keyStep(index: number, into: number, step: number, others: number, nests: boolean): [number, number] {
        if (!nests) return [SortPlacement.step(index, step, others + 1), -1];
        let place = into >= 0 ? into * 2 + 1 : index * 2;
        place = Math.max(0, Math.min(place + step, others * 2));
        return place % 2 === 1 ? [Math.trunc((place - 1) / 2), Math.trunc((place - 1) / 2)] : [Math.trunc(place / 2), -1];
    }
}

