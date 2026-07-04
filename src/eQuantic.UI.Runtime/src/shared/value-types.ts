/**
 * Runtime classes for the shared vocabulary's VALUE types — the exact call shapes eqc emits for
 * `eQuantic.UI.Primitives` structs (`new TypeStyle(15, 15, 'semiBold', 0.1, 1.3)`,
 * `SizeValue.fill`, `EdgeInsets.symmetric(16, 0)`, `token.withOpacity(0.38)`). Instances satisfy the
 * plain `*Value` interfaces in `nodes.ts`, so the lowering consumes them directly. C#'s implicit
 * float→SizeValue conversion has no JS equivalent — the node classes normalize raw numbers instead.
 */

import type { ColorValue, SizeKindValue, TypeStyleValue } from './nodes';

/** Paired light/dark color — mirrors `eQuantic.UI.Primitives.ColorToken` (channels are 0–255 bytes). */
export class ColorToken {
  readonly light: ColorValue;
  readonly dark: ColorValue;

  constructor(light: ColorValue, dark: ColorValue = light) {
    this.light = light;
    this.dark = dark;
  }

  /** Both modes with alpha scaled — the C# token-level disabled 38% group, byte-rounded like `Color.WithOpacity`. */
  withOpacity(opacity: number): ColorToken {
    const scale = (c: ColorValue): ColorValue => ({ ...c, a: Math.round(c.a * opacity) });
    return new ColorToken(scale(this.light), scale(this.dark));
  }
}

export class SizeValue {
  readonly kind: SizeKindValue;
  readonly value: number;

  private constructor(kind: SizeKindValue, value: number) {
    this.kind = kind;
    this.value = value;
  }

  static readonly hug = new SizeValue('hug', 0);
  static readonly fill = new SizeValue('fill', 0);
  static fixed(dp: number): SizeValue {
    return new SizeValue('fixed', dp);
  }

  /** C#'s implicit float→SizeValue: numbers appearing where a size is expected become Fixed. */
  static from(value: SizeValue | number | undefined | null): SizeValue | undefined {
    if (value === undefined || value === null) return undefined;
    return typeof value === 'number' ? SizeValue.fixed(value) : value;
  }
}

export class EdgeInsets {
  constructor(
    readonly start = 0,
    readonly top = 0,
    readonly end = 0,
    readonly bottom = 0,
  ) {}

  static all(value: number): EdgeInsets {
    return new EdgeInsets(value, value, value, value);
  }

  static symmetric(horizontal: number, vertical: number): EdgeInsets {
    return new EdgeInsets(horizontal, vertical, horizontal, vertical);
  }
}

export class CornerRadii {
  readonly topLeft: number;
  readonly topRight: number;
  readonly bottomRight: number;
  readonly bottomLeft: number;

  /** One argument = uniform radius (the C# `CornerRadii(float uniform)` overload); four = per corner. */
  constructor(topLeft = 0, topRight?: number, bottomRight?: number, bottomLeft?: number) {
    if (topRight === undefined) {
      this.topLeft = this.topRight = this.bottomRight = this.bottomLeft = topLeft;
    } else {
      this.topLeft = topLeft;
      this.topRight = topRight;
      this.bottomRight = bottomRight ?? 0;
      this.bottomLeft = bottomLeft ?? 0;
    }
  }
}

/** Positional mirror of the C# `TypeStyle(Size, LineHeight, Weight, Tracking, MaxScale)` record. */
export class TypeStyle implements TypeStyleValue {
  constructor(
    readonly size: number,
    readonly lineHeight: number,
    readonly weight: string | number,
    readonly tracking = 0,
    readonly maxScale = 1,
  ) {}
}

/** The five sub-tokens of an interactive variant (spec §01). */
export class VariantColors {
  constructor(
    readonly base: ColorToken,
    readonly onBase: ColorToken,
    readonly pressed: ColorToken,
    readonly subtle: ColorToken,
    readonly onSubtle: ColorToken,
  ) {}
}

/** One analytic rrect shadow (spec §05) — mirrors `ShadowSpec`. */
export interface ShadowSpec {
  offsetY: number;
  blur: number;
  spread: number;
  color: ColorToken;
}

/**
 * The theme contract transpiled components read through `context.theme` — the camelCase mirror of
 * `eQuantic.UI.Primitives.IAppTheme`. Variants/roles arrive as camelCase member-name strings (the
 * enum lowering). The values live in `design-system.generated.ts` — never hand-written.
 */
export interface AppTheme {
  background: ColorToken;
  surface: ColorToken;
  surfaceSubtle: ColorToken;
  border: ColorToken;
  borderStrong: ColorToken;
  textPrimary: ColorToken;
  textSecondary: ColorToken;
  textMuted: ColorToken;
  textInverse: ColorToken;
  focusRing: ColorToken;
  linkColor: ColorToken;
  scrim: ColorToken;
  disabledOpacity: number;
  colors(variant: string): VariantColors;
  type(role: string): TypeStyle;
  elevation(level: number): ShadowSpec;
}
