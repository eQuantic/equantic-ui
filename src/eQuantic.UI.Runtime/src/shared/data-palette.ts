/**
 * Client twins of the data palette a chart draws with — `eQuantic.UI.Primitives.DataPalette` and
 * its two scales. A chart never picks a colour; it asks the theme (`theme.data`) and every colour
 * it is handed does one of four jobs: `series` encode IDENTITY (eight hues in a FIXED order, never
 * cycled), `sequential` encodes MAGNITUDE, `diverging` encodes POLARITY and `status` encodes STATE;
 * `other` is the one gray for what the story is not about.
 *
 * The VALUES never live here: `design-system.generated.ts` emits them from the C# single source and
 * assigns `DataPalette.default`, and the SSR bridge (`theme-bridge.ts`) rehydrates a custom theme's
 * palette from the JSON the server serialized. The audit a palette is held to (`PaletteAudit`) has no
 * twin on purpose — the browser receives a palette already validated.
 */
import type { ColorToken } from './value-types';

/** C# `DivergingScale`: two poles that read as opposite, and a midpoint that reads as nothing. */
export class DivergingScale {
  constructor(
    readonly negative: ColorToken,
    readonly midpoint: ColorToken,
    readonly positive: ColorToken,
  ) {}
}

/** C# `StatusScale`: the four reserved state steps, never themed into a series slot. */
export class StatusScale {
  constructor(
    readonly good: ColorToken,
    readonly warning: ColorToken,
    readonly serious: ColorToken,
    readonly critical: ColorToken,
  ) {}
}

/** C# `DataPalette`. */
export class DataPalette {
  /** The categorical ceiling — a ninth hue is indistinguishable from one of the eight for somebody. */
  static readonly seriesCeiling = 8;

  /** The validated reference instance — assigned by `design-system.generated.ts`, the module that
   * knows the values, so that no hex is ever written twice. */
  static default: DataPalette;

  constructor(
    readonly series: readonly ColorToken[],
    readonly sequential: readonly ColorToken[],
    readonly diverging: DivergingScale,
    readonly other: ColorToken,
    readonly status: StatusScale,
  ) {
    if (series.length !== DataPalette.seriesCeiling) {
      throw new RangeError(
        `A data palette carries exactly ${DataPalette.seriesCeiling} series slots in a fixed order (it has ${series.length}). ` +
          'The order is the colour-vision-safety mechanism; fewer slots is a palette that cannot serve a chart with that many series, more is a ninth hue nobody can tell apart.',
      );
    }
    if (sequential.length < 2) throw new RangeError('A sequential ramp needs at least two steps.');
  }

  /** The colour of the series at `index` (0-based), or an exception by name past the ceiling. */
  seriesColor(index: number): ColorToken {
    if (index < 0 || index >= this.series.length) {
      throw new RangeError(
        `A chart carries at most ${DataPalette.seriesCeiling} series colours. Fold the tail into Other, facet into small multiples, ` +
          'or encode a second channel (shape) — a ninth hue is indistinguishable from an existing one under colour-vision deficiency.',
      );
    }
    return this.series[index];
  }
}
