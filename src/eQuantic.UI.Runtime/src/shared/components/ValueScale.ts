import { ValueTicks } from "../runtime-exports";

export class ValueScale {
    static nice(low: number, high: number, target: number) {
        if (target < 2) target = 2;
        if (high < low) {
            let swap = low;
            low = high;
            high = swap;
        }
        if (high === low) high = low + 1;
        let rough = (high - low) / (target - 1);
        let magnitude = 1.0;
        while (rough >= magnitude * 10) magnitude *= 10;
        while (rough < magnitude) magnitude /= 10;
        let ratio = rough / magnitude;
        let step = (ratio < 1.5 ? 1 : ratio < 3 ? 2 : ratio < 7 ? 5 : 10) * magnitude;
        let min = Math.floor(low / step) * step;
        let max = Math.ceil(high / step) * step;
        if (max <= min) max = min + step;
        return new ValueTicks(min, max, step);
    }

    static fixed(min: number, max: number, ticks: number) {
        if (ticks < 2) ticks = 2;
        if (max <= min) max = min + 1;
        return new ValueTicks(min, max, (max - min) / (ticks - 1));
    }
}

