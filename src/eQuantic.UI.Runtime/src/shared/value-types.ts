/**
 * Runtime classes for the shared vocabulary's VALUE types — the exact call shapes eqc emits for
 * `eQuantic.UI.Primitives` structs (`new TypeStyle(15, 15, 'semiBold', 0.1, 1.3)`,
 * `SizeValue.fill`, `EdgeInsets.symmetric(16, 0)`, `token.withOpacity(0.38)`). Instances satisfy the
 * plain `*Value` interfaces in `nodes.ts`, so the lowering consumes them directly. C#'s implicit
 * float→SizeValue conversion has no JS equivalent — the node classes normalize raw numbers instead.
 */

import type { ColorValue, SizeKindValue, TypeStyleValue } from './nodes';

/**
 * Companion of the C# `Color` struct — the statics transpiled code references. The surface must
 * MIRROR `Primitives/Color.cs` member for member: a missing member is not a type error anywhere
 * (the transpiler routes `Color` to the runtime and trusts it), it is a module that fails to load
 * in the browser. `fromRgba` was missing until the site dogfood defined a brand token with alpha
 * and the page died with a misleading 404.
 */
export const Color = {
  transparent: { r: 0, g: 0, b: 0, a: 0 } as ColorValue,
  black: { r: 0, g: 0, b: 0, a: 255 } as ColorValue,
  white: { r: 255, g: 255, b: 255, a: 255 } as ColorValue,
  fromRgb(r: number, g: number, b: number): ColorValue {
    return { r, g, b, a: 255 };
  },
  fromRgba(r: number, g: number, b: number, a: number): ColorValue {
    return { r, g, b, a };
  },
  /** Alpha scaled by `opacity` (0..1) — byte-rounded exactly like C# `Color.WithOpacity`. */
  withOpacity(color: ColorValue, opacity: number): ColorValue {
    return { ...color, a: Math.max(0, Math.min(255, Math.round(color.a * opacity))) };
  },
};

/**
 * Mirror of the C# `[Flags] StyleChannels` (spec S6) — the transpiler emits flags enums
 * NUMERICALLY, so these bits ARE the wire values app code arrives with. Lives here (not in
 * vocabulary) so the lowering can read it without importing back into a module that imports it.
 */
export const StyleChannels = {
  none: 0,
  colors: 1,
  opacity: 2,
  transform: 4,
  shadow: 8,
  filters: 16,
  size: 32,
  all: 63,
} as const;

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

/** Mirror of the C# `Transform2D` — components applied translate → rotate → scale, center-anchored. */
export class Transform2D {
  constructor(
    readonly translateX = 0,
    readonly translateY = 0,
    readonly rotationDegrees = 0,
    readonly scaleX = 1,
    readonly scaleY = 1,
  ) {}

  static translate(x: number, y = 0): Transform2D {
    return new Transform2D(x, y);
  }
  static rotate(degrees: number): Transform2D {
    return new Transform2D(0, 0, degrees);
  }
  static scale(x: number, y?: number): Transform2D {
    return new Transform2D(0, 0, 0, x, y ?? x);
  }

  withTranslate(x: number, y = 0): Transform2D {
    return new Transform2D(x, y, this.rotationDegrees, this.scaleX, this.scaleY);
  }
  withRotate(degrees: number): Transform2D {
    return new Transform2D(this.translateX, this.translateY, degrees, this.scaleX, this.scaleY);
  }
  withScale(uniform: number): Transform2D {
    return new Transform2D(this.translateX, this.translateY, this.rotationDegrees, uniform, uniform);
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
    /** The MONOSPACED face — code, keys, versions (C# `TypeStyle.Mono`). */
    readonly mono = false,
  ) {}

  /**
   * The same style at another SIZE, with the line box following the ratio — the C# twin of
   * `TypeStyle.WithSize`. Patching only the size leaves a bigger glyph in the old box, which is
   * how a descender ends up outside its line.
   */
  withSize(size: number): TypeStyle {
    const lineHeight =
      this.size <= 0 ? this.lineHeight : Math.round((this.lineHeight * size) / this.size * 2) / 2;
    return new TypeStyle(size, lineHeight, this.weight, this.tracking, this.maxScale, this.mono);
  }

  /** A style from a SIZE alone, with the typographic default line box (1.25×). */
  static ofSize(size: number, weight: string | number, tracking = 0, maxScale = 1.3): TypeStyle {
    return new TypeStyle(size, Math.round(size * 1.25 * 2) / 2, weight, tracking, maxScale);
  }
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
  surfaceHighlight: ColorToken;
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
  shape(scale: string): number;
  /** Syntax ink for one code token kind (C# `IAppTheme.Code`). */
  code(kind: string): ColorToken;
}

/**
 * C# `IAppTheme.Code`'s default derivation, twin for twin: the mapping is chosen for READING —
 * comments recede to the muted tier, strings and numbers take the two calm variants, keywords take
 * the primary accent, and separating punctuation stays quieter than the code it separates. Every
 * AppTheme producer (the generated default and the SSR bridge) implements `code` through this one
 * function, so a custom palette crossing the bridge inherits it for free.
 */
export function codeTokenColor(theme: AppTheme, kind: string): ColorToken {
  switch (kind) {
    case 'keyword':
      return theme.colors('primary').base;
    case 'type':
      return theme.colors('info').base;
    case 'string':
      return theme.colors('success').base;
    case 'number':
      return theme.colors('warning').base;
    case 'comment':
      return theme.textMuted;
    case 'function':
      return theme.colors('tertiary').base;
    case 'attribute':
      return theme.colors('warning').onSubtle;
    case 'property':
      return theme.colors('info').onSubtle;
    case 'constant':
      return theme.colors('destructive').base;
    case 'operator':
    case 'punctuation':
      return theme.textSecondary;
    default:
      return theme.textPrimary;
  }
}
