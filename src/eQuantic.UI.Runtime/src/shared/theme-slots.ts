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
}

/** A `SizeLadder` per density: both ride, since the density is the browser's to report. */
export class DensityLadder {
  comfortable: SizeLadder | null = null;
  compact: SizeLadder | null = null;

  constructor(config?: Partial<DensityLadder>) {
    if (config) Object.assign(this, config);
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
}

/** A number per density, each side optional, for a control that varies by density alone (E6b). */
export class DensityValue {
  comfortable: number | null = null;
  compact: number | null = null;

  constructor(config?: Partial<DensityValue>) {
    if (config) Object.assign(this, config);
  }
}

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

  constructor(config?: Partial<InputTheme>) {
    if (config) Object.assign(this, config);
  }
}

/** The selection controls' slot: Checkbox, RadioGroup, Switch and Slider (E6b, #1242). */
export class SelectionControlsTheme {
  box: DensityValue | null = null;
  radioSize: DensityValue | null = null;
  borderWidth: number | null = null;
  borderColor: ColorToken | null = null;
  checkboxRadius: number | null = null;
  radioDot: DensityValue | null = null;
  radioRowHeight: number | null = null;
  labelGap: number | null = null;
  switchWidth: DensityValue | null = null;
  switchHeight: DensityValue | null = null;
  switchThumb: DensityValue | null = null;
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
}

/** The segmented controls' slot: SegmentedControl and Stepper (E6b, #1242). */
export class SegmentedTheme {
  controls: ControlsTheme | null = null;
  inset: number | null = null;
  trackFill: ColorToken | null = null;
  thumbFill: ColorToken | null = null;
  thumbElevation: number | null = null;
  borderWidth: number | null = null;
  borderColor: ColorToken | null = null;
  iconSize: number | null = null;

  constructor(config?: Partial<SegmentedTheme>) {
    if (config) Object.assign(this, config);
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
}

// Records: each hashes by its members, as `equals` compares it (`ValueTwinHashTests` holds this to the C#).
hashesByValue(SizeLadder);
hashesByValue(DensityLadder);
hashesByValue(ControlsTheme);
hashesByValue(ButtonTheme);
hashesByValue(DensityValue);
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

export const slotDensityValue = (data: unknown): DensityValue | null =>
  data ? new DensityValue(data as Partial<DensityValue>) : null;

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
  });
}

/** The selection controls' slot the bridge wrote. */
export function selectionControlsTheme(data: SlotData): SelectionControlsTheme {
  return new SelectionControlsTheme({
    box: slotDensityValue(data.box),
    radioSize: slotDensityValue(data.radioSize),
    borderWidth: slotValue<number>(data.borderWidth),
    borderColor: slotToken(data.borderColor),
    checkboxRadius: slotValue<number>(data.checkboxRadius),
    radioDot: slotDensityValue(data.radioDot),
    radioRowHeight: slotValue<number>(data.radioRowHeight),
    labelGap: slotValue<number>(data.labelGap),
    switchWidth: slotDensityValue(data.switchWidth),
    switchHeight: slotDensityValue(data.switchHeight),
    switchThumb: slotDensityValue(data.switchThumb),
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
 * `AppThemeExtensions.with(theme, …)`, the receiver first): the theme with a slot replaced and every
 * other member forwarded, its defaults included, as `OverriddenTheme` does.
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
    return new Proxy(theme, {
      get(target, key) {
        if (key === 'controls') return controls ?? target.controls ?? null;
        if (key === 'button') return button ?? target.button ?? null;
        if (key === 'iconButton') return iconButton ?? target.iconButton ?? null;
        if (key === 'input') return input ?? target.input ?? null;
        if (key === 'selectionControls') return selectionControls ?? target.selectionControls ?? null;
        if (key === 'segmented') return segmented ?? target.segmented ?? null;
        if (key === 'chip') return chip ?? target.chip ?? null;
        const value = Reflect.get(target, key, target);
        return typeof value === 'function' ? value.bind(target) : value;
      },
    });
  }
}
