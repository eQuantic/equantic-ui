/**
 * The client half of the SSR→client THEME BRIDGE: rehydrate the JSON the server serialized from the
 * app-selected `IAppTheme` (see C# `ThemeBridge.SerializeJson`) back into an `AppTheme` the runtime
 * swaps in via `setPhotonTheme` at boot. This is what lets an app pick Material — or a seeded theme —
 * for hydration: client re-renders then resolve the SAME colors/shape the server baked into the SSR
 * markup. The wire shape mirrors the generated `photonTheme`; the round-trip is pinned in vitest.
 */

import { ColorToken, FluidSize, FontFamily, FontFile, TypeStyle, VariantColors, codeTokenColor } from './value-types';
import type { AppTheme, ShadowSpec } from './value-types';
import type { ColorValue } from './nodes';
import { DataPalette, DivergingScale, StatusScale } from './data-palette';
import { SheetTheme } from './sheet-theme';

type RgbaTuple = readonly [number, number, number, number];
type TokenTuple = readonly [RgbaTuple, RgbaTuple];
/** A face file on the wire: `[assembly, resource, minWeight, maxWeight, italic]`. */
type FontFileData = readonly [string, string, number, number, boolean];
/** A family on the wire: its name where it ships no file, else its name and each of its files. */
type FontFamilyData = string | readonly [string, ...FontFileData[]];

/** The exact JSON `ThemeBridge.SerializeJson` emits into `window.__EQ_THEME__`. */
export interface ThemeData {
  surfaces: Record<string, TokenTuple>;
  disabledOpacity: number;
  variants: Record<string, readonly TokenTuple[]>; // [base, onBase, pressed, subtle, onSubtle]
  /** `[size, lineHeight, weight, tracking, maxScale]`, plus `mono, italic, family` when a role
   *  carries any of them — the tail rides only when it says something. */
  type: Record<
    string,
    readonly [
      number,
      number,
      string,
      number,
      number,
      boolean?,
      boolean?,
      (FontFamilyData | null)?,
      (readonly [number, number, number])?,
    ]
  >;
  /** The theme's code face, present only when it names one — same rule as the type tail. */
  monoFamily?: FontFamilyData;
  /** The theme's text face, by the same rule. */
  textFamily?: FontFamilyData;
  /** The selected text's colour, present only when the theme names one. */
  onSelection?: TokenTuple;
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
  /** The spreadsheet's slot, present only when the theme answers one, with the members it names. */
  sheet?: Partial<Record<keyof SheetTheme, TokenTuple>>;
}

const color = (t: RgbaTuple): ColorValue => ({ r: t[0], g: t[1], b: t[2], a: t[3] });
const token = (t: TokenTuple): ColorToken => new ColorToken(color(t[0]), color(t[1]));

/**
 * The family `data` names, with its files when it ships them (C# `ThemeBridge.Family`): the family a
 * component reads after hydration is the one it read on the server, its files and its equality too.
 */
function fontFamily(data: FontFamilyData): FontFamily {
  if (typeof data === 'string') return new FontFamily(data);
  const [name, ...files] = data;
  return new FontFamily(
    name,
    ...files.map(([assembly, resource, minWeight, maxWeight, italic]) => new FontFile(assembly, resource, minWeight, maxWeight, italic)),
  );
}

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
    // The tail is OPTIONAL on the wire — a theme whose roles are all proportional, upright and
    // unnamed sends five values, which is every theme shipped with the SDK. Reading it with
    // defaults is what lets a branded theme keep its face through hydration; without this the
    // server rendered the brand and the client re-rendered the system font.
    const [size, lineHeight, weight, tracking, maxScale, mono, italic, family, fluid] = data.type[key];
    const style = new TypeStyle(
      size,
      lineHeight,
      weight,
      tracking,
      maxScale,
      mono ?? false,
      italic ?? false,
      family ? fontFamily(family) : undefined,
    );
    // A role whose size follows the window (C# ThemeBridge): the clamp rides last.
    typeScale[key] = fluid ? Object.assign(style, { fluid: new FluidSize(fluid[0], fluid[1], fluid[2]) }) : style;
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
  let sheet: SheetTheme | null = null;
  if (data.sheet) {
    sheet = new SheetTheme();
    for (const [name, value] of Object.entries(data.sheet)) {
      if (value) (sheet as unknown as Record<string, ColorToken>)[name] = token(value);
    }
  }
  return {
    ...(surfaces as unknown as Pick<AppTheme, never>),
    data: palette,
    sheet,
    disabledOpacity: data.disabledOpacity,
    monoFamily: data.monoFamily ? fontFamily(data.monoFamily) : undefined,
    textFamily: data.textFamily ? fontFamily(data.textFamily) : undefined,
    onSelection: data.onSelection ? token(data.onSelection) : undefined,
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
