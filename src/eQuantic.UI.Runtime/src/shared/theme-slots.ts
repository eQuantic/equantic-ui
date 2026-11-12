/**
 * Mirrors of the C# theme slots of `docs/ELEMENT-TREE-PLAN.md` §3.10 that hold the control ladder:
 * `SizeLadder`, `DensityLadder`, `ControlsTheme` and `ButtonTheme`, every member optional. A member left
 * null is the next step of the slot order. Built from a config object, as eqc writes a record whose
 * members are all `init` (`new ButtonTheme({ minWidth })`), and from the bridge's JSON
 * (`theme-bridge.ts`).
 */

import { hashesByValue } from '../utils/hash';
import { ColorToken, EdgeInsets, TypeStyle, type AppTheme } from './value-types';
import type { ColorValue } from './nodes';
import { SheetTheme } from './sheet-theme';

/** A value per size, each optional (`small` · `medium` · `large` · `xLarge`). */
export class SizeLadder {
  small: number | null = null;
  medium: number | null = null;
  large: number | null = null;
  xLarge: number | null = null;

  constructor(config?: Partial<SizeLadder>) {
    if (config) Object.assign(this, config);
  }

  /** C# `SizeLadder.Merge`: `over`'s members laid over `under`'s (`mergeSlot`). */
  static merge(under: SizeLadder | null, over: SizeLadder | null): SizeLadder | null {
    return mergeSlot(under, over);
  }
}

/** A `SizeLadder` per density: both ride, since the density is the browser's to report. */
export class DensityLadder {
  comfortable: SizeLadder | null = null;
  compact: SizeLadder | null = null;

  constructor(config?: Partial<DensityLadder>) {
    if (config) Object.assign(this, config);
  }

  /** C# `DensityLadder.Merge`: `over`'s members laid over `under`'s (`mergeSlot`). */
  static merge(under: DensityLadder | null, over: DensityLadder | null): DensityLadder | null {
    return mergeSlot(under, over);
  }
}

/** The theme's control ladder: what `Sizing` holds, which a theme may now say. */
export class ControlsTheme {
  height: DensityLadder | null = null;
  paddingX: DensityLadder | null = null;
  labelSize: DensityLadder | null = null;
  gap: SizeLadder | null = null;
  icon: SizeLadder | null = null;
  radius: SizeLadder | null = null;

  constructor(config?: Partial<ControlsTheme>) {
    if (config) Object.assign(this, config);
  }

  /** C# `ControlsTheme.Merge`: `over`'s members laid over `under`'s (`mergeSlot`). */
  static merge(under: ControlsTheme | null, over: ControlsTheme | null): ControlsTheme | null {
    return mergeSlot(under, over);
  }
}

/** The Button's slot: its own ladder ahead of the theme's, and what it wrote as constants. */
export class ButtonTheme {
  controls: ControlsTheme | null = null;
  minWidth: number | null = null;
  labelWeight: string | null = null;
  labelTracking: number | null = null;
  outlineWidth: number | null = null;
  linkPaddingX: number | null = null;
  outlineColor: ColorToken | null = null;
  quietFill: ColorToken | null = null;

  constructor(config?: Partial<ButtonTheme>) {
    if (config) Object.assign(this, config);
  }

  /** C# `ButtonTheme.Merge`: `over`'s members laid over `under`'s (`mergeSlot`). */
  static merge(under: ButtonTheme | null, over: ButtonTheme | null): ButtonTheme | null {
    return mergeSlot(under, over);
  }
}

/** The slot records a merge recurses into: a member holding one is merged member by member, as the C#
 * `Merge` of the record that holds it does. A family adds its records here. */
export const slotClasses = new Set<unknown>([SizeLadder, DensityLadder, ControlsTheme, ButtonTheme]);

/**
 * The twin of every slot record's C# `Merge`: `over`'s members laid over `under`'s. A member `over`
 * names wins, one it leaves null is `under`'s, and a member holding a slot record merges the same way.
 * Null when both are. A colour, insets or a type style is a value and is taken whole.
 */
export function mergeSlot<T extends object>(under: T | null | undefined, over: T | null | undefined): T | null {
  if (over == null) return under ?? null;
  if (under == null) return over;
  const merged = new (over.constructor as new () => T)() as unknown as Record<string, unknown>;
  const below = under as unknown as Record<string, unknown>;
  const above = over as unknown as Record<string, unknown>;
  for (const key of new Set([...Object.keys(below), ...Object.keys(above)])) {
    const a = below[key];
    const b = above[key];
    merged[key] =
      a != null && b != null && typeof b === 'object' && slotClasses.has((b as object).constructor)
        ? mergeSlot(a as object, b as object)
        : (b ?? a ?? null);
  }
  return merged as unknown as T;
}

// Records: each hashes by its members, as `equals` compares it (`ValueTwinHashTests` holds this to the C#).
hashesByValue(SizeLadder);
hashesByValue(DensityLadder);
hashesByValue(ControlsTheme);
hashesByValue(ButtonTheme);

// ---- from the bridge's JSON (C# `ThemeBridge.SlotJson`) ------------------------------------------
// The bridge writes every slot by one rule: a member under its camel-cased name, a number as itself, an
// enum by its camel-cased name, a colour as a light/dark tuple, insets as `[start, top, end, bottom]`, a
// type style as the type scale writes one, a nested record as an object. A family's builder below turns
// its slot's object into its mirror with these converters, and registers itself in `slotBuilders`.

type RgbaTuple = readonly [number, number, number, number];
/** A colour on the wire. */
export type TokenTuple = readonly [RgbaTuple, RgbaTuple];
/** Insets on the wire: `[start, top, end, bottom]`. */
export type InsetsTuple = readonly [number, number, number, number];
/** A type style on the wire: `[size, lineHeight, weight, tracking, maxScale]`. */
export type TypeTuple = readonly [number, number, string, number, number];
/** A slot record on the wire: its members by name. */
export type SlotData = Record<string, unknown>;

const color = (t: RgbaTuple): ColorValue => ({ r: t[0], g: t[1], b: t[2], a: t[3] });

export const slotToken = (t: unknown): ColorToken | null =>
  t ? new ColorToken(color((t as TokenTuple)[0]), color((t as TokenTuple)[1])) : null;

export const slotInsets = (t: unknown): EdgeInsets | null => {
  if (!t) return null;
  const [start, top, end, bottom] = t as InsetsTuple;
  return new EdgeInsets(start, top, end, bottom);
};

export const slotType = (t: unknown): TypeStyle | null => {
  if (!t) return null;
  const [size, lineHeight, weight, tracking, maxScale] = t as TypeTuple;
  return new TypeStyle(size, lineHeight, weight, tracking, maxScale);
};

/** A number, a flag or an enum's name crosses as itself; absent is null, as a C# member left unset. */
export const slotValue = <T>(v: unknown): T | null => (v === undefined || v === null ? null : (v as T));

export const slotSizeLadder = (data: unknown): SizeLadder | null => (data ? new SizeLadder(data as Partial<SizeLadder>) : null);

export const slotDensityLadder = (data: unknown): DensityLadder | null => {
  if (!data) return null;
  const d = data as SlotData;
  return new DensityLadder({ comfortable: slotSizeLadder(d.comfortable), compact: slotSizeLadder(d.compact) });
};

/** The control ladder the bridge wrote. */
export function controlsTheme(data: SlotData): ControlsTheme {
  return new ControlsTheme({
    height: slotDensityLadder(data.height),
    paddingX: slotDensityLadder(data.paddingX),
    labelSize: slotDensityLadder(data.labelSize),
    gap: slotSizeLadder(data.gap),
    icon: slotSizeLadder(data.icon),
    radius: slotSizeLadder(data.radius),
  });
}

/** The Button slot the bridge wrote. */
export function buttonTheme(data: SlotData): ButtonTheme {
  return new ButtonTheme({
    controls: data.controls ? controlsTheme(data.controls as SlotData) : null,
    minWidth: slotValue<number>(data.minWidth),
    labelWeight: slotValue<string>(data.labelWeight),
    labelTracking: slotValue<number>(data.labelTracking),
    outlineWidth: slotValue<number>(data.outlineWidth),
    linkPaddingX: slotValue<number>(data.linkPaddingX),
    outlineColor: slotToken(data.outlineColor),
    quietFill: slotToken(data.quietFill),
  });
}

/** The spreadsheet's slot the bridge wrote: every member a colour. */
export function sheetTheme(data: SlotData): SheetTheme {
  const sheet = new SheetTheme();
  for (const [name, value] of Object.entries(data)) {
    if (value) (sheet as unknown as Record<string, ColorToken | null>)[name] = slotToken(value);
  }
  return sheet;
}

/**
 * Every slot the browser's theme carries, by the name the bridge writes it under and the builder that
 * reads it. `materializeTheme` walks this, so a family adds its slot here and nowhere in the bridge.
 */
export const slotBuilders: Record<string, (data: SlotData) => unknown> = {
  sheet: sheetTheme,
  controls: controlsTheme,
  button: buttonTheme,
};

/**
 * The twin of C# `AppThemeExtensions`, as the static home its extension lowers to (`theme.With(…)` is
 * `AppThemeExtensions.with(theme, …)`, the receiver first): the theme with each slot it is given laid over
 * its own, member by member (`mergeSlot`), and every other member forwarded, its defaults included, as
 * `OverriddenTheme` does.
 */
export class AppThemeExtensions {
  static with(theme: AppTheme, controls: ControlsTheme | null = null, button: ButtonTheme | null = null): AppTheme {
    // A map, so a slot's name is never confused with a member every object inherits; each merge once.
    const given = new Map<PropertyKey, object | null>([
      ['controls', controls],
      ['button', button],
    ]);
    const merged = new Map<PropertyKey, unknown>();
    return new Proxy(theme, {
      get(target, key) {
        if (given.has(key)) {
          if (!merged.has(key)) merged.set(key, mergeSlot(Reflect.get(target, key, target) as object | null, given.get(key)));
          return merged.get(key);
        }
        const value = Reflect.get(target, key, target);
        return typeof value === 'function' ? value.bind(target) : value;
      },
    });
  }
}
