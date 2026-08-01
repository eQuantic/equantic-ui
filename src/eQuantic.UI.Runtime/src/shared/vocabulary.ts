/**
 * Runtime classes for the shared vocabulary's NODES — what transpiled component code instantiates
 * (`new Box(new BoxStyle({ … }), content)`, `new Row(gap, { height: SizeValue.fill })`,
 * `content.add(label)`). Instances ARE the `nodes.ts` shapes (`nodeKind` discriminators, camelCase
 * fields), so `lowerVisualNode` consumes them as-is — and every node carries `render(): HtmlNode`
 * that self-lowers with the ambient Photon context, which is what slots an abstract tree into the
 * existing web component pipeline (a page's `build()` can return one directly; the framework calls
 * `render()` like on any other component; the reconciler never learns about abstract nodes).
 */

import type { AnchorPlacementValue, ComponentChild } from './nodes';
import type { HtmlNode } from '../core/types';
import { iconPaths } from './icons.generated';
import type {
  ColorTokenValue,
  CrossAlignValue,
  EdgeInsetsValue,
  MainAlignValue,
  TypeStyleValue,
} from './nodes';
import { lowerVisualNode } from './lowering';
import { ambientLoweringContext } from './photon-context';
import { CornerRadii, EdgeInsets, SizeValue } from './value-types';

/** Base of every abstract node: the wire discriminator + self-lowering into the web pipeline. */
export abstract class VisualNode {
  abstract readonly nodeKind: string;
  key?: string | null;

  /** Lowers this subtree with the ambient Photon context — the web pipeline's `Component.render()` seam. */
  render(): HtmlNode {
    return lowerVisualNode(this as never, ambientLoweringContext());
  }

  /** Parity with the web `Component` contract (`getVirtualNode`), so hosts treat nodes uniformly. */
  getVirtualNode(): HtmlNode {
    return this.render();
  }
}

/**
 * Anything a child slot accepts: a vocabulary node, or a shared COMPONENT instance (in C#,
 * `UiComponent : VisualNode` — on the runtime components live in their own class hierarchy, so the
 * union states the same truth structurally; the lowering expands components via `build`).
 */
export type VisualChild = VisualNode | ComponentChild;

/** Mirror of the C# `StyleDiff` record (spec S5): a partial style applied over the base while
 * an interaction state is active — only the set members override. */
export class StyleDiff {
  background?: ColorTokenValue | null;
  borderColor?: ColorTokenValue | null;
  borderWidth?: number | null;
  elevation?: number | null;
  opacity?: number | null;

  constructor(config?: {
    background?: ColorTokenValue | null;
    borderColor?: ColorTokenValue | null;
    borderWidth?: number | null;
    elevation?: number | null;
    opacity?: number | null;
  }) {
    if (config) Object.assign(this, config);
  }
}

interface BoxStyleConfig {
  width?: SizeValue | number;
  height?: SizeValue | number;
  minWidth?: number;
  minHeight?: number;
  maxWidth?: number;
  maxHeight?: number;
  padding?: EdgeInsetsValue;
  background?: ColorTokenValue | null;
  cornerRadius?: CornerRadii;
  borderWidth?: number;
  borderColor?: ColorTokenValue;
  elevation?: number;
  clip?: boolean;
  gradient?: LinearGradient | null;
  pattern?: GridPattern | null;
  opacity?: number | null;
  transform?: unknown;
  aspectRatio?: number;
  hover?: StyleDiff | null;
  focus?: StyleDiff | null;
}

/** Mirror of the C# `BoxStyle` record — constructed from the transpiled initializer config object.
 * Raw numbers normalize to fixed sizes (C#'s implicit float→SizeValue conversion). */
export class BoxStyle {
  width?: SizeValue;
  height?: SizeValue;
  minWidth = 0;
  minHeight = 0;
  maxWidth = 0;
  maxHeight = 0;
  padding: EdgeInsetsValue = new EdgeInsets();
  background?: ColorTokenValue | null;
  cornerRadius: CornerRadii = new CornerRadii();
  borderWidth = 0;
  borderColor?: ColorTokenValue;
  elevation = 0;
  clip = false;
  gradient: LinearGradient | null = null;
  /** The repeating hairline grid backdrop (null = none). */
  pattern: GridPattern | null = null;
  /** Spec S1 group opacity (null = opaque). */
  opacity?: number | null;
  /** Spec S1 static transform (the Transform2D wire shape). */
  transform?: unknown;
  /** Spec S1 aspect-ratio constraint (0 = none). */
  aspectRatio = 0;
  /** Spec S5 hover/focus diffs. */
  hover?: StyleDiff | null;
  focus?: StyleDiff | null;

  constructor(config?: BoxStyleConfig) {
    if (!config) return;
    const { width, height, ...rest } = config;
    Object.assign(this, rest);
    this.width = SizeValue.from(width);
    this.height = SizeValue.from(height);
  }
}

export class Box extends VisualNode {
  readonly nodeKind = 'box';
  style: BoxStyle;
  child: VisualChild | null;

  constructor(style: BoxStyle = new BoxStyle(), child: VisualChild | null = null) {
    super();
    this.style = style;
    this.child = child;
  }
}

interface FlexConfig {
  main?: MainAlignValue;
  cross?: CrossAlignValue;
  wrap?: boolean;
  runGap?: number | null;
  padding?: EdgeInsetsValue;
  width?: SizeValue | number;
  height?: SizeValue | number;
  background?: ColorTokenValue | null;
  cornerRadius?: CornerRadii;
}

abstract class FlexNode extends VisualNode {
  gap: number;
  /** Spec S3 flow wrapping (children keep natural size; lines stack with runGap). */
  wrap = false;
  runGap?: number | null;
  main: MainAlignValue = 'start';
  /** Per-subclass default (Row=center, Column=stretch — spec A2), set by the BASE ctor: a derived
   * field initializer would run AFTER super() and silently overwrite the config's explicit value
   * (the ES2022 class-field order) — the exact hydration bug the e2e identity test caught. */
  cross: CrossAlignValue;
  padding: EdgeInsetsValue = new EdgeInsets();
  width?: SizeValue;
  height?: SizeValue;
  background?: ColorTokenValue | null;
  cornerRadius: CornerRadii = new CornerRadii();
  children: VisualChild[] = [];

  protected constructor(gap: number, defaultCross: CrossAlignValue, config?: FlexConfig) {
    super();
    this.gap = gap;
    this.cross = defaultCross;
    if (!config) return;
    const { width, height, ...rest } = config;
    Object.assign(this, rest);
    if (width !== undefined) this.width = SizeValue.from(width);
    if (height !== undefined) this.height = SizeValue.from(height);
  }

  add(child: VisualChild): void {
    this.children.push(child);
  }
}

/** Mirror of the C# `Anchored` (wave 3): floating panel positioned relative to its anchor. */
export class Anchored extends VisualNode {
  readonly nodeKind = 'anchored';
  anchor: VisualChild;
  panel: VisualChild;
  // Transpiled C# enums arrive as camelCase STRINGS — the union documents the valid set.
  placement: AnchorPlacementValue | string = 'bottomStart';
  gap = 4;
  open = false;
  onDismiss?: (() => void) | null;
  matchAnchorWidth = false;
  openOnHover = false;

  constructor(
    anchor: VisualChild,
    panel: VisualChild,
    config?: {
      placement?: AnchorPlacementValue | string;
      gap?: number;
      open?: boolean;
      onDismiss?: (() => void) | null;
      matchAnchorWidth?: boolean;
      openOnHover?: boolean;
      key?: string | null;
    },
  ) {
    super();
    this.anchor = anchor;
    this.panel = panel;
    if (config) Object.assign(this, config);
  }
}

/** Mirror of the C# `Sticky` (spec S7): scroll-anchored chrome. */
export class Sticky extends VisualNode {
  readonly nodeKind = 'sticky';

  constructor(
    readonly child: VisualChild,
    readonly offset = 0,
  ) {
    super();
  }
}

/** Mirror of the C# `AdaptiveNode` (spec S6): up to three size-class variants. */
export class AdaptiveNode extends VisualNode {
  readonly nodeKind = 'adaptive';

  constructor(
    readonly compact: VisualNode,
    readonly medium: VisualNode | null = null,
    readonly expanded: VisualNode | null = null,
  ) {
    super();
  }
}

/** Mirror of the C# `GridTrack` statics. */
export class GridTrack {
  constructor(
    readonly kind: 'fixed' | 'fill' | 'hug',
    readonly value: number,
  ) {}

  static fixed(dp: number): GridTrack {
    return new GridTrack('fixed', dp);
  }
  static flex(weight = 1): GridTrack {
    return new GridTrack('fill', weight);
  }
  static get auto(): GridTrack {
    return new GridTrack('hug', 0);
  }
  static repeat(count: number, track: GridTrack): GridTrack[] {
    return Array.from({ length: count }, () => track);
  }
}

interface GridConfig {
  rowGap?: number | null;
  padding?: EdgeInsetsValue;
  width?: SizeValue | number;
  height?: SizeValue | number;
}

/** Mirror of the C# `Grid` (spec S4). */
export class Grid extends VisualNode {
  readonly nodeKind = 'grid';
  columns: GridTrack[];
  gap: number;
  rowGap?: number | null;
  padding: EdgeInsetsValue = new EdgeInsets();
  width?: SizeValue;
  height?: SizeValue;
  children: VisualChild[] = [];

  constructor(columns: GridTrack[], gap = 0, rowGap: number | null = null, config?: GridConfig) {
    super();
    this.columns = columns;
    this.gap = gap;
    this.rowGap = rowGap;
    if (!config) return;
    const { width, height, ...rest } = config;
    Object.assign(this, rest);
    if (width !== undefined) this.width = SizeValue.from(width);
    if (height !== undefined) this.height = SizeValue.from(height);
  }

  add(child: VisualChild): void {
    this.children.push(child);
  }
}

export class Row extends FlexNode {
  readonly nodeKind = 'row';

  constructor(gap = 0, config?: FlexConfig) {
    super(gap, 'center', config);
  }
}

export class Column extends FlexNode {
  readonly nodeKind = 'column';

  constructor(gap = 0, config?: FlexConfig) {
    super(gap, 'stretch', config);
  }
}

interface TextConfig {
  styleOverride?: TypeStyleValue | null;
  key?: string | null;
}

export class Text extends VisualNode {
  readonly nodeKind = 'text';
  content: string;
  role: string;
  color: ColorTokenValue | null;
  maxLines: number;
  styleOverride: TypeStyleValue | null = null;

  constructor(
    content: string,
    role = 'bodyL',
    color: ColorTokenValue | null = null,
    maxLines = 0,
    config?: TextConfig,
  ) {
    super();
    this.content = content;
    this.role = role;
    this.color = color;
    this.maxLines = maxLines;
    if (config) Object.assign(this, config);
  }
}

interface PressableConfig {
  disabled?: boolean;
  label?: string | null;
  key?: string | null;
  pressedBackground?: ColorTokenValue | null;
}

export class Pressable extends VisualNode {
  readonly nodeKind = 'pressable';
  child: VisualChild;
  onPressed: (() => void) | null;
  disabled = false;
  label: string | null = null;
  pressedBackground: ColorTokenValue | null = null;

  constructor(child: VisualChild, onPressed: (() => void) | null = null, config?: PressableConfig) {
    super();
    this.child = child;
    this.onPressed = onPressed;
    if (config) Object.assign(this, config);
  }
}

/** Mirror of the C# `Overlay` (Phase C): the viewport layer above the page. */
export class Overlay extends VisualNode {
  readonly nodeKind = 'overlay';
  child: VisualChild;
  /** False = non-modal layer (toasts): pointer passes through except the layer's pressables. */
  modal = true;

  constructor(child: VisualChild, config?: { modal?: boolean }) {
    super();
    this.child = child;
    if (config) Object.assign(this, config);
  }
}

/** Spec §06 enter motion: the wrapped subtree animates in on first appearance ('fade' | 'slideUp').
 * Layout-transparent — the web lowering rides the animation class on the child's own root. */
export class Presence extends VisualNode {
  readonly nodeKind = 'presence';
  child: VisualChild;
  enter: string;

  constructor(child: VisualChild, enter = 'fade') {
    super();
    this.child = child;
    this.enter = enter;
  }
}

/** Navigation semantics: the child becomes a link to href — the child owns all visuals. */
export class Link extends VisualNode {
  readonly nodeKind = 'link';
  href: string;
  child: VisualChild;
  label: string | null = null;

  constructor(href: string, child: VisualChild, config?: { label?: string | null }) {
    super();
    this.href = href;
    this.child = child;
    if (config) Object.assign(this, config);
  }
}

/** Gestures v2: vertical drag-to-dismiss (the sheet contract) — release past the threshold fires
 * onDismiss; short of it, the pointer-capture controller glides the subtree back. */
export class DragDismiss extends VisualNode {
  readonly nodeKind = 'dragDismiss';
  static readonly thresholdDp = 96;
  child: VisualChild;
  onDismiss: (() => void) | null;

  constructor(child: VisualChild, onDismiss: (() => void) | null = null) {
    super();
    this.child = child;
    this.onDismiss = onDismiss;
  }
}

interface TextEntryConfig {
  placeholder?: string | null;
  onSubmit?: (() => void) | null;
  onFocusChanged?: ((focused: boolean) => void) | null;
  disabled?: boolean;
  obscure?: boolean;
  role?: string;
}

/** Mirror of the C# `TextEntry` (spec B9/B10): single-line entry; the browser owns caret/IME. */
export class TextEntry extends VisualNode {
  readonly nodeKind = 'textEntry';
  value: string;
  onChanged: ((value: string) => void) | null;
  placeholder: string | null = null;
  onSubmit: (() => void) | null = null;
  onFocusChanged: ((focused: boolean) => void) | null = null;
  disabled = false;
  obscure = false;
  role = 'bodyL';

  constructor(
    value: string,
    onChanged: ((value: string) => void) | null = null,
    config?: TextEntryConfig,
  ) {
    super();
    this.value = value;
    this.onChanged = onChanged;
    if (config) Object.assign(this, config);
  }
}

/** Mirror of the C# `LinearGradient` record: two token stops on a straight axis (engine fence). */
export class LinearGradient {
  from: ColorTokenValue;
  to: ColorTokenValue;
  direction: string;

  constructor(from: ColorTokenValue, to: ColorTokenValue, direction = 'toRight') {
    this.from = from;
    this.to = to;
    this.direction = direction;
  }
}

/** Mirror of the C# `GridPattern`: repeating hairline grid (square cell, token line color). */
export class GridPattern {
  cell: number;
  color: ColorTokenValue;
  lineWidth: number;

  constructor(cell: number, color: ColorTokenValue, lineWidth = 1) {
    this.cell = cell;
    this.color = color;
    this.lineWidth = lineWidth;
  }
}

interface LoopMotionConfig {
  hideAtRest?: boolean;
}

/** Mirror of the C# `LoopMotion` (spec §06): continuous transform-only loop around one child. */
export class LoopMotion extends VisualNode {
  readonly nodeKind = 'loopMotion';
  child: VisualChild;
  effect: string;
  fromX: number;
  toX: number;
  durationMs: number;
  hideAtRest = false;

  constructor(
    child: VisualChild,
    effect: string,
    fromX: number,
    toX: number,
    durationMs: number,
    config?: LoopMotionConfig,
  ) {
    super();
    this.child = child;
    this.effect = effect;
    this.fromX = fromX;
    this.toX = toX;
    this.durationMs = durationMs;
    if (config) Object.assign(this, config);
  }
}

export class ScrollView extends VisualNode {
  readonly nodeKind = 'scrollView';
  child: VisualChild;
  axis: string;
  width?: SizeValue;
  height?: SizeValue;
  offset: number;

  constructor(
    child: VisualChild,
    axis = 'vertical',
    config?: { width?: SizeValue | number; height?: SizeValue | number; offset?: number },
  ) {
    super();
    this.child = child;
    this.axis = axis;
    this.offset = config?.offset ?? 0;
    if (config?.width !== undefined) this.width = SizeValue.from(config.width);
    if (config?.height !== undefined) this.height = SizeValue.from(config.height);
  }
}

export class Image extends VisualNode {
  readonly nodeKind = 'image';
  source: string;
  width: number;
  height: number;
  fit: string;
  alt: string;
  cornerRadius?: CornerRadii;

  constructor(
    source: string,
    width: number,
    height: number,
    fit = 'cover',
    alt = '',
    config?: { cornerRadius?: CornerRadii },
  ) {
    super();
    this.source = source;
    this.width = width;
    this.height = height;
    this.fit = fit;
    this.alt = alt;
    if (config) Object.assign(this, config);
  }
}

/** Mirror of the C# `IconGlyph` record: target-neutral glyph data (write-once icon packs). */
export class IconGlyph {
  name: string;
  path: string;
  style: string;
  viewBox: string;
  strokeWidth: number;

  constructor(name: string, path: string, style = 'fill', viewBox = '0 0 24 24', strokeWidth = 2) {
    this.name = name;
    this.path = path;
    this.style = style;
    this.viewBox = viewBox;
    this.strokeWidth = strokeWidth;
  }
}

export class Icon extends VisualNode {
  readonly nodeKind = 'icon';
  glyph: IconGlyph;
  size: number;
  color: ColorTokenValue | null;
  label: string | null;

  constructor(
    glyph: string | IconGlyph,
    size = 24,
    color: ColorTokenValue | null = null,
    label: string | null = null,
  ) {
    super();
    if (size !== 16 && size !== 20 && size !== 24 && size !== 32) {
      throw new RangeError(`Icon size ${size} is not on the §07 whitelist (16/20/24/32).`);
    }
    // Curated names (the transpiled `Icons` enum lowers to its camelCase member) resolve from the
    // generated catalog; pack glyphs arrive as IconGlyph objects and pass through whole.
    this.glyph = typeof glyph === 'string' ? new IconGlyph(glyph, iconPaths[glyph] ?? '') : glyph;
    this.size = size;
    this.color = color;
    this.label = label;
  }
}

export class Stack extends VisualNode {
  readonly nodeKind = 'stack';
  align: string;
  width?: SizeValue;
  height?: SizeValue;
  children: VisualChild[] = [];

  constructor(
    align = 'topStart',
    config?: { width?: SizeValue | number; height?: SizeValue | number },
  ) {
    super();
    this.align = align;
    if (config?.width !== undefined) this.width = SizeValue.from(config.width);
    if (config?.height !== undefined) this.height = SizeValue.from(config.height);
  }

  add(child: VisualChild): void {
    this.children.push(child);
  }
}

export class Positioned extends VisualNode {
  readonly nodeKind = 'positioned';
  child: VisualChild;
  top: number | null;
  end: number | null;
  bottom: number | null;
  start: number | null;

  constructor(
    child: VisualChild,
    top: number | null = null,
    end: number | null = null,
    bottom: number | null = null,
    start: number | null = null,
  ) {
    super();
    this.child = child;
    this.top = top;
    this.end = end;
    this.bottom = bottom;
    this.start = start;
  }
}

interface FlexibleConfig {
  animateChanges?: boolean;
}

export class Flexible extends VisualNode {
  readonly nodeKind = 'flexible';
  child: VisualChild;
  flex: number;
  animateChanges = false;

  constructor(child: VisualChild, flex = 1, config?: FlexibleConfig) {
    super();
    this.child = child;
    this.flex = flex;
    if (config) Object.assign(this, config);
  }
}

/** Mirror of the C# `Spinner` (spec B15): the 8-bar activity indicator, icon em-box sizes. */
export class Spinner extends VisualNode {
  readonly nodeKind = 'spinner';
  size: number;
  color: ColorTokenValue | null;

  constructor(size = 20, color: ColorTokenValue | null = null) {
    super();
    this.size = size;
    this.color = color;
  }
}

export class Spacer extends VisualNode {
  readonly nodeKind = 'spacer';
  flex: number;
  fixedLength: number;
  /** Spec B14: weight changes animate over Motion.Base (pairs with an animated Flexible). */
  animateChanges = false;

  constructor(flex = 1, config?: { animateChanges?: boolean; key?: string | null }) {
    super();
    this.flex = Math.max(1, flex);
    this.fixedLength = 0;
    if (config) Object.assign(this, config);
  }

  static fixed(length: number): Spacer {
    const spacer = new Spacer();
    spacer.flex = 0;
    spacer.fixedLength = length;
    return spacer;
  }
}
