/**
 * GENERATED — do not edit. Every value comes from the C# design-system single source
 * (eQuantic.UI.Primitives). Regenerate: EQ_UPDATE_DESIGN_TS=1 dotnet test eQuantic.UI.Web.Tests
 * (DesignSystemTsGeneratorTests pins this file byte-for-byte against the generator).
 */

import { ColorToken, TypeStyle, VariantColors, codeTokenColor } from './value-types';
import type { AppTheme, ShadowSpec } from './value-types';
import type { ColorValue } from './nodes';
import { DataPalette, DivergingScale, StatusScale } from './data-palette';
import { MotionSpec } from './primitive-values';
import { badgeTheme, buttonTheme, calendarTheme, controlsTheme, listTheme, markdownTheme, navigationTheme, tableTheme } from './theme-slots';

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
  minPointerTarget: 24,
  pressCancelSlop: 12,
  wheelLine: 16,
} as const;

export const Curve = {
  standard: { x1: 0.20000000298023224, y1: 0, x2: 0, y2: 1 },
  decelerate: { x1: 0, y1: 0, x2: 0, y2: 1 },
  accelerate: { x1: 0.30000001192092896, y1: 0, x2: 1, y2: 1 },
} as const;

export const Motion = {
  fastMs: 100,
  baseMs: 200,
  slowMs: 300,
  reducedCrossfadeMs: 120,
  press: new MotionSpec(100, Curve.standard),
  state: new MotionSpec(200, Curve.standard),
  enter: new MotionSpec(300, Curve.decelerate),
  exit: new MotionSpec(200, Curve.accelerate),
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
  avatarInitials(size: string): number {
    switch (size) {
      case 'small': return 10;
      case 'medium': return 13;
      case 'large': return 16;
      default: return 22;
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
  height(size: string, density = 'comfortable'): number {
    if (density === 'compact') {
      switch (size) {
        case 'small': return 26;
        case 'medium': return 32;
        case 'large': return 40;
        default: return 48;
      }
    }
    switch (size) {
      case 'small': return 32;
      case 'medium': return 40;
      case 'large': return 48;
      default: return 56;
    }
  },
  hitTarget(size: string, density = 'comfortable'): number {
    if (density === 'compact') {
      switch (size) {
        case 'small': return 26;
        case 'medium': return 32;
        case 'large': return 40;
        default: return 48;
      }
    }
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
  labelSize(size: string, density = 'comfortable'): number {
    if (density === 'compact') {
      switch (size) {
        case 'small': return 11.5;
        case 'medium': return 13;
        case 'large': return 14.5;
        default: return 15.5;
      }
    }
    switch (size) {
      case 'small': return 13;
      case 'medium': return 15;
      case 'large': return 16;
      default: return 17;
    }
  },
  paddingX(size: string, density = 'comfortable'): number {
    if (density === 'compact') {
      switch (size) {
        case 'small': return 8;
        case 'medium': return 12;
        case 'large': return 16;
        default: return 20;
      }
    }
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
  radioDot(density = 'comfortable'): number {
    return density === 'compact' ? 9 : 10;
  },
  selectionBox(density = 'comfortable'): number {
    return density === 'compact' ? 20 : 22;
  },
  switchHeight(density = 'comfortable'): number {
    return density === 'compact' ? 26 : 32;
  },
  switchThumb(density = 'comfortable'): number {
    return density === 'compact' ? 20 : 26;
  },
  switchTravel(density = 'comfortable'): number {
    return density === 'compact' ? 18 : 20;
  },
  switchWidth(density = 'comfortable'): number {
    return density === 'compact' ? 44 : 52;
  },
  buttonMinWidth: 64,
  switchInset: 3,
};

const variantColors: Record<string, VariantColors> = {
  primary: new VariantColors(
    t(c(0, 80, 160, 255), c(118, 186, 255, 255)),
    t(c(255, 255, 255, 255), c(6, 38, 63, 255)),
    t(c(0, 66, 127, 255), c(155, 205, 255, 255)),
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
    t(c(180, 35, 24, 255), c(255, 149, 140, 255)),
    t(c(255, 255, 255, 255), c(59, 7, 4, 255)),
    t(c(143, 29, 29, 255), c(254, 192, 187, 255)),
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
    t(c(0, 80, 160, 255), c(129, 186, 243, 255)),
    t(c(0, 80, 160, 255), c(129, 186, 243, 255)),
    t(c(0, 66, 127, 255), c(168, 205, 242, 255)),
    t(c(0, 0, 0, 0), c(0, 0, 0, 0)),
    t(c(0, 80, 160, 255), c(129, 186, 243, 255)),
  ),
  success: new VariantColors(
    t(c(59, 122, 34, 255), c(137, 197, 98, 255)),
    t(c(255, 255, 255, 255), c(18, 41, 10, 255)),
    t(c(44, 94, 23, 255), c(159, 210, 121, 255)),
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
    t(c(12, 108, 134, 255), c(77, 196, 223, 255)),
    t(c(255, 255, 255, 255), c(6, 43, 51, 255)),
    t(c(10, 84, 104, 255), c(111, 209, 231, 255)),
    t(c(228, 243, 248, 255), c(10, 42, 50, 255)),
    t(c(10, 84, 104, 255), c(159, 220, 235, 255)),
  ),
  tertiary: new VariantColors(
    t(c(12, 108, 134, 255), c(77, 196, 223, 255)),
    t(c(255, 255, 255, 255), c(6, 43, 51, 255)),
    t(c(10, 84, 104, 255), c(111, 209, 231, 255)),
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

// The data palette: eight series slots in a FIXED order, the sequential ramp, the diverging
// pair, the de-emphasis gray and the four status steps (C# `DataPalette.Default`).
const defaultData = new DataPalette(
  [
    t(c(42, 120, 214, 255), c(57, 135, 229, 255)),
    t(c(235, 104, 52, 255), c(217, 89, 38, 255)),
    t(c(27, 175, 122, 255), c(25, 158, 112, 255)),
    t(c(237, 161, 0, 255), c(201, 133, 0, 255)),
    t(c(232, 123, 164, 255), c(213, 81, 129, 255)),
    t(c(0, 131, 0, 255), c(0, 131, 0, 255)),
    t(c(74, 58, 167, 255), c(144, 133, 233, 255)),
    t(c(227, 73, 72, 255), c(230, 103, 103, 255)),
  ],
  [
    t(c(205, 226, 251, 255), c(13, 54, 107, 255)),
    t(c(158, 197, 244, 255), c(24, 79, 149, 255)),
    t(c(109, 167, 236, 255), c(37, 106, 191, 255)),
    t(c(57, 135, 229, 255), c(57, 135, 229, 255)),
    t(c(37, 106, 191, 255), c(109, 167, 236, 255)),
    t(c(24, 79, 149, 255), c(158, 197, 244, 255)),
    t(c(13, 54, 107, 255), c(205, 226, 251, 255)),
  ],
  new DivergingScale(t(c(42, 120, 214, 255), c(57, 135, 229, 255)), t(c(240, 239, 236, 255), c(56, 56, 53, 255)), t(c(227, 73, 72, 255), c(230, 103, 103, 255))),
  t(c(137, 135, 129, 255), c(137, 135, 129, 255)),
  new StatusScale(
    t(c(12, 163, 12, 255), c(12, 163, 12, 255)),
    t(c(250, 178, 25, 255), c(250, 178, 25, 255)),
    t(c(236, 131, 90, 255), c(236, 131, 90, 255)),
    t(c(208, 59, 59, 255), c(208, 59, 59, 255)),
  ),
);
DataPalette.default = defaultData;

export const photonTheme: AppTheme = {
  background: t(c(245, 246, 248, 255), c(12, 15, 19, 255)),
  surface: t(c(255, 255, 255, 255), c(20, 24, 30, 255)),
  surfaceSubtle: t(c(239, 241, 244, 255), c(28, 35, 43, 255)),
  surfaceHighlight: t(c(255, 255, 255, 140), c(255, 255, 255, 18)),
  border: t(c(226, 229, 234, 255), c(42, 50, 61, 255)),
  borderStrong: t(c(201, 206, 214, 255), c(76, 86, 100, 255)),
  textPrimary: t(c(23, 27, 33, 255), c(242, 244, 247, 255)),
  textSecondary: t(c(75, 85, 99, 255), c(199, 209, 220, 255)),
  textMuted: t(c(95, 107, 122, 255), c(172, 183, 197, 255)),
  textInverse: t(c(255, 255, 255, 255), c(23, 27, 33, 255)),
  focusRing: t(c(0, 80, 160, 255), c(129, 186, 243, 255)),
  linkColor: t(c(0, 80, 160, 255), c(129, 186, 243, 255)),
  scrim: t(c(11, 14, 18, 102), c(0, 0, 0, 143)),
  selection: t(c(0, 80, 160, 71), c(129, 186, 243, 71)),
  placeholder: t(c(95, 107, 122, 255), c(172, 183, 197, 255)),
  scrollbarThumb: t(c(95, 107, 122, 140), c(172, 183, 197, 140)),
  scrollbarTrack: t(c(239, 241, 244, 255), c(28, 35, 43, 255)),
  data: defaultData,
  disabledOpacity: 0.38,
  controls: controlsTheme({"height":{"comfortable":{"small":32,"medium":40,"large":48,"xLarge":56},"compact":{"small":26,"medium":32,"large":40,"xLarge":48}},"paddingX":{"comfortable":{"small":12,"medium":16,"large":20,"xLarge":24},"compact":{"small":8,"medium":12,"large":16,"xLarge":20}},"labelSize":{"comfortable":{"small":13,"medium":15,"large":16,"xLarge":17},"compact":{"small":11.5,"medium":13,"large":14.5,"xLarge":15.5}},"gap":{"small":6,"medium":8,"large":8,"xLarge":10},"icon":{"small":16,"medium":20,"large":20,"xLarge":24},"radius":{"small":10,"medium":10,"large":10,"xLarge":14}}),
  button: buttonTheme({"minWidth":64,"labelWeight":"semiBold","labelTracking":0.1,"outlineWidth":1,"linkPaddingX":6,"outlineColor":[[201,206,214,255],[76,86,100,255]],"quietFill":[[239,241,244,255],[28,35,43,255]]}),
  navigation: navigationTheme({"inactiveColor":[[95,107,122,255],[172,183,197,255]],"quietFill":[[239,241,244,255],[28,35,43,255]],"background":[[255,255,255,255],[20,24,30,255]],"tabHeight":48,"tabLabelSize":14,"tabLabelLineHeight":18,"tabLabelWeight":"semiBold","tabSelectedLabelWeight":"bold","tabLabelTracking":0.1,"indicatorThickness":3,"indicatorInset":16,"indicatorRadius":2,"pillRadius":999,"pillLabelGap":2,"barHeight":56,"barIconSize":24,"barPillWidth":56,"barPillHeight":26,"barLabelSize":11,"barLabelLineHeight":14,"barLabelWeight":"bold","barLabelTracking":0.1,"railWidth":80,"railPaddingY":12,"railGap":8,"railItemHeight":56,"railIconSize":20,"railPillWidth":52,"railPillHeight":30,"railSelectedLabelSize":12,"railSelectedLabelLineHeight":16,"railSelectedLabelWeight":"bold","railBorderWidth":1,"railBorderColor":[[226,229,234,255],[42,50,61,255]],"appBarHeight":56,"appBarPaddingX":4,"titleSize":20,"titleLineHeight":26,"titleWeight":"semiBold","titleColor":[[23,27,33,255],[242,244,247,255]],"titleInset":12,"titleGap":8,"scrolledElevation":2,"crumbGap":4,"crumbColor":[[75,85,99,255],[199,209,220,255]],"currentCrumbColor":[[23,27,33,255],[242,244,247,255]],"separatorSize":16,"separatorColor":[[201,206,214,255],[76,86,100,255]],"pageCellSize":30,"pageGap":3,"paginationGap":8,"pageRadius":6,"pageColor":[[75,85,99,255],[199,209,220,255]],"ellipsisColor":[[95,107,122,255],[172,183,197,255]],"dotSize":6,"currentDotWidth":18,"dotGap":4,"dotRadius":999,"dotColor":[[201,206,214,255],[76,86,100,255]]}),
  list: listTheme({"quietFill":[[239,241,244,255],[28,35,43,255]],"itemHeight":52,"twoLineItemHeight":68,"threeLineItemHeight":88,"itemPaddingX":16,"itemPaddingY":8,"itemGap":12,"textGap":2,"titleSize":15,"titleLineHeight":20,"titleWeight":"medium","titleColor":[[23,27,33,255],[242,244,247,255]],"subtitleColor":[[75,85,99,255],[199,209,220,255]],"dividerThickness":1,"dividerColor":[[226,229,234,255],[42,50,61,255]],"dividerInset":16,"headerHeight":48,"sectionPaddingX":12,"sectionPaddingBottom":12,"headerGap":8,"chevronSize":16,"chevronColor":[[75,85,99,255],[199,209,220,255]],"swipeActionWidth":96,"swipeActionGap":3,"swipeIconSize":20,"swipeSurface":[[255,255,255,255],[20,24,30,255]]}),
  table: tableTheme({"cellPaddingX":12,"cellPaddingY":8,"hoverFill":[[239,241,244,255],[28,35,43,255]],"columnGap":12,"rowMinHeight":44,"headerColor":[[75,85,99,255],[199,209,220,255]],"dataRowHeight":52,"selectionColumnWidth":44,"headerFill":[[239,241,244,255],[28,35,43,255]],"dataHeaderColor":[[95,107,122,255],[172,183,197,255]],"sortedHeaderColor":[[23,27,33,255],[242,244,247,255]],"headerUppercase":true,"headerIconGap":4,"sortIconSize":16,"borderWidth":1,"borderColor":[[226,229,234,255],[42,50,61,255]]}),
  badge: badgeTheme({"ringColor":[[255,255,255,255],[20,24,30,255]],"ringWidth":2,"dotSize":8,"countHeight":16,"countPaddingX":4,"countLabelSize":10,"countLabelLineHeight":12,"countLabelWeight":"bold","neutralFill":[[239,241,244,255],[28,35,43,255]],"neutralColor":[[75,85,99,255],[199,209,220,255]],"avatarSize":{"small":24,"medium":32,"large":40,"xLarge":56},"avatarLabelSize":{"small":10,"medium":13,"large":16,"xLarge":22},"avatarLabelWeight":"semiBold","avatarGlyphSize":{"small":16,"medium":20,"large":24,"xLarge":32},"avatarFill":[[239,241,244,255],[28,35,43,255]],"avatarGlyphColor":[[95,107,122,255],[172,183,197,255]],"wellSize":64,"wellFill":[[239,241,244,255],[28,35,43,255]],"wellIconSize":32,"wellIconColor":[[95,107,122,255],[172,183,197,255]],"emptyPaddingX":16,"emptyPaddingY":48,"wellGap":16,"bodyGap":6,"actionsGap":20,"actionSpacing":8,"emptyTitleSize":20,"emptyTitleLineHeight":26,"emptyTitleWeight":"semiBold","emptyTitleColor":[[23,27,33,255],[242,244,247,255]],"emptyBodyColor":[[75,85,99,255],[199,209,220,255]]}),
  calendar: calendarTheme({"cellSize":44,"weekdayHeight":28,"headerGap":4,"gap":8,"titleRole":"titleSmall","titleColor":[[23,27,33,255],[242,244,247,255]],"weekdayRole":"caption","weekdayColor":[[95,107,122,255],[172,183,197,255]],"dayRole":"bodyM","dayColor":[[23,27,33,255],[242,244,247,255]],"dayHoverFill":[[239,241,244,255],[28,35,43,255]],"todayRingWidth":1.5,"panelFill":[[255,255,255,255],[20,24,30,255]],"panelRadius":10,"panelBorderWidth":1,"panelBorderColor":[[226,229,234,255],[42,50,61,255]],"panelElevation":2,"panelPadding":12,"openerIconSize":20,"openerColor":[[75,85,99,255],[199,209,220,255]]}),
  markdown: markdownTheme({"heading1":"heading","heading2":"title","heading3":"titleSmall","heading4":"label","body":"bodyM","blockGap":14,"codeLineNumbers":false,"codeInverse":false,"headingSpace":10,"subheadingSpace":6,"headingColor":[[23,27,33,255],[242,244,247,255]],"bodyColor":[[75,85,99,255],[199,209,220,255]],"inlineCodeMaxSize":13.5,"inlineCodeColor":[[23,27,33,255],[242,244,247,255]],"quoteIndent":16,"quotePaddingY":2,"quoteRuleWidth":3,"quoteRuleColor":[[201,206,214,255],[76,86,100,255]],"listGap":8,"markerGap":10,"listIndent":20,"markerColor":[[95,107,122,255],[172,183,197,255]],"tableRadius":12,"tableBorderWidth":1,"tableBorderColor":[[226,229,234,255],[42,50,61,255]],"tableHeaderFill":[[239,241,244,255],[28,35,43,255]],"tableCellPaddingX":14,"tableHeaderPaddingY":10,"tableCellPaddingY":11}),
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
  code(kind: string): ColorToken {
    return codeTokenColor(this, kind);
  },
};

/** The transpiled shape of `PhotonTheme.Instance` references. */
export const PhotonTheme = { instance: photonTheme };
