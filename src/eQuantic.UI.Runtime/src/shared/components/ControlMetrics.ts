import { BuildContext, ControlsTheme, DensityLadder, DensityValue, SizeLadder, SizeVariantValue, Sizing } from "../runtime-exports";

export class ControlMetrics {
    static height(context: BuildContext, size: SizeVariantValue, family: ControlsTheme | null = null) {
        return ControlMetrics.rungAt((family?.height ?? null), size, context.density) ?? (ControlMetrics.rungAt((context.theme.controls?.height ?? null), size, context.density) ?? Sizing.height(size, context.density));
    }

    static paddingX(context: BuildContext, size: SizeVariantValue, family: ControlsTheme | null = null) {
        return ControlMetrics.rungAt((family?.paddingX ?? null), size, context.density) ?? (ControlMetrics.rungAt((context.theme.controls?.paddingX ?? null), size, context.density) ?? Sizing.paddingX(size, context.density));
    }

    static labelSize(context: BuildContext, size: SizeVariantValue, family: ControlsTheme | null = null) {
        return ControlMetrics.rungAt((family?.labelSize ?? null), size, context.density) ?? (ControlMetrics.rungAt((context.theme.controls?.labelSize ?? null), size, context.density) ?? Sizing.labelSize(size, context.density));
    }

    static gap(context: BuildContext, size: SizeVariantValue, family: ControlsTheme | null = null) {
        return ControlMetrics.rung((family?.gap ?? null), size) ?? (ControlMetrics.rung((context.theme.controls?.gap ?? null), size) ?? Sizing.gap(size));
    }

    static icon(context: BuildContext, size: SizeVariantValue, family: ControlsTheme | null = null) {
        return ControlMetrics.rung((family?.icon ?? null), size) ?? (ControlMetrics.rung((context.theme.controls?.icon ?? null), size) ?? Sizing.icon(size));
    }

    static radius(context: BuildContext, size: SizeVariantValue, family: ControlsTheme | null = null, byDefault: number | null = null) {
        return ControlMetrics.rung((family?.radius ?? null), size) ?? (ControlMetrics.rung((context.theme.controls?.radius ?? null), size) ?? (byDefault ?? Sizing.radius(size)));
    }

    static rung(ladder: SizeLadder | null, size: SizeVariantValue) {
        return (() => { const $s = size; if ($s === 'small') return (ladder?.small ?? null); if ($s === 'medium') return (ladder?.medium ?? null); if ($s === 'large') return (ladder?.large ?? null); return (ladder?.xLarge ?? null); })();
    }

    static rungAt(ladder: DensityLadder | null, size: SizeVariantValue, density: DensityValue) {
        return ControlMetrics.rung(density === 'compact' ? (ladder?.compact ?? null) : (ladder?.comfortable ?? null), size);
    }

    static atSize(ladder: SizeLadder | null, size: SizeVariantValue) {
        return ControlMetrics.rung(ladder, size);
    }
}

