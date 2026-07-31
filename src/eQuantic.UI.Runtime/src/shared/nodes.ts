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

/** Wire shape of the C# `GridTrack`. */
export interface GridTrackValue {
  kind: 'fixed' | 'fill' | 'hug';
  value: number;
}

/** Spec S7 — scroll-anchored chrome: in flow until scrolling pins it at `offset`. */
export interface StickyNode {
  nodeKind: 'sticky';
  key?: string | null;
  child: VisualNodeValue;
  offset: number;
}

/** Spec S6 — a subtree that adapts to the window size class (up to 3 variants). */
export interface AdaptiveNodeValue {
  nodeKind: 'adaptive';
  key?: string | null;
  compact: VisualNodeValue;
  medium?: VisualNodeValue | null;
  expanded?: VisualNodeValue | null;
}

/** Spec S4 — the 2D grid container (auto-flow, explicit column tracks). */
export interface GridNode {
  nodeKind: 'grid';
  key?: string | null;
  columns: GridTrackValue[];
  gap: number;
  rowGap?: number | null;
  padding?: EdgeInsetsValue;
  width?: SizeValueValue;
  height?: SizeValueValue;
  children: VisualNodeValue[];
}

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
  /** Elevation level 0-5 (§05) — resolved through the active theme's ShadowSpec. */
  elevation?: number;
  /** Clip children to the rrect (native PushClip / CSS overflow:hidden) — loop-motion container. */
  clip?: boolean;
  /** 2-stop linear gradient (engine fence) — draws OVER the solid background when both are set. */
  gradient?: LinearGradientValue | null;
  /** Spec S1 group opacity 0–1 (one composited layer — CSS opacity / native PushLayer). */
  opacity?: number | null;
  /** Spec S1 static transform, center-anchored, paint-only (CSS transform twin). */
  transform?: TransformValue | null;
  /** Spec S1 width ÷ height constraint; one determined axis derives the other. 0/undefined = none. */
  aspectRatio?: number;
  /** Spec S5: style diff while hovered (CSS :hover — never fires on touch). */
  hover?: StyleDiffValue | null;
  /** Spec S5: style diff while focused (CSS :focus-visible). */
  focus?: StyleDiffValue | null;
}

/** Wire shape of the C# `StyleDiff` — only set members override the base. */
export interface StyleDiffValue {
  background?: ColorTokenValue | null;
  borderColor?: ColorTokenValue | null;
  borderWidth?: number | null;
  elevation?: number | null;
  opacity?: number | null;
}

/** Wire shape of the C# `Transform2D`: components applied translate → rotate → scale. */
export interface TransformValue {
  translateX?: number;
  translateY?: number;
  rotationDegrees?: number;
  scaleX?: number;
  scaleY?: number;
}

/** Wire shape of the C# `LinearGradient`: two token stops on a straight axis. */
export interface LinearGradientValue {
  from: ColorTokenValue;
  to: ColorTokenValue;
  direction: string;
}

/** Base shape every abstract node carries. */
export interface VisualNodeValue {
  nodeKind: string;
  key?: string | null;
  /** Spec S4: grid column span (parent-interpreted; 0/1 = one column). */
  gridSpan?: number;
  /** Spec S1 align-self: overrides the parent flex container's cross alignment for this child. */
  alignSelf?: CrossAlignValue | null;
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
  /** Spec S3: children wrap onto new lines when the main extent overflows. */
  wrap?: boolean;
  /** Spec S3: spacing between wrapped lines; undefined = same as gap. */
  runGap?: number | null;
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
  /** Pressed-state fill token — drives the generated `.eq-pressable:active` swap. */
  pressedBackground?: ColorTokenValue | null;
}

export interface FlexibleNode extends VisualNodeValue {
  nodeKind: 'flexible';
  child: VisualNodeValue;
  flex: number; /** Spec B14: weight changes animate Base/standard; omitted on a regression (snap). */
  animateChanges?: boolean;
}

export interface SpacerNode extends VisualNodeValue {
  nodeKind: 'spacer';
  flex: number;
  fixedLength: number;
}

export type AlignmentValue =
  | 'topStart'
  | 'topCenter'
  | 'topEnd'
  | 'centerStart'
  | 'center'
  | 'centerEnd'
  | 'bottomStart'
  | 'bottomCenter'
  | 'bottomEnd';

/** Spec §06 loop motion: a transform-only translate loop around one child. Offsets are fractions
 * of the node's OWN width (CSS translateX(%) base == the native realizer's offset math). */
export interface LoopMotionNode extends VisualNodeValue {
  nodeKind: 'loopMotion';
  child: VisualNodeValue;
  effect: string;
  fromX: number;
  toX: number;
  durationMs: number;
  /** Reduce Motion policy: decorative loops hide entirely at rest (Skeleton shimmer). */
  hideAtRest?: boolean;
}

/** Spec B15: the canonical activity indicator — 8 phase-staggered rrect bars, 800ms/rev. */
export interface SpinnerNode extends VisualNodeValue {
  nodeKind: 'spinner';
  size: number;
  color?: ColorTokenValue | null;
}

/** Phase C viewport layer: the child escapes the page flow (fixed inset-0 stacking layer). */
export interface OverlayNode extends VisualNodeValue {
  nodeKind: 'overlay';
  child: VisualNodeValue;
}

/** Spec §06 enter motion: the subtree animates IN when it first appears ('fade' | 'slideUp'). */
export interface PresenceNode extends VisualNodeValue {
  nodeKind: 'presence';
  child: VisualNodeValue;
  enter?: string;
}

/** Gestures v2: vertical drag-to-dismiss — the child follows a downward drag; releasing past the
 * threshold fires onDismiss, short of it glides back. */
export interface DragDismissNode extends VisualNodeValue {
  nodeKind: 'dragDismiss';
  child: VisualNodeValue;
  onDismiss?: (() => void) | null;
}

/** Spec B9/B10 primitive: single-line text entry — a real chrome-less <input> on web. */
export interface TextEntryNode extends VisualNodeValue {
  nodeKind: 'textEntry';
  value: string;
  onChanged?: ((value: string) => void) | null;
  placeholder?: string | null;
  onSubmit?: (() => void) | null;
  onFocusChanged?: ((focused: boolean) => void) | null;
  disabled?: boolean;
  obscure?: boolean;
  role: string;
}

export interface ScrollViewNode extends VisualNodeValue {
  nodeKind: 'scrollView';
  child: VisualNodeValue;
  /** ScrollAxis as camelCase member string ('vertical' | 'horizontal'). */
  axis: string;
  width?: SizeValueValue;
  height?: SizeValueValue;
}

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

/** Wire shape of the C# `IconGlyph`: target-neutral path data (any pack or the curated set). */
export interface IconGlyphValue {
  name: string;
  path: string;
  style?: string;
  viewBox?: string;
  strokeWidth?: number;
}

export interface IconNode extends VisualNodeValue {
  nodeKind: 'icon';
  /** The RESOLVED glyph — curated names resolve at construction, pack glyphs arrive whole. */
  glyph: IconGlyphValue;
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
