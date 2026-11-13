/**
 * Mirrors of the C# theme slots of `docs/ELEMENT-TREE-PLAN.md` §3.10: the control ladder (`SizeLadder`,
 * `DensityLadder`, `ControlsTheme`), the Button's, the form controls' (`DensityPair`, the IconButton's,
 * the fields', the selection controls', the segmented controls' and the Chip's), the surfaces' (modal,
 * menu, notice, card and progress) and the families' (navigation, list, table, badge, calendar and the
 * rendered document's), every member optional. A member left null is the next step of the slot order.
 * Built from a config object, as eqc writes a record whose members are all `init`
 * (`new ButtonTheme({ minWidth })`), and from the bridge's JSON (`theme-bridge.ts`).
 */

import { hashesByValue } from '../utils/hash';
import { ColorToken, EdgeInsets, TypeStyle, type AppTheme } from './value-types';
import type { ColorValue } from './nodes';
import { SheetTheme } from './sheet-theme';
import type { FontWeightValue, ShapeScaleValue, TypeRoleValue } from './enums.generated';

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
  outlineWidth: number | null = null;
  outlineColor: ColorToken | null = null;
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

/** The modal surfaces' slot: Dialog, BottomSheet and Drawer (E6c, #1243). */
export class ModalTheme {
  dialogMaxWidth: number | null = null;
  dialogShape: ShapeScaleValue | null = null;
  dialogElevation: number | null = null;
  dialogPadding: EdgeInsets | null = null;
  dialogGap: number | null = null;
  dialogTextGap: number | null = null;
  dialogActionGap: number | null = null;
  dialogGutter: number | null = null;
  bottomSheetShape: ShapeScaleValue | null = null;
  bottomSheetElevation: number | null = null;
  bottomSheetPadding: EdgeInsets | null = null;
  bottomSheetGap: number | null = null;
  handleWidth: number | null = null;
  handleHeight: number | null = null;
  handleColor: ColorToken | null = null;
  drawerElevation: number | null = null;
  drawerPadding: EdgeInsets | null = null;

  constructor(config?: Partial<ModalTheme>) {
    if (config) Object.assign(this, config);
  }

  /** C# `ModalTheme.Merge`: `over`'s members laid over `under`'s (`mergeSlot`). */
  static merge(under: ModalTheme | null, over: ModalTheme | null): ModalTheme | null {
    return mergeSlot(under, over);
  }
}

/** The menus' slot: Popover, Menu and Tooltip (E6c, #1243). */
export class MenuTheme {
  panelShape: ShapeScaleValue | null = null;
  panelBorderWidth: number | null = null;
  panelElevation: number | null = null;
  popoverPadding: EdgeInsets | null = null;
  menuMinWidth: number | null = null;
  menuPadding: EdgeInsets | null = null;
  itemPadding: EdgeInsets | null = null;
  itemGap: number | null = null;
  tooltipShape: ShapeScaleValue | null = null;
  tooltipPadding: EdgeInsets | null = null;
  tooltipFill: ColorToken | null = null;
  tooltipText: ColorToken | null = null;

  constructor(config?: Partial<MenuTheme>) {
    if (config) Object.assign(this, config);
  }

  /** C# `MenuTheme.Merge`: `over`'s members laid over `under`'s (`mergeSlot`). */
  static merge(under: MenuTheme | null, over: MenuTheme | null): MenuTheme | null {
    return mergeSlot(under, over);
  }
}

/** The notices' slot: Toast, Banner and CookieConsent (E6c, #1243). */
export class NoticeTheme {
  toastFill: ColorToken | null = null;
  toastText: ColorToken | null = null;
  toastShape: ShapeScaleValue | null = null;
  toastElevation: number | null = null;
  toastPadding: EdgeInsets | null = null;
  toastMaxWidth: number | null = null;
  toastDot: number | null = null;
  toastGap: number | null = null;
  toastMargin: EdgeInsets | null = null;
  bannerShape: ShapeScaleValue | null = null;
  bannerPadding: EdgeInsets | null = null;
  bannerIconGap: number | null = null;
  bannerGap: number | null = null;
  bannerTextSize: number | null = null;
  bannerLineHeight: number | null = null;
  bannerTitleWeight: FontWeightValue | null = null;
  bannerBodyWeight: FontWeightValue | null = null;
  consentShape: ShapeScaleValue | null = null;
  consentElevation: number | null = null;
  consentBorderWidth: number | null = null;
  consentPadding: EdgeInsets | null = null;

  constructor(config?: Partial<NoticeTheme>) {
    if (config) Object.assign(this, config);
  }

  /** C# `NoticeTheme.Merge`: `over`'s members laid over `under`'s (`mergeSlot`). */
  static merge(under: NoticeTheme | null, over: NoticeTheme | null): NoticeTheme | null {
    return mergeSlot(under, over);
  }
}

/** The Card's slot (E6c, #1243). */
export class CardTheme {
  shape: ShapeScaleValue | null = null;
  borderWidth: number | null = null;
  elevation: number | null = null;
  filledColor: ColorToken | null = null;
  elevatedBorderColor: ColorToken | null = null;

  constructor(config?: Partial<CardTheme>) {
    if (config) Object.assign(this, config);
  }

  /** C# `CardTheme.Merge`: `over`'s members laid over `under`'s (`mergeSlot`). */
  static merge(under: CardTheme | null, over: CardTheme | null): CardTheme | null {
    return mergeSlot(under, over);
  }
}

/** The progress slot: ProgressBar, PullToRefresh and Skeleton (E6c, #1243). */
export class ProgressTheme {
  barHeight: number | null = null;
  prominentBarHeight: number | null = null;
  barShape: ShapeScaleValue | null = null;
  trackColor: ColorToken | null = null;
  refreshSpinner: number | null = null;
  skeletonLineHeight: number | null = null;
  skeletonShape: ShapeScaleValue | null = null;
  skeletonFill: ColorToken | null = null;
  skeletonHighlight: ColorToken | null = null;

  constructor(config?: Partial<ProgressTheme>) {
    if (config) Object.assign(this, config);
  }

  /** C# `ProgressTheme.Merge`: `over`'s members laid over `under`'s (`mergeSlot`). */
  static merge(under: ProgressTheme | null, over: ProgressTheme | null): ProgressTheme | null {
    return mergeSlot(under, over);
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

  /** C# `NavigationTheme.Merge`: `over`'s members laid over `under`'s (`mergeSlot`). */
  static merge(under: NavigationTheme | null | undefined, over: NavigationTheme | null | undefined): NavigationTheme | null {
    return mergeSlot(under, over);
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

  /** C# `ListTheme.Merge`: `over`'s members laid over `under`'s (`mergeSlot`). */
  static merge(under: ListTheme | null | undefined, over: ListTheme | null | undefined): ListTheme | null {
    return mergeSlot(under, over);
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

  /** C# `TableTheme.Merge`: `over`'s members laid over `under`'s (`mergeSlot`). */
  static merge(under: TableTheme | null | undefined, over: TableTheme | null | undefined): TableTheme | null {
    return mergeSlot(under, over);
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

  /** C# `BadgeTheme.Merge`: `over`'s members laid over `under`'s (`mergeSlot`). */
  static merge(under: BadgeTheme | null | undefined, over: BadgeTheme | null | undefined): BadgeTheme | null {
    return mergeSlot(under, over);
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

  /** C# `CalendarTheme.Merge`: `over`'s members laid over `under`'s (`mergeSlot`). */
  static merge(under: CalendarTheme | null | undefined, over: CalendarTheme | null | undefined): CalendarTheme | null {
    return mergeSlot(under, over);
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

  /** C# `MarkdownTheme.Merge`: `over`'s members laid over `under`'s (`mergeSlot`). */
  static merge(under: MarkdownTheme | null | undefined, over: MarkdownTheme | null | undefined): MarkdownTheme | null {
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
hashesByValue(ModalTheme);
hashesByValue(MenuTheme);
hashesByValue(NoticeTheme);
hashesByValue(CardTheme);
hashesByValue(ProgressTheme);
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
    outlineWidth: slotValue<number>(data.outlineWidth),
    outlineColor: slotToken(data.outlineColor),
    pressOpacity: slotValue<number>(data.pressOpacity),
    iconSize: slotValue<number>(data.iconSize),
    removeIconSize: slotValue<number>(data.removeIconSize),
  });
}

/** The ModalTheme the bridge wrote. */
export function modalTheme(data: SlotData): ModalTheme {
  return new ModalTheme({
    dialogMaxWidth: slotValue<number>(data.dialogMaxWidth),
    dialogShape: slotValue<ShapeScaleValue>(data.dialogShape),
    dialogElevation: slotValue<number>(data.dialogElevation),
    dialogPadding: slotInsets(data.dialogPadding),
    dialogGap: slotValue<number>(data.dialogGap),
    dialogTextGap: slotValue<number>(data.dialogTextGap),
    dialogActionGap: slotValue<number>(data.dialogActionGap),
    dialogGutter: slotValue<number>(data.dialogGutter),
    bottomSheetShape: slotValue<ShapeScaleValue>(data.bottomSheetShape),
    bottomSheetElevation: slotValue<number>(data.bottomSheetElevation),
    bottomSheetPadding: slotInsets(data.bottomSheetPadding),
    bottomSheetGap: slotValue<number>(data.bottomSheetGap),
    handleWidth: slotValue<number>(data.handleWidth),
    handleHeight: slotValue<number>(data.handleHeight),
    handleColor: slotToken(data.handleColor),
    drawerElevation: slotValue<number>(data.drawerElevation),
    drawerPadding: slotInsets(data.drawerPadding),
  });
}

/** The MenuTheme the bridge wrote. */
export function menuTheme(data: SlotData): MenuTheme {
  return new MenuTheme({
    panelShape: slotValue<ShapeScaleValue>(data.panelShape),
    panelBorderWidth: slotValue<number>(data.panelBorderWidth),
    panelElevation: slotValue<number>(data.panelElevation),
    popoverPadding: slotInsets(data.popoverPadding),
    menuMinWidth: slotValue<number>(data.menuMinWidth),
    menuPadding: slotInsets(data.menuPadding),
    itemPadding: slotInsets(data.itemPadding),
    itemGap: slotValue<number>(data.itemGap),
    tooltipShape: slotValue<ShapeScaleValue>(data.tooltipShape),
    tooltipPadding: slotInsets(data.tooltipPadding),
    tooltipFill: slotToken(data.tooltipFill),
    tooltipText: slotToken(data.tooltipText),
  });
}

/** The NoticeTheme the bridge wrote. */
export function noticeTheme(data: SlotData): NoticeTheme {
  return new NoticeTheme({
    toastFill: slotToken(data.toastFill),
    toastText: slotToken(data.toastText),
    toastShape: slotValue<ShapeScaleValue>(data.toastShape),
    toastElevation: slotValue<number>(data.toastElevation),
    toastPadding: slotInsets(data.toastPadding),
    toastMaxWidth: slotValue<number>(data.toastMaxWidth),
    toastDot: slotValue<number>(data.toastDot),
    toastGap: slotValue<number>(data.toastGap),
    toastMargin: slotInsets(data.toastMargin),
    bannerShape: slotValue<ShapeScaleValue>(data.bannerShape),
    bannerPadding: slotInsets(data.bannerPadding),
    bannerIconGap: slotValue<number>(data.bannerIconGap),
    bannerGap: slotValue<number>(data.bannerGap),
    bannerTextSize: slotValue<number>(data.bannerTextSize),
    bannerLineHeight: slotValue<number>(data.bannerLineHeight),
    bannerTitleWeight: slotValue<FontWeightValue>(data.bannerTitleWeight),
    bannerBodyWeight: slotValue<FontWeightValue>(data.bannerBodyWeight),
    consentShape: slotValue<ShapeScaleValue>(data.consentShape),
    consentElevation: slotValue<number>(data.consentElevation),
    consentBorderWidth: slotValue<number>(data.consentBorderWidth),
    consentPadding: slotInsets(data.consentPadding),
  });
}

/** The CardTheme the bridge wrote. */
export function cardTheme(data: SlotData): CardTheme {
  return new CardTheme({
    shape: slotValue<ShapeScaleValue>(data.shape),
    borderWidth: slotValue<number>(data.borderWidth),
    elevation: slotValue<number>(data.elevation),
    filledColor: slotToken(data.filledColor),
    elevatedBorderColor: slotToken(data.elevatedBorderColor),
  });
}

/** The ProgressTheme the bridge wrote. */
export function progressTheme(data: SlotData): ProgressTheme {
  return new ProgressTheme({
    barHeight: slotValue<number>(data.barHeight),
    prominentBarHeight: slotValue<number>(data.prominentBarHeight),
    barShape: slotValue<ShapeScaleValue>(data.barShape),
    trackColor: slotToken(data.trackColor),
    refreshSpinner: slotValue<number>(data.refreshSpinner),
    skeletonLineHeight: slotValue<number>(data.skeletonLineHeight),
    skeletonShape: slotValue<ShapeScaleValue>(data.skeletonShape),
    skeletonFill: slotToken(data.skeletonFill),
    skeletonHighlight: slotToken(data.skeletonHighlight),
  });
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
  iconButton: iconButtonTheme,
  input: inputTheme,
  selectionControls: selectionControlsTheme,
  segmented: segmentedTheme,
  chip: chipTheme,
  modal: modalTheme,
  menu: menuTheme,
  notice: noticeTheme,
  progress: progressTheme,
  card: cardTheme,
  navigation: navigationTheme,
  list: listTheme,
  table: tableTheme,
  badge: badgeTheme,
  calendar: calendarTheme,
  markdown: markdownTheme,
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
    modal: ModalTheme | null = null,
    menu: MenuTheme | null = null,
    notice: NoticeTheme | null = null,
    progress: ProgressTheme | null = null,
    card: CardTheme | null = null,
    navigation: NavigationTheme | null = null,
    list: ListTheme | null = null,
    table: TableTheme | null = null,
    badge: BadgeTheme | null = null,
    calendar: CalendarTheme | null = null,
    markdown: MarkdownTheme | null = null,
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
      ['modal', modal],
      ['menu', menu],
      ['notice', notice],
      ['progress', progress],
      ['card', card],
      ['navigation', navigation],
      ['list', list],
      ['table', table],
      ['badge', badge],
      ['calendar', calendar],
      ['markdown', markdown],
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
