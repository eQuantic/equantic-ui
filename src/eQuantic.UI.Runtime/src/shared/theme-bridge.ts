/**
 * The client half of the SSR→client THEME BRIDGE: rehydrate the JSON the server serialized from the
 * app-selected `IAppTheme` (see C# `ThemeBridge.SerializeJson`) back into an `AppTheme` the runtime
 * swaps in via `setPhotonTheme` at boot. This is what lets an app pick Material — or a seeded theme —
 * for hydration: client re-renders then resolve the SAME colors/shape the server baked into the SSR
 * markup. The wire shape mirrors the generated `photonTheme`; the round-trip is pinned in vitest.
 */

import { ColorToken, TypeStyle, VariantColors, codeTokenColor } from './value-types';
import type { AppTheme, ShadowSpec } from './value-types';
import type { ColorValue } from './nodes';
import { DataPalette, DivergingScale, StatusScale } from './data-palette';

type RgbaTuple = readonly [number, number, number, number];
type TokenTuple = readonly [RgbaTuple, RgbaTuple];

/** The exact JSON `ThemeBridge.SerializeJson` emits into `window.__EQ_THEME__`. */
export interface ThemeData {
  surfaces: Record<string, TokenTuple>;
  disabledOpacity: number;
  variants: Record<string, readonly TokenTuple[]>; // [base, onBase, pressed, subtle, onSubtle]
  type: Record<string, readonly [number, number, string, number, number]>;
  elevations: ReadonlyArray<readonly [number, number, number, TokenTuple]>;
  shape: Record<string, number>;
  /** The data palette: series and sequential as token lists, diverging as [negative, midpoint,
   * positive], status as [good, warning, serious, critical]. */
  data: {
    series: readonly TokenTuple[];
    sequential: readonly TokenTuple[];
    diverging: readonly [TokenTuple, TokenTuple, TokenTuple];
    other: TokenTuple;
    status: readonly [TokenTuple, TokenTuple, TokenTuple, TokenTuple];
  };
}

const color = (t: RgbaTuple): ColorValue => ({ r: t[0], g: t[1], b: t[2], a: t[3] });
const token = (t: TokenTuple): ColorToken => new ColorToken(color(t[0]), color(t[1]));

/**
 * Rebuild an `AppTheme` from the serialized blob — the mirror of the generated `photonTheme` object:
 * surface tokens spread as top-level props, plus the four map-backed lookups (colors/type/elevation/
 * shape) with the same fallbacks the generator emits.
 */
export function materializeTheme(data: ThemeData): AppTheme {
  const surfaces: Record<string, ColorToken> = {};
  for (const key of Object.keys(data.surfaces)) surfaces[key] = token(data.surfaces[key]);

  const variants: Record<string, VariantColors> = {};
  for (const key of Object.keys(data.variants)) {
    const [base, onBase, pressed, subtle, onSubtle] = data.variants[key];
    variants[key] = new VariantColors(
      token(base),
      token(onBase),
      token(pressed),
      token(subtle),
      token(onSubtle),
    );
  }

  const typeScale: Record<string, TypeStyle> = {};
  for (const key of Object.keys(data.type)) {
    const [size, lineHeight, weight, tracking, maxScale] = data.type[key];
    typeScale[key] = new TypeStyle(size, lineHeight, weight, tracking, maxScale);
  }

  const elevations: ShadowSpec[] = data.elevations.map(([offsetY, blur, spread, c]) => ({
    offsetY,
    blur,
    spread,
    color: token(c),
  }));

  const palette = new DataPalette(
    data.data.series.map(token),
    data.data.sequential.map(token),
    new DivergingScale(
      token(data.data.diverging[0]),
      token(data.data.diverging[1]),
      token(data.data.diverging[2]),
    ),
    token(data.data.other),
    new StatusScale(
      token(data.data.status[0]),
      token(data.data.status[1]),
      token(data.data.status[2]),
      token(data.data.status[3]),
    ),
  );
  return {
    ...(surfaces as unknown as Pick<AppTheme, never>),
    data: palette,
    disabledOpacity: data.disabledOpacity,
    colors(variant: string): VariantColors {
      return variants[variant] ?? variants.primary;
    },
    type(role: string): TypeStyle {
      return typeScale[role] ?? typeScale.bodyL;
    },
    elevation(level: number): ShadowSpec {
      return elevations[Math.max(0, Math.min(5, Math.trunc(level)))];
    },
    code(kind: string): ColorToken {
      return codeTokenColor(this as unknown as AppTheme, kind);
    },
    shape(scale: string): number {
      return data.shape[scale] ?? data.shape.medium;
    },
  } as AppTheme;
}
