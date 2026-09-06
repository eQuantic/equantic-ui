import { BarChartGeometry, BarRect, ChartSeries, ValueAxis, ValueScale, ValueTicks } from "@equantic/runtime";

export class BarChartLayout {
    static maxThickness: number = 24;
    static gap: number = 2;
    static dataEndRadius: number = 4;
    static hitSlack: number = 4;

    static tickOffset(ticks: ValueTicks, index: number, across: number) {
        return ticks.span <= 0 ? 0 : Math.fround(((ticks.at(index) - ticks.min) / ticks.span)) * across;
    }

    static ticks(series: ChartSeries[], visible: boolean[], categoryCount: number, layout: string, axis: ValueAxis) {
        if ((axis.min != null) && (axis.max != null)) return ValueScale.fixed(axis.min, axis.max, axis.ticks);
        let low = 0;
        let high = 0;
        for (let c = 0; c < categoryCount; c++) {
            let positive = 0;
            let negative = 0;
            for (let s = 0; s < series.length; s++) {
                if (!visible[s]) continue;
                let v = series[s].at(c);
                if (layout === 'stacked') {
                    if (v >= 0) positive += v; else negative += v;
                } else {
                    if (v > high) high = v;
                    if (v < low) low = v;
                }
            }
            if (layout === 'stacked') {
                if (positive > high) high = positive;
                if (negative < low) low = negative;
            }
        }
        if ((axis.min != null)) low = axis.min;
        if ((axis.max != null)) high = axis.max;
        return ValueScale.nice(low, high, axis.ticks);
    }

    static solve(series: ChartSeries[], visible: boolean[], categoryCount: number, layout: string, orientation: string, axis: ValueAxis, width: number, height: number) {
        let ticks = BarChartLayout.ticks(series, visible, categoryCount, layout, axis);
        let vertical = orientation === 'vertical';
        let along = vertical ? width : height;
        let across = vertical ? height : width;
        let baseValue = ticks.min > 0 ? ticks.min : ticks.max < 0 ? ticks.max : 0;
        let baseline = BarChartLayout.offset(ticks, baseValue, across);
        let shown: number[] = [];
        for (let s = 0; s < series.length; s++) {
            if (visible[s]) shown.push(s);
        }
        let slot = Math.fround(categoryCount === 0 ? along : along / categoryCount);
        let bars: BarRect[] = [];
        for (let c = 0; c < categoryCount; c++) {
            let categoryStart = Math.fround(c * slot);
            if (layout === 'grouped') {
                let n = shown.length;
                if (n === 0) continue;
                let thickness = Math.min(BarChartLayout.maxThickness, (slot - BarChartLayout.gap * (n + 1)) / n);
                if (thickness < 1) thickness = 1;
                let group = Math.fround(n * thickness + (n - 1) * BarChartLayout.gap);
                let start = Math.fround(categoryStart + (slot - group) / 2);
                for (let k = 0; k < n; k++) {
                    let s = shown[k];
                    let v = series[s].at(c);
                    let from = BarChartLayout.offset(ticks, baseValue, across);
                    let to = BarChartLayout.offset(ticks, v, across);
                    bars.push(BarChartLayout.rect(vertical, across, c, s, start + k * (thickness + BarChartLayout.gap), thickness, Math.min(from, to), Math.max(from, to), v < baseValue, true));
                }
            } else {
                let thickness = Math.min(BarChartLayout.maxThickness, slot - 2 * BarChartLayout.gap);
                if (thickness < 1) thickness = 1;
                let position = Math.fround(categoryStart + (slot - thickness) / 2);
                let lastPositive = -1;
                let lastNegative = -1;
                for (const s of shown) {
                    if (series[s].at(c) >= 0) lastPositive = s; else lastNegative = s;
                }
                let positiveTop = baseValue;
                let negativeBottom = baseValue;
                for (const s of shown) {
                    let v = series[s].at(c);
                    let from = null;
                    let to = null;
                    let dataEnd = null;
                    if (v >= 0) {
                        from = positiveTop;
                        to = positiveTop + v;
                        positiveTop = to;
                        dataEnd = s === lastPositive;
                    } else {
                        to = negativeBottom;
                        from = negativeBottom + v;
                        negativeBottom = from;
                        dataEnd = s === lastNegative;
                    }
                    let low = BarChartLayout.offset(ticks, Math.min(from, to), across);
                    let high = BarChartLayout.offset(ticks, Math.max(from, to), across);
                    if (!dataEnd) {
                        if (v >= 0) high = Math.fround(high - BarChartLayout.gap); else low = Math.fround(low + BarChartLayout.gap);
                        if (high < low) high = low;
                    }
                    bars.push(BarChartLayout.rect(vertical, across, c, s, position, thickness, low, high, v < 0, dataEnd));
                }
            }
        }
        return new BarChartGeometry(width, height, orientation, ticks, vertical ? height - baseline : baseline, bars);
    }

    static hitTest(geometry: BarChartGeometry, x: number, y: number) {
        let bars = geometry.bars;
        for (let i = 0; i < bars.length; i++) {
            let b = bars[i];
            if (x >= Math.fround(b.x - BarChartLayout.hitSlack) && x <= Math.fround(b.x + b.width + BarChartLayout.hitSlack) && y >= Math.fround(b.y - BarChartLayout.hitSlack) && y <= Math.fround(b.y + b.height + BarChartLayout.hitSlack)) return i;
        }
        return -1;
    }

    static offset(ticks: ValueTicks, value: number, across: number) {
        return ticks.span <= 0 ? 0 : Math.fround(((value - ticks.min) / ticks.span)) * across;
    }

    static rect(vertical: boolean, across: number, category: number, series: number, position: number, thickness: number, low: number, high: number, negative: boolean, dataEnd: boolean) {
        let length = Math.fround(high - low);
        return vertical ? new BarRect(category, series, position, across - high, thickness, length, negative, dataEnd) : new BarRect(category, series, low, position, length, thickness, negative, dataEnd);
    }
}

