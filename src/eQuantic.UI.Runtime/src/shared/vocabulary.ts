/**
 * Runtime classes for the shared vocabulary's NODES — what transpiled component code instantiates
 * (`new Box(new BoxStyle({ … }), content)`, `new Row(gap, { height: SizeValue.fill })`,
 * `content.add(label)`). Instances ARE the `nodes.ts` shapes (`nodeKind` discriminators, camelCase
 * fields), so `lowerVisualNode` consumes them as-is — and every node carries `render(): HtmlNode`
 * that self-lowers with the ambient Photon context, which is what slots an abstract tree into the
 * existing web component pipeline (a page's `build()` can return one directly; the framework calls
 * `render()` like on any other component; the reconciler never learns about abstract nodes).
 */

import type { HtmlNode } from '../core/types';
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
  child: VisualNode | null;

  constructor(style: BoxStyle = new BoxStyle(), child: VisualNode | null = null) {
    super();
    this.style = style;
    this.child = child;
  }
}

interface FlexConfig {
  main?: MainAlignValue;
  cross?: CrossAlignValue;
  padding?: EdgeInsetsValue;
  width?: SizeValue | number;
  height?: SizeValue | number;
  background?: ColorTokenValue | null;
  cornerRadius?: CornerRadii;
}

abstract class FlexNode extends VisualNode {
  gap: number;
  main: MainAlignValue = 'start';
  abstract cross: CrossAlignValue;
  padding: EdgeInsetsValue = new EdgeInsets();
  width?: SizeValue;
  height?: SizeValue;
  background?: ColorTokenValue | null;
  cornerRadius: CornerRadii = new CornerRadii();
  children: VisualNode[] = [];

  protected constructor(gap: number, config?: FlexConfig) {
    super();
    this.gap = gap;
    if (!config) return;
    const { width, height, ...rest } = config;
    Object.assign(this, rest);
    if (width !== undefined) this.width = SizeValue.from(width);
    if (height !== undefined) this.height = SizeValue.from(height);
  }

  add(child: VisualNode): void {
    this.children.push(child);
  }
}

export class Row extends FlexNode {
  readonly nodeKind = 'row';
  /** Row cross defaults to Center (spec A2). */
  cross: CrossAlignValue = 'center';

  constructor(gap = 0, config?: FlexConfig) {
    super(gap, config);
  }
}

export class Column extends FlexNode {
  readonly nodeKind = 'column';
  /** Column cross defaults to Stretch (spec A2). */
  cross: CrossAlignValue = 'stretch';

  constructor(gap = 0, config?: FlexConfig) {
    super(gap, config);
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
}

export class Pressable extends VisualNode {
  readonly nodeKind = 'pressable';
  child: VisualNode;
  onPressed: (() => void) | null;
  disabled = false;
  label: string | null = null;

  constructor(child: VisualNode, onPressed: (() => void) | null = null, config?: PressableConfig) {
    super();
    this.child = child;
    this.onPressed = onPressed;
    if (config) Object.assign(this, config);
  }
}

export class Stack extends VisualNode {
  readonly nodeKind = 'stack';
  align: string;
  width?: SizeValue;
  height?: SizeValue;
  children: VisualNode[] = [];

  constructor(align = 'topStart', config?: { width?: SizeValue | number; height?: SizeValue | number }) {
    super();
    this.align = align;
    if (config?.width !== undefined) this.width = SizeValue.from(config.width);
    if (config?.height !== undefined) this.height = SizeValue.from(config.height);
  }

  add(child: VisualNode): void {
    this.children.push(child);
  }
}

export class Positioned extends VisualNode {
  readonly nodeKind = 'positioned';
  child: VisualNode;
  top: number | null;
  end: number | null;
  bottom: number | null;
  start: number | null;

  constructor(
    child: VisualNode,
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

export class Flexible extends VisualNode {
  readonly nodeKind = 'flexible';
  child: VisualNode;
  flex: number;

  constructor(child: VisualNode, flex = 1) {
    super();
    this.child = child;
    this.flex = flex;
  }
}

export class Spacer extends VisualNode {
  readonly nodeKind = 'spacer';
  flex: number;
  fixedLength: number;

  constructor(flex = 1) {
    super();
    this.flex = Math.max(1, flex);
    this.fixedLength = 0;
  }

  static fixed(length: number): Spacer {
    const spacer = new Spacer();
    spacer.flex = 0;
    spacer.fixedLength = length;
    return spacer;
  }
}
