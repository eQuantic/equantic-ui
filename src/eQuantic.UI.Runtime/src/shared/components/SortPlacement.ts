import { $eq } from "../runtime-exports";

export class SortPlacement {
    static tiltPerSpeed: number = 8;
    static tiltMax: number = 12;
    static tiltRestMs: number = 80;

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

    static step(index: number, step: number, count: number) {
        return Math.max(0, Math.min(index + step, count - 1));
    }
}

