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

    static wanted(depth: number, travelled: number, indent: number) {
        return indent <= 0 ? depth : depth + $eq.num.saturate(Math.floor(Math.fround(Math.fround(travelled / indent) + 0.5)));
    }

    static depths(wants: number, above: number | null, below: number | null) {
        let [shallowest, deepest] = SortPlacement.depthRange(above, below);
        let first = Math.max(shallowest, Math.min(wants, deepest));
        let depths = [first];
        for (let depth = first - 1; depth >= shallowest; depth--) $eq.collections.listAdd(depths, depth);
        for (let depth = first + 1; depth <= deepest; depth++) $eq.collections.listAdd(depths, depth);
        return depths;
    }

    static keyDepth(depth: number, step: number, above: number | null, below: number | null) {
        let [shallowest, deepest] = SortPlacement.depthRange(above, below);
        let next = depth + step;
        return next < shallowest || next > deepest ? depth : next;
    }

    static depthRange(above: number | null, below: number | null): [number, number] {
        let shallowest = below ?? 0;
        let before: any; 
        return [shallowest, (before = above) != null ? Math.max(before + 1, shallowest) : shallowest];
    }

    static siblings(depths: number[], index: number, depth: number): [number, number, number] {
        let holder = -1;
        let position = 1;
        for (let i = index - 1; i >= 0; i--) {
            if ($eq.collections.item(depths, i) < depth) {
                if ($eq.collections.item(depths, i) === depth - 1) holder = i;
                break;
            }
            if ($eq.collections.item(depths, i) === depth) position++;
        }
        let count = position;
        for (let i = index; i < $eq.collections.count(depths) && $eq.collections.item(depths, i) >= depth; i++) {
            if ($eq.collections.item(depths, i) === depth) count++;
        }
        return [holder, position, count];
    }
}

