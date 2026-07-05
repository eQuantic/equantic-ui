import { describe, it, expect } from 'vitest';
import { materializeTheme, type ThemeData } from './theme-bridge';
import { photonTheme } from './design-system.generated';
import type { AppTheme } from './value-types';
// The SAME fixture the C# ThemeBridgeTests pins (ThemeBridge.SerializeJson(PhotonTheme.Instance)).
// Round-tripping it back to the generated photonTheme proves the C# serializer and the TS materializer
// agree byte-for-byte — the guarantee that a client hydrates the exact theme the server rendered.
import photonData from './theme-bridge.photon.json';

describe('theme bridge: materializeTheme ∘ ThemeBridge.SerializeJson === photonTheme', () => {
  const data = photonData as unknown as ThemeData;
  const theme = materializeTheme(data);
  const asRecord = photonTheme as unknown as Record<string, unknown>;

  it('round-trips every surface/chrome token', () => {
    for (const key of Object.keys(data.surfaces)) {
      expect((theme as unknown as Record<string, unknown>)[key]).toEqual(asRecord[key]);
    }
    expect(theme.disabledOpacity).toBe(photonTheme.disabledOpacity);
  });

  it('round-trips every variant color group', () => {
    for (const variant of Object.keys(data.variants)) {
      expect(theme.colors(variant)).toEqual(photonTheme.colors(variant));
    }
  });

  it('round-trips the type scale', () => {
    for (const role of Object.keys(data.type)) {
      expect(theme.type(role)).toEqual(photonTheme.type(role));
    }
  });

  it('round-trips elevations 0..5', () => {
    for (let level = 0; level <= 5; level++) {
      expect(theme.elevation(level)).toEqual(photonTheme.elevation(level));
    }
  });

  it('round-trips the shape scale', () => {
    for (const scale of Object.keys(data.shape)) {
      expect(theme.shape(scale)).toBe(photonTheme.shape(scale));
    }
  });

  it('preserves the generator fallbacks (unknown variant → primary, unknown role → bodyL)', () => {
    expect(theme.colors('nope')).toEqual(theme.colors('primary'));
    expect(theme.type('nope')).toEqual(theme.type('bodyL'));
    expect(theme.shape('nope')).toBe(theme.shape('medium'));
  });
});

describe('theme bridge: a distinct theme materializes to distinct values', () => {
  it('a Material-like blob yields the M3 primary, not Photon blue', () => {
    // A hand-built blob standing in for MaterialTheme.Instance's primary (#6750A4). Only the shape of
    // the wire matters here; the C#↔TS value agreement is covered by the fixture round-trip above.
    const purple: [number, number, number, number] = [103, 80, 164, 255];
    const white: [number, number, number, number] = [255, 255, 255, 255];
    const material = materializeTheme({
      ...(photonData as unknown as ThemeData),
      variants: {
        ...(photonData as unknown as ThemeData).variants,
        primary: [[purple, purple], [white, white], [purple, purple], [purple, purple], [white, white]],
      },
    });

    expect(material.colors('primary').base.light).toEqual({ r: 103, g: 80, b: 164, a: 255 });
    expect(material.colors('primary').base.light).not.toEqual(photonTheme.colors('primary').base.light);
  });

  it('materializeTheme returns a structurally valid AppTheme', () => {
    const t: AppTheme = materializeTheme(photonData as unknown as ThemeData);
    expect(typeof t.colors).toBe('function');
    expect(typeof t.type).toBe('function');
    expect(typeof t.elevation).toBe('function');
    expect(typeof t.shape).toBe('function');
    expect(t.surface.light).toBeDefined();
  });
});
