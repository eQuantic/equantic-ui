import { describe, expect, it } from 'vitest';
import { DataPalette } from './data-palette';
import { photonTheme } from './design-system.generated';
import { materializeTheme } from './theme-bridge';
import type { ThemeData } from './theme-bridge';
import fixture from './theme-bridge.photon.json';

/**
 * The data palette reaches the client twin for twin: the generated design system carries the
 * reference instance, the SSR bridge rehydrates a serialized one to the same values, and the two
 * refusals the C# constructor and `SeriesColor` make are made here too — a chart that asks for a
 * ninth colour fails by name on every target.
 */
describe('DataPalette', () => {
  it('reaches the client with eight fixed series slots, and is the default', () => {
    expect(photonTheme.data.series).toHaveLength(DataPalette.seriesCeiling);
    expect(DataPalette.default).toBe(photonTheme.data);
    // The four status steps are fixed and never themed: the same hex in both modes, by design.
    const status = photonTheme.data.status;
    for (const step of [status.good, status.warning, status.serious, status.critical]) {
      expect(step.dark).toEqual(step.light);
    }
  });

  it('crosses the SSR bridge to the same values', () => {
    const bridged = materializeTheme(fixture as unknown as ThemeData).data;

    expect(bridged.series).toEqual(photonTheme.data.series);
    expect(bridged.sequential).toEqual(photonTheme.data.sequential);
    expect(bridged.diverging).toEqual(photonTheme.data.diverging);
    expect(bridged.other).toEqual(photonTheme.data.other);
    expect(bridged.status).toEqual(photonTheme.data.status);
  });

  it('refuses a ninth series by name, like the C# twin', () => {
    expect(photonTheme.data.seriesColor(7)).toBe(photonTheme.data.series[7]);
    expect(() => photonTheme.data.seriesColor(8)).toThrow(/fold the tail into Other/i);
    expect(() => photonTheme.data.seriesColor(-1)).toThrow(RangeError);
  });

  it('refuses a palette with the wrong number of slots at construction', () => {
    const d = photonTheme.data;

    expect(
      () => new DataPalette(d.series.slice(0, 7), d.sequential, d.diverging, d.other, d.status),
    ).toThrow(/exactly 8 series slots/);
  });
});
