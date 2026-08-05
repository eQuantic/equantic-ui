/**
 * GENERATED — do not edit. Every value comes from the C# design-system single source
 * (eQuantic.UI.Primitives). Regenerate: EQ_UPDATE_DESIGN_TS=1 dotnet test eQuantic.UI.Web.Tests
 * (DesignSystemTsGeneratorTests pins this file byte-for-byte against the generator).
 */

import { ColorToken, TypeStyle, VariantColors } from './value-types';
import type { AppTheme, ShadowSpec } from './value-types';
import type { ColorValue } from './nodes';

const c = (r: number, g: number, b: number, a: number): ColorValue => ({ r, g, b, a });
const t = (light: ColorValue, dark: ColorValue): ColorToken => new ColorToken(light, dark);

export const Space = {
  s1: 4,
  s2: 8,
  s3: 12,
  s4: 16,
  s5: 20,
  s6: 24,
  s8: 32,
  s10: 40,
  s12: 48,
  s16: 64,
  gutter: 16,
} as const;

export const Radius = {
  xs: 4,
  sm: 6,
  md: 10,
  lg: 14,
  xl: 20,
  full: 999,
} as const;

export const IconSize = {
  sm: 16,
  dense: 20,
  md: 24,
  lg: 32,
} as const;

export const Touch = {
  minTarget: 48,
  pressCancelSlop: 12,
  wheelLine: 16,
} as const;

export const Curve = {
  standard: [0.2, 0, 0, 1],
  decelerate: [0, 0, 0, 1],
  accelerate: [0.3, 0, 1, 1],
} as const;

export const Motion = {
  fastMs: 100,
  baseMs: 200,
  slowMs: 300,
  reducedCrossfadeMs: 120,
  press: { durationMs: 100, curve: Curve.standard },
  state: { durationMs: 200, curve: Curve.standard },
  enter: { durationMs: 300, curve: Curve.decelerate },
  exit: { durationMs: 200, curve: Curve.accelerate },
} as const;

/** The control ladder — every control of a given size measures the same (spec A12). */
export const Sizing = {
  avatar(size: string): number {
    switch (size) {
      case 'small': return 24;
      case 'medium': return 32;
      case 'large': return 40;
      default: return 56;
    }
  },
  gap(size: string): number {
    switch (size) {
      case 'small': return 6;
      case 'medium': return 8;
      case 'large': return 8;
      default: return 10;
    }
  },
  height(size: string): number {
    switch (size) {
      case 'small': return 32;
      case 'medium': return 40;
      case 'large': return 48;
      default: return 56;
    }
  },
  hitTarget(size: string): number {
    switch (size) {
      case 'small': return 48;
      case 'medium': return 48;
      case 'large': return 48;
      default: return 56;
    }
  },
  icon(size: string): number {
    switch (size) {
      case 'small': return 16;
      case 'medium': return 20;
      case 'large': return 20;
      default: return 24;
    }
  },
  labelSize(size: string): number {
    switch (size) {
      case 'small': return 13;
      case 'medium': return 15;
      case 'large': return 16;
      default: return 17;
    }
  },
  paddingX(size: string): number {
    switch (size) {
      case 'small': return 12;
      case 'medium': return 16;
      case 'large': return 20;
      default: return 24;
    }
  },
  radius(size: string): number {
    switch (size) {
      case 'small': return 10;
      case 'medium': return 10;
      case 'large': return 10;
      default: return 14;
    }
  },
};

/** Height · PadX · Gap · Label · Icon · Radius · HitTarget — the spec A12 size table. */
export const ButtonStyles = {
  minWidth: 64,
  metrics(size: string): [number, number, number, number, number, number, number] {
    switch (size) {
      case 'small': return [32, 12, 6, 13, 16, 10, 48];
      case 'medium': return [40, 16, 8, 15, 20, 10, 48];
      case 'large': return [48, 20, 8, 16, 20, 10, 48];
      default: return [56, 24, 10, 17, 24, 14, 56];
    }
  },
};

const variantColors: Record<string, VariantColors> = {
  primary: new VariantColors(
    t(c(0, 80, 160, 255), c(92, 162, 232, 255)),
    t(c(255, 255, 255, 255), c(6, 38, 63, 255)),
    t(c(0, 66, 127, 255), c(124, 181, 238, 255)),
    t(c(232, 241, 250, 255), c(15, 39, 64, 255)),
    t(c(0, 62, 126, 255), c(168, 205, 242, 255)),
  ),
  secondary: new VariantColors(
    t(c(233, 237, 242, 255), c(36, 44, 54, 255)),
    t(c(58, 67, 80, 255), c(215, 221, 229, 255)),
    t(c(220, 226, 234, 255), c(46, 56, 68, 255)),
    t(c(233, 237, 242, 255), c(36, 44, 54, 255)),
    t(c(58, 67, 80, 255), c(215, 221, 229, 255)),
  ),
  destructive: new VariantColors(
    t(c(180, 35, 24, 255), c(229, 100, 92, 255)),
    t(c(255, 255, 255, 255), c(59, 7, 4, 255)),
    t(c(143, 29, 29, 255), c(242, 139, 133, 255)),
    t(c(252, 235, 234, 255), c(58, 18, 16, 255)),
    t(c(143, 29, 29, 255), c(244, 169, 164, 255)),
  ),
  outline: new VariantColors(
    t(c(0, 0, 0, 0), c(0, 0, 0, 0)),
    t(c(23, 27, 33, 255), c(242, 244, 247, 255)),
    t(c(239, 241, 244, 255), c(28, 35, 43, 255)),
    t(c(0, 0, 0, 0), c(0, 0, 0, 0)),
    t(c(23, 27, 33, 255), c(242, 244, 247, 255)),
  ),
  ghost: new VariantColors(
    t(c(0, 0, 0, 0), c(0, 0, 0, 0)),
    t(c(23, 27, 33, 255), c(242, 244, 247, 255)),
    t(c(239, 241, 244, 255), c(28, 35, 43, 255)),
    t(c(0, 0, 0, 0), c(0, 0, 0, 0)),
    t(c(23, 27, 33, 255), c(242, 244, 247, 255)),
  ),
  link: new VariantColors(
    t(c(0, 0, 0, 0), c(0, 0, 0, 0)),
    t(c(0, 80, 160, 255), c(124, 181, 238, 255)),
    t(c(0, 0, 0, 0), c(0, 0, 0, 0)),
    t(c(0, 0, 0, 0), c(0, 0, 0, 0)),
    t(c(0, 80, 160, 255), c(124, 181, 238, 255)),
  ),
  success: new VariantColors(
    t(c(59, 122, 34, 255), c(133, 192, 94, 255)),
    t(c(255, 255, 255, 255), c(18, 41, 10, 255)),
    t(c(44, 94, 23, 255), c(154, 205, 116, 255)),
    t(c(237, 246, 230, 255), c(22, 41, 12, 255)),
    t(c(44, 94, 23, 255), c(181, 219, 151, 255)),
  ),
  warning: new VariantColors(
    t(c(138, 90, 0, 255), c(232, 176, 75, 255)),
    t(c(255, 255, 255, 255), c(46, 27, 2, 255)),
    t(c(110, 71, 0, 255), c(240, 192, 107, 255)),
    t(c(251, 243, 223, 255), c(46, 32, 8, 255)),
    t(c(122, 82, 0, 255), c(238, 207, 143, 255)),
  ),
  info: new VariantColors(
    t(c(12, 108, 134, 255), c(76, 195, 222, 255)),
    t(c(255, 255, 255, 255), c(6, 43, 51, 255)),
    t(c(10, 84, 104, 255), c(110, 208, 230, 255)),
    t(c(228, 243, 248, 255), c(10, 42, 50, 255)),
    t(c(10, 84, 104, 255), c(159, 220, 235, 255)),
  ),
  tertiary: new VariantColors(
    t(c(12, 108, 134, 255), c(76, 195, 222, 255)),
    t(c(255, 255, 255, 255), c(6, 43, 51, 255)),
    t(c(10, 84, 104, 255), c(110, 208, 230, 255)),
    t(c(228, 243, 248, 255), c(10, 42, 50, 255)),
    t(c(10, 84, 104, 255), c(159, 220, 235, 255)),
  ),
};

const typeScale: Record<string, TypeStyle> = {
  display: new TypeStyle(34, 40, 'extraBold', -0.4, 1.15),
  heading: new TypeStyle(28, 34, 'bold', -0.3, 1.15),
  title: new TypeStyle(20, 26, 'semiBold', -0.2, 1.25),
  bodyL: new TypeStyle(17, 24, 'regular', 0, 1.3),
  bodyM: new TypeStyle(15, 20, 'regular', 0, 1.3),
  label: new TypeStyle(13, 16, 'semiBold', 0.1, 1.3),
  caption: new TypeStyle(12, 16, 'medium', 0.2, 1.3),
  titleSmall: new TypeStyle(15, 20, 'bold', -0.1, 1.3),
  labelSmall: new TypeStyle(11, 15, 'medium', 0.1, 1.3),
  overline: new TypeStyle(10, 14, 'extraBold', 1, 1.2),
};

const elevations: ShadowSpec[] = [
  { offsetY: 0, blur: 0, spread: 0, color: t(c(0, 0, 0, 0), c(0, 0, 0, 0)) },
  { offsetY: 1, blur: 3, spread: 0, color: t(c(15, 23, 32, 26), c(0, 0, 0, 115)) },
  { offsetY: 2, blur: 8, spread: 0, color: t(c(15, 23, 32, 31), c(0, 0, 0, 122)) },
  { offsetY: 6, blur: 16, spread: -2, color: t(c(15, 23, 32, 41), c(0, 0, 0, 133)) },
  { offsetY: 12, blur: 28, spread: -4, color: t(c(15, 23, 32, 51), c(0, 0, 0, 143)) },
  { offsetY: 20, blur: 44, spread: -6, color: t(c(15, 23, 32, 66), c(0, 0, 0, 158)) },
];

const shapeScale: Record<string, number> = {
  none: 0,
  extraSmall: 4,
  small: 6,
  medium: 10,
  large: 14,
  extraLarge: 20,
  full: 999,
};

export const photonTheme: AppTheme = {
  background: t(c(245, 246, 248, 255), c(12, 15, 19, 255)),
  surface: t(c(255, 255, 255, 255), c(20, 24, 30, 255)),
  surfaceSubtle: t(c(239, 241, 244, 255), c(28, 35, 43, 255)),
  surfaceHighlight: t(c(255, 255, 255, 140), c(255, 255, 255, 18)),
  border: t(c(226, 229, 234, 255), c(42, 50, 61, 255)),
  borderStrong: t(c(201, 206, 214, 255), c(61, 71, 84, 255)),
  textPrimary: t(c(23, 27, 33, 255), c(242, 244, 247, 255)),
  textSecondary: t(c(75, 85, 99, 255), c(174, 183, 194, 255)),
  textMuted: t(c(95, 107, 122, 255), c(139, 149, 163, 255)),
  textInverse: t(c(255, 255, 255, 255), c(23, 27, 33, 255)),
  focusRing: t(c(0, 80, 160, 255), c(124, 181, 238, 255)),
  linkColor: t(c(0, 80, 160, 255), c(124, 181, 238, 255)),
  scrim: t(c(11, 14, 18, 102), c(0, 0, 0, 143)),
  disabledOpacity: 0.38,
  colors(variant: string): VariantColors {
    return variantColors[variant] ?? variantColors.primary;
  },
  type(role: string): TypeStyle {
    return typeScale[role] ?? typeScale.bodyL;
  },
  elevation(level: number): ShadowSpec {
    return elevations[Math.max(0, Math.min(5, Math.trunc(level)))];
  },
  shape(scale: string): number {
    return shapeScale[scale] ?? shapeScale.medium;
  },
};

/** The transpiled shape of `PhotonTheme.Instance` references. */
export const PhotonTheme = { instance: photonTheme };
