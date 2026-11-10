/**
 * Mirrors of the C# theme slots of `docs/ELEMENT-TREE-PLAN.md` §3.10 that hold the control ladder:
 * `SizeLadder`, `DensityLadder`, `ControlsTheme` and `ButtonTheme`, every member optional. A member left
 * null is the next step of the slot order. Built from a config object, as eqc writes a record whose
 * members are all `init` (`new ButtonTheme({ minWidth })`), and from the bridge's JSON
 * (`theme-bridge.ts`).
 */

import { hashesByValue } from '../utils/hash';
import type { AppTheme } from './value-types';

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

  constructor(config?: Partial<ButtonTheme>) {
    if (config) Object.assign(this, config);
  }
}

// Records: each hashes by its members, as `equals` compares it (`ValueTwinHashTests` holds this to the C#).
hashesByValue(SizeLadder);
hashesByValue(DensityLadder);
hashesByValue(ControlsTheme);
hashesByValue(ButtonTheme);

/** The JSON shapes the bridge writes (C# `ThemeBridge.Controls` and `ButtonMembers`). */
export type SizeLadderData = Partial<Record<'small' | 'medium' | 'large' | 'xLarge', number>>;
export type DensityLadderData = { comfortable?: SizeLadderData; compact?: SizeLadderData };
export type ControlsData = {
  height?: DensityLadderData;
  paddingX?: DensityLadderData;
  labelSize?: DensityLadderData;
  gap?: SizeLadderData;
  icon?: SizeLadderData;
  radius?: SizeLadderData;
};
export type ButtonData = {
  controls?: ControlsData;
  minWidth?: number;
  labelWeight?: string;
  labelTracking?: number;
  outlineWidth?: number;
  linkPaddingX?: number;
};

const sizeLadder = (data: SizeLadderData | undefined): SizeLadder | null => (data ? new SizeLadder(data) : null);

const densityLadder = (data: DensityLadderData | undefined): DensityLadder | null =>
  data ? new DensityLadder({ comfortable: sizeLadder(data.comfortable), compact: sizeLadder(data.compact) }) : null;

/** The control ladder the bridge wrote, as the records the transpiled components read. */
export function controlsTheme(data: ControlsData): ControlsTheme {
  return new ControlsTheme({
    height: densityLadder(data.height),
    paddingX: densityLadder(data.paddingX),
    labelSize: densityLadder(data.labelSize),
    gap: sizeLadder(data.gap),
    icon: sizeLadder(data.icon),
    radius: sizeLadder(data.radius),
  });
}

/** The Button slot the bridge wrote. */
export function buttonTheme(data: ButtonData): ButtonTheme {
  return new ButtonTheme({
    controls: data.controls ? controlsTheme(data.controls) : null,
    minWidth: data.minWidth ?? null,
    labelWeight: data.labelWeight ?? null,
    labelTracking: data.labelTracking ?? null,
    outlineWidth: data.outlineWidth ?? null,
    linkPaddingX: data.linkPaddingX ?? null,
  });
}

/**
 * The twin of C# `AppThemeExtensions`, as the static home its extension lowers to (`theme.With(…)` is
 * `AppThemeExtensions.with(theme, …)`, the receiver first): the theme with a slot replaced and every
 * other member forwarded, its defaults included, as `OverriddenTheme` does.
 */
export class AppThemeExtensions {
  static with(theme: AppTheme, controls: ControlsTheme | null = null, button: ButtonTheme | null = null): AppTheme {
    return new Proxy(theme, {
      get(target, key) {
        if (key === 'controls') return controls ?? target.controls ?? null;
        if (key === 'button') return button ?? target.button ?? null;
        const value = Reflect.get(target, key, target);
        return typeof value === 'function' ? value.bind(target) : value;
      },
    });
  }
}
