/**
 * Mirrors of the C# theme slots of `docs/ELEMENT-TREE-PLAN.md` §3.10: the control ladder (`SizeLadder`,
 * `DensityLadder`, `ControlsTheme`), the Button's, and the families' (navigation, list, table, badge,
 * calendar and the rendered document's), every member optional. A member left
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

/** The navigation family's slot: Tabs, BottomNavigation, NavigationRail, AppBar, Breadcrumb, Pagination and PageIndicator. */
export class NavigationTheme {
  inactiveColor: ColorToken | null = null;
  quietFill: ColorToken | null = null;
  background: ColorToken | null = null;
  tabHeight: number | null = null;
  tabLabelSize: number | null = null;
  tabLabelLineHeight: number | null = null;
  tabLabelWeight: string | null = null;
  tabSelectedLabelWeight: string | null = null;
  tabLabelTracking: number | null = null;
  indicatorThickness: number | null = null;
  indicatorInset: number | null = null;
  indicatorRadius: number | null = null;
  pillRadius: number | null = null;
  pillPressOpacity: number | null = null;
  pillLabelGap: number | null = null;
  barHeight: number | null = null;
  barIconSize: number | null = null;
  barPillWidth: number | null = null;
  barPillHeight: number | null = null;
  barLabelSize: number | null = null;
  barLabelLineHeight: number | null = null;
  barLabelWeight: string | null = null;
  barLabelTracking: number | null = null;
  railWidth: number | null = null;
  railPaddingY: number | null = null;
  railGap: number | null = null;
  railItemHeight: number | null = null;
  railIconSize: number | null = null;
  railPillWidth: number | null = null;
  railPillHeight: number | null = null;
  railSelectedLabelSize: number | null = null;
  railSelectedLabelLineHeight: number | null = null;
  railSelectedLabelWeight: string | null = null;
  railBorderWidth: number | null = null;
  railBorderColor: ColorToken | null = null;
  appBarHeight: number | null = null;
  appBarPaddingX: number | null = null;
  titleSize: number | null = null;
  titleLineHeight: number | null = null;
  titleWeight: string | null = null;
  titleColor: ColorToken | null = null;
  titleInset: number | null = null;
  titleGap: number | null = null;
  scrolledElevation: number | null = null;
  crumbGap: number | null = null;
  crumbColor: ColorToken | null = null;
  currentCrumbColor: ColorToken | null = null;
  separatorSize: number | null = null;
  separatorColor: ColorToken | null = null;
  pageCellSize: number | null = null;
  pageGap: number | null = null;
  paginationGap: number | null = null;
  pageRadius: number | null = null;
  pageColor: ColorToken | null = null;
  ellipsisColor: ColorToken | null = null;
  dotSize: number | null = null;
  currentDotWidth: number | null = null;
  dotGap: number | null = null;
  dotRadius: number | null = null;
  dotColor: ColorToken | null = null;

  constructor(config?: Partial<NavigationTheme>) {
    if (config) Object.assign(this, config);
  }
}

/** The list family's slot: ListItem, List, Divider, Accordion and SwipeableRow. */
export class ListTheme {
  quietFill: ColorToken | null = null;
  itemHeight: number | null = null;
  twoLineItemHeight: number | null = null;
  threeLineItemHeight: number | null = null;
  itemPaddingX: number | null = null;
  itemPaddingY: number | null = null;
  itemGap: number | null = null;
  textGap: number | null = null;
  titleSize: number | null = null;
  titleLineHeight: number | null = null;
  titleWeight: string | null = null;
  titleColor: ColorToken | null = null;
  subtitleColor: ColorToken | null = null;
  dividerThickness: number | null = null;
  dividerColor: ColorToken | null = null;
  dividerInset: number | null = null;
  headerHeight: number | null = null;
  sectionPaddingX: number | null = null;
  sectionPaddingBottom: number | null = null;
  headerGap: number | null = null;
  chevronSize: number | null = null;
  chevronColor: ColorToken | null = null;
  swipeActionWidth: number | null = null;
  swipeActionGap: number | null = null;
  swipeIconSize: number | null = null;
  swipeSurface: ColorToken | null = null;

  constructor(config?: Partial<ListTheme>) {
    if (config) Object.assign(this, config);
  }
}

/** The table family's slot: Table and DataTable. */
export class TableTheme {
  cellPaddingX: number | null = null;
  cellPaddingY: number | null = null;
  hoverFill: ColorToken | null = null;
  columnGap: number | null = null;
  rowMinHeight: number | null = null;
  tableHeaderColor: ColorToken | null = null;
  dataRowHeight: number | null = null;
  selectionColumnWidth: number | null = null;
  headerFill: ColorToken | null = null;
  dataHeaderColor: ColorToken | null = null;
  sortedHeaderColor: ColorToken | null = null;
  headerUppercase: boolean | null = null;
  headerIconGap: number | null = null;
  sortIconSize: number | null = null;
  borderWidth: number | null = null;
  borderColor: ColorToken | null = null;

  constructor(config?: Partial<TableTheme>) {
    if (config) Object.assign(this, config);
  }
}

/** The badge family's slot: Badge, Avatar and EmptyState. */
export class BadgeTheme {
  ringColor: ColorToken | null = null;
  ringWidth: number | null = null;
  dotSize: number | null = null;
  countHeight: number | null = null;
  countPaddingX: number | null = null;
  countLabelSize: number | null = null;
  countLabelLineHeight: number | null = null;
  countLabelWeight: string | null = null;
  neutralFill: ColorToken | null = null;
  neutralColor: ColorToken | null = null;
  avatarSize: SizeLadder | null = null;
  avatarLabelSize: SizeLadder | null = null;
  avatarLabelWeight: string | null = null;
  avatarGlyphSize: SizeLadder | null = null;
  avatarFill: ColorToken | null = null;
  avatarGlyphColor: ColorToken | null = null;
  offlineColor: ColorToken | null = null;
  wellSize: number | null = null;
  wellFill: ColorToken | null = null;
  wellIconSize: number | null = null;
  wellIconColor: ColorToken | null = null;
  emptyPaddingX: number | null = null;
  emptyPaddingY: number | null = null;
  wellGap: number | null = null;
  bodyGap: number | null = null;
  actionsGap: number | null = null;
  actionSpacing: number | null = null;
  emptyTitleSize: number | null = null;
  emptyTitleLineHeight: number | null = null;
  emptyTitleWeight: string | null = null;
  emptyTitleColor: ColorToken | null = null;
  emptyBodyColor: ColorToken | null = null;

  constructor(config?: Partial<BadgeTheme>) {
    if (config) Object.assign(this, config);
  }
}

/** The calendar family's slot: Calendar and DatePicker. */
export class CalendarTheme {
  cellSize: number | null = null;
  weekdayHeight: number | null = null;
  headerGap: number | null = null;
  gap: number | null = null;
  titleRole: TypeRoleValue | null = null;
  titleColor: ColorToken | null = null;
  weekdayRole: TypeRoleValue | null = null;
  weekdayColor: ColorToken | null = null;
  dayRole: TypeRoleValue | null = null;
  dayColor: ColorToken | null = null;
  dayHoverFill: ColorToken | null = null;
  todayRingWidth: number | null = null;
  panelFill: ColorToken | null = null;
  panelRadius: number | null = null;
  panelBorderWidth: number | null = null;
  panelBorderColor: ColorToken | null = null;
  panelElevation: number | null = null;
  panelPadding: number | null = null;
  openerIconSize: number | null = null;
  openerColor: ColorToken | null = null;

  constructor(config?: Partial<CalendarTheme>) {
    if (config) Object.assign(this, config);
  }
}

/** The rendered document's slot, which a Markdown's own `style` comes ahead of. */
export class MarkdownTheme {
  heading1: TypeRoleValue | null = null;
  heading2: TypeRoleValue | null = null;
  heading3: TypeRoleValue | null = null;
  heading4: TypeRoleValue | null = null;
  body: TypeRoleValue | null = null;
  blockGap: number | null = null;
  codeLineNumbers: boolean | null = null;
  codeInverse: boolean | null = null;
  headingSpace: number | null = null;
  subheadingSpace: number | null = null;
  headingColor: ColorToken | null = null;
  bodyColor: ColorToken | null = null;
  inlineCodeMaxSize: number | null = null;
  inlineCodeColor: ColorToken | null = null;
  quoteIndent: number | null = null;
  quotePaddingY: number | null = null;
  quoteRuleWidth: number | null = null;
  quoteRuleColor: ColorToken | null = null;
  listGap: number | null = null;
  markerGap: number | null = null;
  listIndent: number | null = null;
  markerColor: ColorToken | null = null;
  tableRadius: number | null = null;
  tableBorderWidth: number | null = null;
  tableBorderColor: ColorToken | null = null;
  tableHeaderFill: ColorToken | null = null;
  tableCellPaddingX: number | null = null;
  tableHeaderPaddingY: number | null = null;
  tableCellPaddingY: number | null = null;

  constructor(config?: Partial<MarkdownTheme>) {
    if (config) Object.assign(this, config);
  }
}

// Records: each hashes by its members, as `equals` compares it (`ValueTwinHashTests` holds this to the C#).
hashesByValue(SizeLadder);
hashesByValue(DensityLadder);
hashesByValue(ControlsTheme);
hashesByValue(ButtonTheme);
hashesByValue(NavigationTheme);
hashesByValue(ListTheme);
hashesByValue(TableTheme);
hashesByValue(BadgeTheme);
hashesByValue(CalendarTheme);
hashesByValue(MarkdownTheme);

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
 * A family's slot the bridge wrote, read member by member into its mirror. The wire has one form per
 * kind, so a member's shape says how to read it: a light/dark tuple is a colour, an object is a size
 * ladder (the only record these slots nest), and anything else (a number, a flag, an enum's name)
 * crosses as itself. A name the mirror does not declare is skipped, so a page whose runtime is older
 * than its server's theme keeps every look it can read: `theme-bridge.spec.ts` holds the mirrors to
 * every member the server writes, where a refusal would fail the test and not a page.
 */
function familySlot<T extends object>(slot: T, data: SlotData): T {
  const members = slot as unknown as Record<string, unknown>;
  for (const [name, value] of Object.entries(data)) {
    if (!(name in members)) continue;
    members[name] = Array.isArray(value)
      ? slotToken(value)
      : value !== null && typeof value === 'object'
        ? slotSizeLadder(value)
        : slotValue(value);
  }
  return slot;
}

/** The navigation family's slot the bridge wrote. */
export const navigationTheme = (data: SlotData): NavigationTheme => familySlot(new NavigationTheme(), data);
/** The list family's slot the bridge wrote. */
export const listTheme = (data: SlotData): ListTheme => familySlot(new ListTheme(), data);
/** The table family's slot the bridge wrote. */
export const tableTheme = (data: SlotData): TableTheme => familySlot(new TableTheme(), data);
/** The badge family's slot the bridge wrote. */
export const badgeTheme = (data: SlotData): BadgeTheme => familySlot(new BadgeTheme(), data);
/** The calendar family's slot the bridge wrote. */
export const calendarTheme = (data: SlotData): CalendarTheme => familySlot(new CalendarTheme(), data);
/** The rendered document's slot the bridge wrote. */
export const markdownTheme = (data: SlotData): MarkdownTheme => familySlot(new MarkdownTheme(), data);

/**
 * Every slot the browser's theme carries, by the name the bridge writes it under and the builder that
 * reads it. `materializeTheme` walks this, so a family adds its slot here and nowhere in the bridge.
 */
export const slotBuilders: Record<string, (data: SlotData) => unknown> = {
  sheet: sheetTheme,
  controls: controlsTheme,
  button: buttonTheme,
  navigation: navigationTheme,
  list: listTheme,
  table: tableTheme,
  badge: badgeTheme,
  calendar: calendarTheme,
  markdown: markdownTheme,
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
    navigation: NavigationTheme | null = null,
    list: ListTheme | null = null,
    table: TableTheme | null = null,
    badge: BadgeTheme | null = null,
    calendar: CalendarTheme | null = null,
    markdown: MarkdownTheme | null = null,
  ): AppTheme {
    // A map, so a slot's name is never confused with a member every object inherits.
    const slots = new Map<PropertyKey, unknown>([
      ['controls', controls],
      ['button', button],
      ['navigation', navigation],
      ['list', list],
      ['table', table],
      ['badge', badge],
      ['calendar', calendar],
      ['markdown', markdown],
    ]);
    return new Proxy(theme, {
      get(target, key) {
        if (slots.has(key)) return slots.get(key) ?? Reflect.get(target, key, target) ?? null;
        const value = Reflect.get(target, key, target);
        return typeof value === 'function' ? value.bind(target) : value;
      },
    });
  }
}
