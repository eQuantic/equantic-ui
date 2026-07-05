/**
 * The SHARED abstract vocabulary on the client (docs/SHARED-COMPONENTS-PLAN.md) — the JS shapes of
 * `eQuantic.UI.Primitives` nodes as the transpiler emits them (camelCase properties, enums as
 * camelCase member-name strings, record structs as plain classes). Dispatch is by `nodeKind`, the
 * wire discriminator mirrored from the C# `VisualNode.NodeKind` — class names don't survive bundling.
 */

/** sRGB color with straight alpha — mirrors `eQuantic.UI.Primitives.Color`. */
export interface ColorValue {
  r: number;
  g: number;
  b: number;
  a: number;
}

/** Paired light/dark color — mirrors `ColorToken`. */
export interface ColorTokenValue {
  light: ColorValue;
  dark: ColorValue;
}

/** `SizeKind` transpiles to camelCase member strings. */
export type SizeKindValue = 'hug' | 'fill' | 'fixed';

export interface SizeValueValue {
  kind: SizeKindValue;
  value: number;
}

export interface EdgeInsetsValue {
  start: number;
  top: number;
  end: number;
  bottom: number;
}

export interface CornerRadiiValue {
  topLeft: number;
  topRight: number;
  bottomRight: number;
  bottomLeft: number;
}

/** `FontWeight` arrives as a member-name string (enum repr); the lowering maps it to the number. */
export interface TypeStyleValue {
  size: number;
  lineHeight: number;
  weight: string | number;
  tracking: number;
  maxScale?: number;
}

export type MainAlignValue = 'start' | 'center' | 'end' | 'spaceBetween';
export type CrossAlignValue = 'start' | 'center' | 'end' | 'stretch';

export interface BoxStyleValue {
  width?: SizeValueValue;
  height?: SizeValueValue;
  minWidth?: number;
  minHeight?: number;
  maxWidth?: number;
  maxHeight?: number;
  padding?: EdgeInsetsValue;
  background?: ColorTokenValue | null;
  cornerRadius?: CornerRadiiValue;
  borderWidth?: number;
  borderColor?: ColorTokenValue;
}

/** Base shape every abstract node carries. */
export interface VisualNodeValue {
  nodeKind: string;
  key?: string | null;
}

export interface BoxNode extends VisualNodeValue {
  nodeKind: 'box';
  style: BoxStyleValue;
  child?: VisualNodeValue | null;
}

export interface FlexNodeValue extends VisualNodeValue {
  nodeKind: 'row' | 'column';
  gap: number;
  main: MainAlignValue;
  cross: CrossAlignValue;
  padding?: EdgeInsetsValue;
  width?: SizeValueValue;
  height?: SizeValueValue;
  background?: ColorTokenValue | null;
  cornerRadius?: CornerRadiiValue;
  children: VisualNodeValue[];
}

export interface TextNode extends VisualNodeValue {
  nodeKind: 'text';
  content: string;
  /** `TypeRole` as a camelCase member string ('display' | 'heading' | … | 'bodyL' | 'caption'). */
  role: string;
  color?: ColorTokenValue | null;
  maxLines: number;
  styleOverride?: TypeStyleValue | null;
}

export interface PressableNode extends VisualNodeValue {
  nodeKind: 'pressable';
  child: VisualNodeValue;
  onPressed?: (() => void) | null;
  disabled?: boolean;
  label?: string | null;
}

export interface FlexibleNode extends VisualNodeValue {
  nodeKind: 'flexible';
  child: VisualNodeValue;
  flex: number;
}

export interface SpacerNode extends VisualNodeValue {
  nodeKind: 'spacer';
  flex: number;
  fixedLength: number;
}

export type AlignmentValue =
  | 'topStart' | 'topCenter' | 'topEnd'
  | 'centerStart' | 'center' | 'centerEnd'
  | 'bottomStart' | 'bottomCenter' | 'bottomEnd';

export interface ImageNode extends VisualNodeValue {
  nodeKind: 'image';
  source: string;
  width: number;
  height: number;
  /** ImageFit as camelCase member string ('contain' | 'cover' | 'stretch'). */
  fit: string;
  alt: string;
  cornerRadius?: CornerRadiiValue;
}

export interface IconNode extends VisualNodeValue {
  nodeKind: 'icon';
  /** `Icons` member as a camelCase string ('search' | 'close' | …). */
  glyph: string;
  size: number;
  color?: ColorTokenValue | null;
  label?: string | null;
}

export interface StackNode extends VisualNodeValue {
  nodeKind: 'stack';
  align: AlignmentValue;
  width?: SizeValueValue;
  height?: SizeValueValue;
  children: VisualNodeValue[];
}

export interface PositionedNode extends VisualNodeValue {
  nodeKind: 'positioned';
  child: VisualNodeValue;
  top?: number | null;
  end?: number | null;
  bottom?: number | null;
  start?: number | null;
}

/** A shared component: the lowering expands it by calling `build(context)` (pure, mode-free). */
export interface ComponentNode extends VisualNodeValue {
  nodeKind: 'component';
  build(context: unknown): VisualNodeValue;
}
