/**
 * Runtime classes for the shared vocabulary's VALUE types — the exact call shapes eqc emits for
 * `eQuantic.UI.Primitives` structs (`new TypeStyle(15, 15, 'semiBold', 0.1, 1.3)`,
 * `SizeValue.fill`, `EdgeInsets.symmetric(16, 0)`, `token.withOpacity(0.38)`). Instances satisfy the
 * plain `*Value` interfaces in `nodes.ts`, so the lowering consumes them directly. C#'s implicit
 * float→SizeValue conversion has no JS equivalent — the node classes normalize raw numbers instead.
 */

import { round as dotnetRound } from '../utils/dotnet-math';
import type { ColorValue, SizeKindValue, TypeStyleValue } from './nodes';
import type { DataPalette } from './data-palette';

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
    return { ...color, a: Math.max(0, Math.min(255, dotnetRound(color.a * opacity))) };
  },
  /** Per-channel midpoint, alpha included — byte-exact with C# `Color.MidpointWith`
   * (`(a + b + 1) >> 1`, the §10 hover derivation's primitive). */
  midpointWith(color: ColorValue, other: ColorValue): ColorValue {
    return {
      r: (color.r + other.r + 1) >> 1,
      g: (color.g + other.g + 1) >> 1,
      b: (color.b + other.b + 1) >> 1,
      a: (color.a + other.a + 1) >> 1,
    };
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
    const scale = (c: ColorValue): ColorValue => ({ ...c, a: dotnetRound(c.a * opacity) });
    return new ColorToken(scale(this.light), scale(this.dark));
  }

  /** Per-leg channel midpoint — the §10 hover derivation, byte-exact with C# `ColorToken.MidpointWith`. */
  midpointWith(other: ColorToken): ColorToken {
    return new ColorToken(
      Color.midpointWith(this.light, other.light),
      Color.midpointWith(this.dark, other.dark),
    );
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

  /**
   * C#'s `SizeValue.WindowMinus(inset)` — the window's extent less a fixed inset. The C# side threw
   * for a negative inset with this exact reasoning, and the twin agrees: an inset is what to
   * SUBTRACT, so it is never negative; for more than the window, ask for the size you want.
   */
  static windowMinus(inset: number): SizeValue {
    if (inset < 0) {
      throw new RangeError(
        'A window inset is what to SUBTRACT from the window, so it is never negative. ' +
          'For more than the window, ask for the size you want.',
      );
    }
    return new SizeValue('windowMinus', inset);
  }

  /** C#'s implicit float→SizeValue: numbers appearing where a size is expected become Fixed. */
  static from(value: SizeValue | number | undefined | null): SizeValue | undefined {
    if (value === undefined || value === null) return undefined;
    return typeof value === 'number' ? SizeValue.fixed(value) : value;
  }
}

/**
 * C#'s `WebContent`: what an embedded document IS — an address to load, or a document handed over
 * whole. Exactly one of the two, which is the entire reason the type exists: `WebFrame` used to
 * carry both as nullable strings with a precedence between them, stated only in the web realizer's
 * prose.
 *
 * The private constructor is the fence on both sides — `url` and `document` are the only ways to
 * make one, so no caller can build a value that is an address AND a document.
 */
export class WebContent {
  readonly value: string;
  readonly isInline: boolean;

  private constructor(value: string, isInline: boolean) {
    this.value = value;
    this.isInline = isInline;
  }

  /** Embed by ADDRESS: someone else's page, a map, a video. */
  static url(address: string): WebContent {
    return new WebContent(address, false);
  }

  /** Embed by VALUE: markup that exists only in memory and never at an address. */
  static document(markup: string): WebContent {
    return new WebContent(markup, true);
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

/**
 * SINGLE precision, because the subject is. Every component of this geometry is a C# `float`, so
 * every derived value is a float ADD or a float divide — and eqc emits `Math.fround` for exactly
 * that reason wherever it transpiles one. A twin doing the arithmetic in doubles answers a
 * different last bit, and a hit test comparing a pointer against an edge classifies it differently.
 * Found in review: the transpiled hit test this replaced was `Math.fround(b.x + b.width + slack)`,
 * and reaching for `Rect.inflate().right` quietly dropped the rounding.
 */
const f = Math.fround;

/**
 * Mirror of the C# `Point` — a point, and a vector, in the vocabulary's own geometry. Y grows DOWN,
 * the screen convention every target shares.
 */
export class Point {
  readonly x: number;
  readonly y: number;

  constructor(x = 0, y = 0) {
    // At STORAGE, which is where this SDK rounds: a C# `float` field holds the nearest float, so a
    // twin holding the double a caller passed already disagrees before any arithmetic runs.
    this.x = f(x);
    this.y = f(y);
  }

  static readonly zero = new Point(0, 0);

  dot(other: Point): number {
    return f(f(this.x * other.x) + f(this.y * other.y));
  }
  length(): number {
    return f(Math.sqrt(f(f(this.x * this.x) + f(this.y * this.y))));
  }
}

/** Mirror of the C# `Size`. */
export class Size {
  readonly width: number;
  readonly height: number;

  constructor(width = 0, height = 0) {
    this.width = f(width);
    this.height = f(height);
  }

  static readonly zero = new Size(0, 0);
}

/**
 * Mirror of the C# `Rect` — an axis-aligned box, x/y at its top-left.
 *
 * The four derived edges and the three operations are arithmetic, which means this twin can DRIFT
 * from its subject in a way a missing export cannot: it would load, and answer differently. So the
 * answers are cross-pinned — `PrimitiveValueFixtureTests` computes them in C# and
 * `primitives-exports.spec.ts` asserts these against that fixture.
 */
export class Rect {
  readonly x: number;
  readonly y: number;
  readonly width: number;
  readonly height: number;

  constructor(x = 0, y = 0, width = 0, height = 0) {
    this.x = f(x);
    this.y = f(y);
    this.width = f(width);
    this.height = f(height);
  }

  static fromLTRB(left: number, top: number, right: number, bottom: number): Rect {
    // A `float` PARAMETER is storage too, and that is the half this twin was missing: on the C#
    // side the four arguments are already single precision before `right - left` runs, because the
    // conversion happens at the call. Here they arrive as doubles, so the subtraction has to see
    // the rounded values — rounding only the result gives a different width for fractional input.
    // Found in review, after the same rule had already been applied to fields and to each step.
    const l = f(left);
    const t = f(top);
    return new Rect(l, t, f(f(right) - l), f(f(bottom) - t));
  }

  get left(): number {
    return this.x;
  }
  get top(): number {
    return this.y;
  }
  get right(): number {
    return f(this.x + this.width);
  }
  get bottom(): number {
    return f(this.y + this.height);
  }
  get center(): Point {
    return new Point(f(this.x + f(this.width / 2)), f(this.y + f(this.height / 2)));
  }
  get size(): Size {
    return new Size(this.width, this.height);
  }
  get isEmpty(): boolean {
    return this.width <= 0 || this.height <= 0;
  }

  contains(p: Point): boolean {
    return p.x >= this.left && p.x < this.right && p.y >= this.top && p.y < this.bottom;
  }

  /** The overlap with `other`, or an empty rect pinned at the overlap's corner when disjoint. */
  intersect(other: Rect): Rect {
    const l = Math.max(this.left, other.left);
    const t = Math.max(this.top, other.top);
    const r = Math.min(this.right, other.right);
    const b = Math.min(this.bottom, other.bottom);
    return r <= l || b <= t ? new Rect(l, t, 0, 0) : Rect.fromLTRB(l, t, r, b);
  }

  /** Grows the box by `amount` on every side; a negative amount insets it. */
  inflate(amount: number): Rect {
    // `amount` is a `float` parameter in the subject, so it is rounded before any of this runs —
    // see `fromLTRB`. The fields are already single precision; only the argument was not.
    const by = f(amount);
    return new Rect(
      f(this.x - by),
      f(this.y - by),
      f(this.width + f(by * 2)),
      f(this.height + f(by * 2)),
    );
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
    return new Transform2D(
      this.translateX,
      this.translateY,
      this.rotationDegrees,
      uniform,
      uniform,
    );
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
    /** The SLANTED cut — an AXIS, so it composes with weight and mono (C# `TypeStyle.Italic`). */
    readonly italic = false,
    /** The FACE by name (C# `TypeStyle.Family`), or undefined for the platform's own. */
    readonly family: string | undefined = undefined,
  ) {}

  /**
   * The same style at another SIZE, with the line box following the ratio — the C# twin of
   * `TypeStyle.WithSize`. Patching only the size leaves a bigger glyph in the old box, which is
   * how a descender ends up outside its line.
   */
  withSize(size: number): TypeStyle {
    const lineHeight =
      this.size <= 0
        ? this.lineHeight
        : dotnetRound(((this.lineHeight * size) / this.size) * 2) / 2;
    return new TypeStyle(
      size,
      lineHeight,
      this.weight,
      this.tracking,
      this.maxScale,
      this.mono,
      this.italic,
      // The face survives a resize. Dropping it here is invisible until a control that resizes its
      // own label — SegmentedControl, Stepper — comes out in the system face beside siblings that
      // did not resize and kept the brand's.
      this.family,
    );
  }

  /** A style from a SIZE alone, with the typographic default line box (1.25×). */
  static ofSize(size: number, weight: string | number, tracking = 0, maxScale = 1.3): TypeStyle {
    return new TypeStyle(size, dotnetRound(size * 1.25 * 2) / 2, weight, tracking, maxScale);
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

  /** The DERIVED §10 hover fill for a filled control: midpoint of base → pressed. Derived, never
   * a sixth slot — mirrors C# `VariantColors.Hover`. */
  get hover(): ColorToken {
    return this.base.midpointWith(this.pressed);
  }
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
  /** The colours a chart draws DATA with (C# `IAppTheme.Data`): series, sequential, diverging, other, status. */
  data: DataPalette;
  disabledOpacity: number;
  /**
   * The face this theme sets CODE in (C# `IAppTheme.MonoFamily`), or undefined for the platform's
   * own fixed-pitch face. Here because a component may read it: `IAppTheme` is vocabulary, so a
   * property the server answers and the client does not is the hydration hole `[ServerOnly]` and
   * EQ2010 exist to close — and a theme property is better carried than fenced.
   */
  monoFamily?: string;
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

/**
 * A `FontWeight` as CSS writes it. The enum lowers to its camelCase MEMBER NAME (`'semiBold'`), and
 * a name is not a weight anywhere CSS parses one: `ctx.font = "regular 11.5px ui-monospace"` is an
 * invalid shorthand, which a canvas answers by silently keeping `10px sans-serif` — so a code
 * editor measuring its column width got the advance of a proportional face at the wrong size, and
 * placed its caret four characters from where the text actually was.
 */
export function cssFontWeight(weight: string | number | undefined): number {
  if (typeof weight === 'number') return weight;
  switch (weight) {
    case 'medium':
      return 500;
    case 'semiBold':
      return 600;
    case 'bold':
      return 700;
    case 'extraBold':
      return 800;
    default:
      return 400;
  }
}

/**
 * Twin of C# `FaceName.IsWellFormed`. What may be spelled as a font family — asked before a family
 * is embedded, because a family is the only free-form text the style pipeline carries.
 *
 * A PREDICATE rather than an escaper, and deliberately: the family lands inside a `<style>` element
 * and inside JSON in a `<script>` element, and neither a CSS string nor a JSON string neutralises
 * `</style>` or `</script>` for the HTML parser — that needs a CSS hex escape in one context and a
 * `\u003c` in the other. Two emitters in two languages reproducing two escape grammars identically
 * is the divergence this repo's cross-pins exist to catch. One rule, both sides hold it.
 *
 * The rule must match the C# character for character: SSR emits from there and hydration from here,
 * so a family one side accepts and the other rejects is a hydration mismatch.
 */
export function isWellFormedFace(family: string | null | undefined): family is string {
  // `null` as well as `undefined`: C# `Family` is nullable and an explicitly supplied default
  // crosses the wire AS null, so a predicate that only guarded `undefined` would throw on
  // `family.length` in the middle of hydration — an unnamed face is the documented default, not an
  // error.
  if (family === null || family === undefined) return false;
  if (family.length === 0 || family.length > 128) return false;
  if (family[0] === ' ' || family[family.length - 1] === ' ') return false;
  // Unicode letters and digits, so a CJK or Cyrillic family passes; the punctuation is what real
  // families use, and nothing that means anything to CSS, JSON or HTML.
  return /^[\p{L}\p{Nd} \-_.+]+$/u.test(family);
}

/**
 * The twin of C# `FaceName`, under its own name — eqc routes the whole `eQuantic.UI.Primitives`
 * namespace to `@equantic/runtime`, so a transpiled component writing `FaceName.IsWellFormed(x)`
 * resolves here.
 */
export const FaceName = {
  isWellFormed: isWellFormedFace,
};
