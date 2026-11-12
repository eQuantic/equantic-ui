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
import type { TypeRoleValue } from './enums.generated';

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

/** A number per density, each side optional, for a control that varies by density alone (E6b). */
export class DensityPair {
  comfortable: number | null = null;
  compact: number | null = null;

  constructor(config?: Partial<DensityPair>) {
    if (config) Object.assign(this, config);
  }

  /** C# `DensityPair.Merge`: `over`'s members laid over `under`'s (`mergeSlot`). */
  static merge(under: DensityPair | null, over: DensityPair | null): DensityPair | null {
    return mergeSlot(under, over);
  }
}
slotClasses.add(DensityPair);

/** The IconButton's slot (E6b, #1242). */
export class IconButtonTheme {
  controls: ControlsTheme | null = null;
  iconSize: SizeLadder | null = null;
  radius: number | null = null;
  outlineWidth: number | null = null;
  outlineColor: ColorToken | null = null;
  glyphColor: ColorToken | null = null;
  quietFill: ColorToken | null = null;
  tonalPressOpacity: number | null = null;

  constructor(config?: Partial<IconButtonTheme>) {
    if (config) Object.assign(this, config);
  }

  /** C# `IconButtonTheme.Merge`: `over`'s members laid over `under`'s (`mergeSlot`). */
  static merge(under: IconButtonTheme | null, over: IconButtonTheme | null): IconButtonTheme | null {
    return mergeSlot(under, over);
  }
}

/** The fields' slot: TextInput, SearchField, Select and TimePicker (E6b, #1242). */
export class InputTheme {
  controls: ControlsTheme | null = null;
  paddingX: number | null = null;
  pickerPaddingX: number | null = null;
  gap: number | null = null;
  pickerGap: number | null = null;
  borderWidth: number | null = null;
  focusBorderWidth: number | null = null;
  borderColor: ColorToken | null = null;
  radius: number | null = null;
  searchRadius: number | null = null;
  fill: ColorToken | null = null;
  disabledFill: ColorToken | null = null;
  searchFill: ColorToken | null = null;
  iconSize: number | null = null;
  labelGap: number | null = null;
  captionGap: number | null = null;
  labelRole: TypeRoleValue | null = null;
  captionRole: TypeRoleValue | null = null;
  panelElevation: number | null = null;
  panelBorderColor: ColorToken | null = null;
  panelRowHeight: number | null = null;

  constructor(config?: Partial<InputTheme>) {
    if (config) Object.assign(this, config);
  }

  /** C# `InputTheme.Merge`: `over`'s members laid over `under`'s (`mergeSlot`). */
  static merge(under: InputTheme | null, over: InputTheme | null): InputTheme | null {
    return mergeSlot(under, over);
  }
}

/** The selection controls' slot: Checkbox, RadioGroup, Switch and Slider (E6b, #1242). */
export class SelectionControlsTheme {
  box: DensityPair | null = null;
  radioSize: DensityPair | null = null;
  borderWidth: number | null = null;
  borderColor: ColorToken | null = null;
  checkboxRadius: number | null = null;
  radioDot: DensityPair | null = null;
  radioRowHeight: number | null = null;
  labelGap: number | null = null;
  switchWidth: DensityPair | null = null;
  switchHeight: DensityPair | null = null;
  switchThumb: DensityPair | null = null;
  switchInset: number | null = null;
  switchOffTrack: ColorToken | null = null;
  thumbFill: ColorToken | null = null;
  switchThumbElevation: number | null = null;
  sliderTrackHeight: number | null = null;
  sliderThumbSize: number | null = null;
  sliderThumbBorder: number | null = null;
  sliderThumbBorderColor: ColorToken | null = null;
  sliderThumbElevation: number | null = null;
  sliderRestTrack: ColorToken | null = null;
  sliderMinWidth: number | null = null;

  constructor(config?: Partial<SelectionControlsTheme>) {
    if (config) Object.assign(this, config);
  }

  /** C# `SelectionControlsTheme.Merge`: `over`'s members laid over `under`'s (`mergeSlot`). */
  static merge(under: SelectionControlsTheme | null, over: SelectionControlsTheme | null): SelectionControlsTheme | null {
    return mergeSlot(under, over);
  }
}

/** The segmented controls' slot: SegmentedControl and Stepper (E6b, #1242). */
export class SegmentedTheme {
  controls: ControlsTheme | null = null;
  inset: number | null = null;
  trackFill: ColorToken | null = null;
  thumbFill: ColorToken | null = null;
  thumbElevation: number | null = null;
  trackBorderWidth: number | null = null;
  trackBorderColor: ColorToken | null = null;
  borderWidth: number | null = null;
  borderColor: ColorToken | null = null;
  iconSize: number | null = null;

  constructor(config?: Partial<SegmentedTheme>) {
    if (config) Object.assign(this, config);
  }

  /** C# `SegmentedTheme.Merge`: `over`'s members laid over `under`'s (`mergeSlot`). */
  static merge(under: SegmentedTheme | null, over: SegmentedTheme | null): SegmentedTheme | null {
    return mergeSlot(under, over);
  }
}

/** The Chip's slot (E6b, #1242). */
export class ChipTheme {
  controls: ControlsTheme | null = null;
  paddingX: number | null = null;
  gap: number | null = null;
  labelSize: number | null = null;
  labelLineHeight: number | null = null;
  labelWeight: string | null = null;
  labelTracking: number | null = null;
  radius: number | null = null;
  fill: ColorToken | null = null;
  selectedBorderWidth: number | null = null;
  pressOpacity: number | null = null;
  iconSize: number | null = null;
  removeIconSize: number | null = null;

  constructor(config?: Partial<ChipTheme>) {
    if (config) Object.assign(this, config);
  }

  /** C# `ChipTheme.Merge`: `over`'s members laid over `under`'s (`mergeSlot`). */
  static merge(under: ChipTheme | null, over: ChipTheme | null): ChipTheme | null {
    return mergeSlot(under, over);
  }
}

// Records: each hashes by its members, as `equals` compares it (`ValueTwinHashTests` holds this to the C#).
hashesByValue(SizeLadder);
hashesByValue(DensityLadder);
hashesByValue(ControlsTheme);
hashesByValue(ButtonTheme);
hashesByValue(DensityPair);
hashesByValue(IconButtonTheme);
hashesByValue(InputTheme);
hashesByValue(SelectionControlsTheme);
hashesByValue(SegmentedTheme);
hashesByValue(ChipTheme);

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

export const slotDensityPair = (data: unknown): DensityPair | null =>
  data ? new DensityPair(data as Partial<DensityPair>) : null;

const slotControls = (data: unknown): ControlsTheme | null => (data ? controlsTheme(data as SlotData) : null);

/** The IconButton slot the bridge wrote. */
export function iconButtonTheme(data: SlotData): IconButtonTheme {
  return new IconButtonTheme({
    controls: slotControls(data.controls),
    iconSize: slotSizeLadder(data.iconSize),
    radius: slotValue<number>(data.radius),
    outlineWidth: slotValue<number>(data.outlineWidth),
    outlineColor: slotToken(data.outlineColor),
    glyphColor: slotToken(data.glyphColor),
    quietFill: slotToken(data.quietFill),
    tonalPressOpacity: slotValue<number>(data.tonalPressOpacity),
  });
}

/** The fields' slot the bridge wrote. */
export function inputTheme(data: SlotData): InputTheme {
  return new InputTheme({
    controls: slotControls(data.controls),
    paddingX: slotValue<number>(data.paddingX),
    pickerPaddingX: slotValue<number>(data.pickerPaddingX),
    gap: slotValue<number>(data.gap),
    pickerGap: slotValue<number>(data.pickerGap),
    borderWidth: slotValue<number>(data.borderWidth),
    focusBorderWidth: slotValue<number>(data.focusBorderWidth),
    borderColor: slotToken(data.borderColor),
    radius: slotValue<number>(data.radius),
    searchRadius: slotValue<number>(data.searchRadius),
    fill: slotToken(data.fill),
    disabledFill: slotToken(data.disabledFill),
    searchFill: slotToken(data.searchFill),
    iconSize: slotValue<number>(data.iconSize),
    labelGap: slotValue<number>(data.labelGap),
    captionGap: slotValue<number>(data.captionGap),
    labelRole: slotValue<TypeRoleValue>(data.labelRole),
    captionRole: slotValue<TypeRoleValue>(data.captionRole),
    panelElevation: slotValue<number>(data.panelElevation),
    panelBorderColor: slotToken(data.panelBorderColor),
    panelRowHeight: slotValue<number>(data.panelRowHeight),
  });
}

/** The selection controls' slot the bridge wrote. */
export function selectionControlsTheme(data: SlotData): SelectionControlsTheme {
  return new SelectionControlsTheme({
    box: slotDensityPair(data.box),
    radioSize: slotDensityPair(data.radioSize),
    borderWidth: slotValue<number>(data.borderWidth),
    borderColor: slotToken(data.borderColor),
    checkboxRadius: slotValue<number>(data.checkboxRadius),
    radioDot: slotDensityPair(data.radioDot),
    radioRowHeight: slotValue<number>(data.radioRowHeight),
    labelGap: slotValue<number>(data.labelGap),
    switchWidth: slotDensityPair(data.switchWidth),
    switchHeight: slotDensityPair(data.switchHeight),
    switchThumb: slotDensityPair(data.switchThumb),
    switchInset: slotValue<number>(data.switchInset),
    switchOffTrack: slotToken(data.switchOffTrack),
    thumbFill: slotToken(data.thumbFill),
    switchThumbElevation: slotValue<number>(data.switchThumbElevation),
    sliderTrackHeight: slotValue<number>(data.sliderTrackHeight),
    sliderThumbSize: slotValue<number>(data.sliderThumbSize),
    sliderThumbBorder: slotValue<number>(data.sliderThumbBorder),
    sliderThumbBorderColor: slotToken(data.sliderThumbBorderColor),
    sliderThumbElevation: slotValue<number>(data.sliderThumbElevation),
    sliderRestTrack: slotToken(data.sliderRestTrack),
    sliderMinWidth: slotValue<number>(data.sliderMinWidth),
  });
}

/** The segmented controls' slot the bridge wrote. */
export function segmentedTheme(data: SlotData): SegmentedTheme {
  return new SegmentedTheme({
    controls: slotControls(data.controls),
    inset: slotValue<number>(data.inset),
    trackFill: slotToken(data.trackFill),
    thumbFill: slotToken(data.thumbFill),
    thumbElevation: slotValue<number>(data.thumbElevation),
    trackBorderWidth: slotValue<number>(data.trackBorderWidth),
    trackBorderColor: slotToken(data.trackBorderColor),
    borderWidth: slotValue<number>(data.borderWidth),
    borderColor: slotToken(data.borderColor),
    iconSize: slotValue<number>(data.iconSize),
  });
}

/** The Chip slot the bridge wrote. */
export function chipTheme(data: SlotData): ChipTheme {
  return new ChipTheme({
    controls: slotControls(data.controls),
    paddingX: slotValue<number>(data.paddingX),
    gap: slotValue<number>(data.gap),
    labelSize: slotValue<number>(data.labelSize),
    labelLineHeight: slotValue<number>(data.labelLineHeight),
    labelWeight: slotValue<string>(data.labelWeight),
    labelTracking: slotValue<number>(data.labelTracking),
    radius: slotValue<number>(data.radius),
    fill: slotToken(data.fill),
    selectedBorderWidth: slotValue<number>(data.selectedBorderWidth),
    pressOpacity: slotValue<number>(data.pressOpacity),
    iconSize: slotValue<number>(data.iconSize),
    removeIconSize: slotValue<number>(data.removeIconSize),
  });
}

/**
 * Every slot the browser's theme carries, by the name the bridge writes it under and the builder that
 * reads it. `materializeTheme` walks this, so a family adds its slot here and nowhere in the bridge.
 */
export const slotBuilders: Record<string, (data: SlotData) => unknown> = {
  sheet: sheetTheme,
  controls: controlsTheme,
  button: buttonTheme,
  iconButton: iconButtonTheme,
  input: inputTheme,
  selectionControls: selectionControlsTheme,
  segmented: segmentedTheme,
  chip: chipTheme,
};

/**
 * The twin of C# `AppThemeExtensions`, as the static home its extension lowers to (`theme.With(…)` is
 * `AppThemeExtensions.with(theme, …)`, the receiver first): the theme with each slot it is given laid over
 * its own, member by member (`mergeSlot`), and every other member forwarded, its defaults included, as
 * `OverriddenTheme` does.
 */
export class AppThemeExtensions {
  static with(
    theme: AppTheme,
    controls: ControlsTheme | null = null,
    button: ButtonTheme | null = null,
    iconButton: IconButtonTheme | null = null,
    input: InputTheme | null = null,
    selectionControls: SelectionControlsTheme | null = null,
    segmented: SegmentedTheme | null = null,
    chip: ChipTheme | null = null,
  ): AppTheme {
    // A map, so a slot's name is never confused with a member every object inherits; each merge once.
    const given = new Map<PropertyKey, object | null>([
      ['controls', controls],
      ['button', button],
      ['iconButton', iconButton],
      ['input', input],
      ['selectionControls', selectionControls],
      ['segmented', segmented],
      ['chip', chip],
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
