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
// Re-exported so every importer of './nodes' keeps the name it always had, and IMPORTED because a
// re-export alone does not bring a name into this module's own scope.
import type {
  CrossAlignValue,
  MainAlignValue,
  NavigableMoveValue,
  SizeKindValue,
  VectorPaintKindValue,
} from './enums.generated';

export type { SizeKindValue, NavigableMoveValue } from './enums.generated';

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
  /** The MONOSPACED face (C# `TypeStyle.Mono`) — code, keys, versions. */
  mono?: boolean;
  /** The SLANTED cut (C# `TypeStyle.Italic`) — an axis, so it composes with weight and mono. */
  italic?: boolean;
}

export type { MainAlignValue } from './enums.generated';
export type { CrossAlignValue } from './enums.generated';

/** Wire shape of the C# `GridTrack`. */
export interface GridTrackValue {
  kind: 'fixed' | 'fill' | 'hug';
  value: number;
}

/** Spec S7 — scroll-anchored chrome: in flow until scrolling pins it at `offset`. */
/** Wire shape of the C# `SafeArea`: which edges the system owns, plus the caller's own padding. */
/** Wire shape of the C# `Draggable`: the gesture's RULES, which the controller reads off the DOM. */
export interface DraggableNode extends VisualNodeValue {
  nodeKind: 'draggable';
  child: VisualNodeValue;
  /** DragAxis, as the camelCase name every enum lowers to: 'vertical' | 'horizontal'. */
  axis?: string;
  min?: number;
  max?: number;
  restOffset?: number;
  /** Report a FRACTION of the surface's own extent, which only the client can measure. */
  normalized?: boolean;
  /** Whether the subtree follows the finger, or the caller repaints it from `onMoved`. */
  follows?: boolean;
  onReleased?: ((offset: number) => void) | null;
  onMoved?: ((offset: number) => void) | null;
}

export interface SafeAreaNode {
  nodeKind: 'safeArea';
  child: VisualNodeValue;
  /** SafeEdges flags — Top 1, Bottom 2, Start 4, End 8. */
  edges: number;
  extra?: EdgeInsetsValue | null;
}

export interface StickyNode {
  nodeKind: 'sticky';
  key?: string | null;
  child: VisualNodeValue;
  offset: number;
  /** Floating chrome: fixed at the viewport edge, above the page (content slides underneath). */
  float?: boolean;
  /** Scroll-linked diff — applies while <html> carries eq-scrolled (runtime listener). */
  scrolledStyle?: StyleDiffValue | null;
  /** Spec S6: animates the swap into/out of `scrolledStyle` (null = snap). */
  transition?: TransitionSpecValue | null;
}

/** Wave 3 placements — the four anchor corners (flip/clamp is the positioning fence). */
export type AnchorPlacementValue =
  | 'bottomStart'
  | 'bottomEnd'
  | 'topStart'
  | 'topEnd'
  | 'bottomCenter'
  | 'topCenter';

/** Wave 3 anchored overlay: floating panel positioned relative to the in-flow anchor. */
export interface AnchoredNode extends VisualNodeValue {
  nodeKind: 'anchored';
  anchor: VisualNodeValue;
  panel: VisualNodeValue;
  placement?: AnchorPlacementValue;
  gap?: number;
  open?: boolean;
  onDismiss?: (() => void) | null;
  matchAnchorWidth?: boolean;
  /** Wave 3b (Tooltip): panel shows while the anchor is hovered — pure CSS on web. */
  openOnHover?: boolean;
  /** The panel DESCRIBES the anchor (the Tooltip contract): role=tooltip + deterministic id on
   * the panel, aria-describedby on the anchor's root. Meaningful only with openOnHover. */
  describesAnchor?: boolean;
  /** What the panel IS ('none' | 'menu' | 'listbox'): role + id on the panel, numbered item rows,
   * and the anchor's half of the pattern (haspopup/controls; combobox for a listbox). */
  panelRole?: string;
  /** Which item row the keyboard is on (tree order, -1 = none) — aria-activedescendant on the
   * anchor: a highlight that is only paint says nothing to a screen reader. */
  activeIndex?: number;
  /** Paints the outside-tap scrim (mega-menu page veil) — a BoxStyle, like the C# ScrimStyle. */
  scrimStyle?: BoxStyleValue | null;
  /** Open/close motion: panel + scrim stay mounted in both states and glide between them. */
  motion?: TransitionSpecValue | null;
}

/** Spec S6 — a subtree that adapts to the window size class (up to 3 variants). */
export interface AdaptiveNodeValue {
  nodeKind: 'adaptive';
  key?: string | null;
  compact: VisualNodeValue;
  medium?: VisualNodeValue | null;
  expanded?: VisualNodeValue | null;
  /** Custom thresholds in dp (default 600/840) — a design that switches elsewhere brings its own. */
  mediumFrom?: number;
  expandedFrom?: number;
}

/** Wire shape of the C# `KeyChord`: a DOM `KeyboardEvent.key` name plus [Flags] KeyModifiers
 * (Shift=1, Alt=2, Command=4, Control=8 — flags enums transpile numerically). */
export interface KeyChordValue {
  key: string;
  modifiers?: number;
}

/** Spec S8 — a keyboard binding that is live while this subtree is mounted. */
export interface ShortcutNode extends VisualNodeValue {
  nodeKind: 'shortcut';
  child: VisualNodeValue;
  chord: KeyChordValue;
  onPressed?: (() => void) | null;
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
  /** A cap: `SizeValue` (the C# twin), or a bare number from a payload written before it was one. */
  maxWidth?: SizeValueValue | number;
  maxHeight?: SizeValueValue | number;
  padding?: EdgeInsetsValue;
  background?: ColorTokenValue | null;
  cornerRadius?: CornerRadiiValue;
  borderWidth?: number;
  borderColor?: ColorTokenValue;
  /** Which edges the border draws on — the C# `BorderSides` flags as their numeric value. */
  borderSides?: number;
  /** Elevation level 0-5 (§05) — resolved through the active theme's ShadowSpec. */
  elevation?: number;
  /** Clip children to the rrect (native PushClip / CSS overflow:hidden) — loop-motion container. */
  clip?: boolean;
  /** 2-stop linear gradient (engine fence) — draws OVER the solid background when both are set. */
  gradient?: LinearGradientValue | null;
  pattern?: GridPatternValue | null;
  glow?: RadialGradientValue | null;
  /** Spec S1 group opacity 0–1 (one composited layer — CSS opacity / native PushLayer). */
  opacity?: number | null;
  /** Spec S3 frosted glass: backdrop blur radius in logical px (CSS backdrop-filter / native pass split). */
  backdropBlur?: number;
  /** Custom shadow (glow/halo — full spec, composes with elevation). */
  shadow?: ShadowSpecValue | null;
  /** Composed custom shadows in paint order (ring + projected glow pairs). */
  shadows?: ShadowSpecValue[] | null;
  /** The 1px inner top highlight (glossy buttons) — an inset box-shadow entry. */
  insetHighlight?: ColorTokenValue | null;
  /** Element blur (blur-3xl glow washes): this box's own pixels — CSS filter: blur(). */
  blur?: number;
  /** Spec S1 static transform, center-anchored, paint-only (CSS transform twin). */
  transform?: TransformValue | null;
  /** Spec S1 width ÷ height constraint; one determined axis derives the other. 0/undefined = none. */
  aspectRatio?: number;
  /** Spec S6: animates changes to the covered channels (CSS transition). null = snap. */
  transition?: TransitionSpecValue | null;
  /** Spec S5: style diff while hovered (CSS :hover — never fires on touch). */
  hover?: StyleDiffValue | null;
  /** Spec S5: style diff while focused (CSS :focus-visible). */
  focus?: StyleDiffValue | null;
  /** CSS cursor mirror — the C# PointerCursor member name in camelCase ('crosshair', 'colResize'). */
  cursor?: string;
}

/** Wire shape of the C# `StyleDiff` — only set members override the base. */
export interface StyleDiffValue {
  background?: ColorTokenValue | null;
  borderColor?: ColorTokenValue | null;
  borderWidth?: number | null;
  elevation?: number | null;
  opacity?: number | null;
  /** Swaps the gradient fill while active (gradient-button hover). */
  gradient?: LinearGradientValue | null;
  /** Backdrop blur radius while active (the scrolled header's frosted veil). */
  backdropBlur?: number | null;
}

/** Wire shape of the C# `ShadowSpec` (offsetY, blur, spread, color). */
export interface ShadowSpecValue {
  offsetY: number;
  blur: number;
  spread: number;
  color: ColorTokenValue;
}

/** Wire shape of the C# `Transform2D`: components applied translate → rotate → scale. */
export interface TransformValue {
  translateX?: number;
  translateY?: number;
  rotationDegrees?: number;
  scaleX?: number;
  scaleY?: number;
}

/** Wire shape of the C# `LinearGradient`: two token stops on a straight axis, plus the optional
 * `via` midpoint at `viaPosition` (0–1). */
export interface LinearGradientValue {
  from: ColorTokenValue;
  to: ColorTokenValue;
  direction: string;
  via?: ColorTokenValue | null;
  viaPosition?: number;
}

/** Wire shape of the C# `TransitionSpec` (spec S6). `channels` carries the [Flags] StyleChannels
 * bits (the transpiler emits flags enums numerically); `easing` is a cubic-bezier control tuple. */
export interface TransitionSpecValue {
  channels: number;
  durationMs?: number;
  delayMs?: number;
  easing?: readonly number[];
}

/** Wire shape of the C# `RadialGradient`: two-stop elliptical spotlight, center as box fractions. */
export interface RadialGradientValue {
  from: ColorTokenValue;
  to: ColorTokenValue;
  centerX: number;
  centerY: number;
  radiusX: number;
  radiusY: number;
}

/** Wire shape of the C# `GridPattern`: repeating hairline grid (square cell, token line color). */
export interface GridPatternValue {
  cell: number;
  color: ColorTokenValue;
  lineWidth: number;
}

/** Base shape every abstract node carries. */
/** A shared component instance sitting in a child slot — the lowering expands it via build(). */
export interface ComponentChild {
  build(context: unknown): unknown;
}

export interface VisualNodeValue {
  /**
   * The C# `VisualNode.Bookmark` — a named place a link can reach, which becomes the element's
   * `id`. Distinct from `key`, which is reconciler identity and need only be unique among siblings;
   * and not called `anchor`, a word `Anchored` already spends on popover positioning.
   */
  bookmark?: string | null;
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
  /** Where this text sits in the document's OUTLINE — 1..6, 0 for text that is not a heading. */
  headingLevel?: number;
  /** `TypeRole` as a camelCase member string ('display' | 'heading' | … | 'bodyL' | 'caption'). */
  role: string;
  color?: ColorTokenValue | null;
  maxLines: number;
  styleOverride?: TypeStyleValue | null;
  /** Fills the glyphs with a gradient instead of `color` (background-clip: text). */
  gradient?: LinearGradientValue | null;
  /** Line alignment within the paragraph ('start' | 'center' | 'end') — CSS text-align twin. */
  align?: string;
  /** Monospace face (code, versions) — the platform mono stack on web. */
  /** Tabular figures (tnum) — a changing number must not shift its row. */
  tabular?: boolean;
  mono?: boolean;
  /** The SLANTED cut for the whole paragraph (C# `Text.Italic`). */
  italic?: boolean;
  /** Rich inline runs — rendered instead of `content` when present (wire of C# Text.Spans). */
  spans?: TextRunValue[] | null;
  /** Spec S6: animates color changes (the design's transition-colors). null = snap. */
  transition?: TransitionSpecValue | null;
}

/** Wire shape of the C# `TextRun` — one inline run of a rich Text. */
export interface TextRunValue {
  content: string;
  color?: ColorTokenValue | null;
  mono?: boolean;
  /** The SLANTED cut for this run (C# `TextRun.Italic`) — markdown's `*single asterisk*`. */
  italic?: boolean;
  /** Inline emphasis (C# `TextRun.Weight`) — the camelCased FontWeight name. */
  weight?: string | null;
  /**
   * A SIZE of its own (C# `TextRun.StyleOverride`) — inline code at 13.5 inside a 16 paragraph.
   * Only the size is taken: the run keeps the paragraph's line box, which is what makes it a run.
   */
  styleOverride?: TypeStyleValue | null;
  /**
   * Where the run LINKS to (C# `TextRun.Href`). A link inside a sentence has to live on a run: a
   * Link around the Text makes the whole paragraph one link, and a Row of Texts breaks between RUNS
   * instead of between words.
   */
  destination?: string | null;
}

/** Wire shape of the C# `Hoverable` — pointer-presence callback (S5 programmable hover). */
export interface HoverableNode extends VisualNodeValue {
  nodeKind: 'hoverable';
  child: VisualNodeValue;
  /** true = pointer entered, false = pointer left. */
  onChanged?: (entered: boolean) => void;
}

/**
 * Wire shape of the C# `Simulated` — draws its subtree AS IF it were in these states. The flags are
 * the C# `SimulatedState` by VALUE (Hovered 1, Pressed 2, Focused 4), because that is what a
 * [Flags] enum transpiles to.
 */
export interface SimulatedNode extends VisualNodeValue {
  nodeKind: 'simulated';
  state: number;
  child: VisualNodeValue;
}

/** Wire shape of the C# `InFlow` — draws an overlay where it stands, without its layer. */
export interface InFlowNode extends VisualNodeValue {
  nodeKind: 'inFlow';
  child: VisualNodeValue;
}

/** Wire shape of the C# `InView` — reports when its child comes on screen, and when it leaves. */
export interface InViewNode extends VisualNodeValue {
  nodeKind: 'inView';
  child: VisualNodeValue;
  onChanged?: (visible: boolean) => void;
  /** How much has to show before it counts, 0–1. */
  threshold?: number;
}

export interface PressableNode extends VisualNodeValue {
  nodeKind: 'pressable';
  child: VisualNodeValue;
  onPressed?: (() => void) | null;
  disabled?: boolean;
  label?: string | null;
  /** Pressed-state fill token — drives the generated `.eq-pressable:active` swap. */
  pressedBackground?: ColorTokenValue | null;
  /** Selection for a toggling/picking button — lowers to aria-pressed. null = not selectable. */
  selected?: boolean | null;
  /** PARTLY selected — the "select all" over a mixed set. Checkbox role only (ARIA's own rule);
   * wins over selected. */
  mixed?: boolean;
  /** This pressable OPENS something (accordion header, select field, menu trigger) — lowers to
   * aria-expanded. null = controls no disclosure at all. */
  expanded?: boolean | null;
  /** §10 initial focus: when a trap opens around this pressable, focus lands HERE first. */
  initialFocus?: boolean;
  /** Composite-item role ('button' | 'radio' | 'checkbox' | 'switch' | 'tab' | 'menuItem' |
   * 'option'): radio and tab lower with roving tabindex (the wrapping Adjustable is the one
   * stop); menuItem/option leave the tab order too (the keyboard lives on the trigger, the
   * highlight rides aria-activedescendant); checkbox/switch keep their own stop. */
  role?: string;
}

export interface FlexibleNode extends VisualNodeValue {
  nodeKind: 'flexible';
  child: VisualNodeValue;
  flex: number; /** Spec B14: weight changes animate Base/standard; omitted on a regression (snap). */
  /** CSS flex-basis in dp. Absent or 0 = sized purely from the weight (the historical shape). */
  basis?: number;
  /** CSS flex-shrink. Absent = 1, which is what this realizer has always emitted. */
  shrink?: number;
  animateChanges?: boolean;
}

export interface SpacerNode extends VisualNodeValue {
  nodeKind: 'spacer';
  flex: number;
  fixedLength: number;
  /** Spec B14: weight changes animate Base/standard; omitted on a regression (snap). */
  animateChanges?: boolean;
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
  /** false = non-modal layer (Toast): pointer events pass through outside the child. */
  modal?: boolean;
  /** The dialog's accessible NAME (aria-label) — a Dialog passes its title. */
  label?: string | null;
  /** An ALERT dialog (destructive confirm) — role="alertdialog", announced assertively. */
  alert?: boolean;
  /** Visibility while `motion` is set — a closed layer stays mounted and animates out. */
  open?: boolean;
  /** Open/close motion: the layer stays mounted in both states and fades between them. */
  motion?: TransitionSpecValue | null;
}

/** Spec §06 enter motion: the subtree animates IN when it first appears ('fade' | 'slideUp'). */
export interface PresenceNode extends VisualNodeValue {
  nodeKind: 'presence';
  child: VisualNodeValue;
  enter?: string;
}

/** Navigation semantics: the child becomes a link to destination (web <a>; native host seam). */
export interface LinkNode extends VisualNodeValue {
  nodeKind: 'link';
  destination: string;
  child: VisualNodeValue;
  label?: string | null;
  /** Keeps the reader where they are instead of starting the new page at its top (C# twin). */
  keepsPosition?: boolean;
  /** This link points at the page the reader is ON — aria-current="page" (C# twin). */
  current?: boolean;
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
  /** The accessible name (aria-label web / AX label native) — a placeholder is only a hint. */
  label?: string | null;
  /** Helper/error caption text — associated (describedby) and announced (polite live region)
   * through the entry's sr-only twin; the visible caption is a sibling the entry can't reference. */
  description?: string | null;
  /** The value fails validation — surfaced as aria-invalid, never worded into the name. */
  invalid?: boolean;
  onSubmit?: (() => void) | null;
  onFocusChanged?: ((focused: boolean) => void) | null;
  disabled?: boolean;
  obscure?: boolean;
  role: string;
  /** Takes focus on MOUNT (the command-palette contract) — the attribute alone only fires on the
   * initial document parse, so the client lowering focuses it too. */
  autofocus?: boolean;
  /** Line count: 1 (default) is the single-line input, more makes it a textarea that tall. */
  lines?: number;
}

export interface ScrollViewNode extends VisualNodeValue {
  nodeKind: 'scrollView';
  child: VisualNodeValue;
  /** ScrollAxis as camelCase member string ('vertical' | 'horizontal'). */
  axis: string;
  width?: SizeValueValue;
  height?: SizeValueValue;
  /** Programmatic scroll position in dp (applied once, on adoption — the browser owns it after). */
  offset?: number;
  /** Where it IS, whenever that changes — what a windowed list reads to know what to build. */
  onScrolled?: ((offset: number) => void) | null;
  /** How tall the viewport turned out to be, reported after the pass mounts it. */
  onViewportChanged?: ((height: number) => void) | null;
}

export interface SheetSurfaceNode extends VisualNodeValue {
  nodeKind: 'sheetSurface';
  child: VisualNodeValue;
  firstRow?: number;
  firstCol?: number;
  headerWidth?: number;
  headerHeight?: number;
  label?: string | null;
}

/** Arrow-key adjustment semantics — one Tab stop wrapping a slider-like control. */
export interface AdjustableNode extends VisualNodeValue {
  nodeKind: 'adjustable';
  child: VisualNodeValue;
  onAdjust?: (direction: number) => void;
  label?: string;
  /** ARIA identity of the host — 'slider' (default), 'tablist' or 'radiogroup'. */
  role?: 'slider' | 'tablist' | 'radiogroup';
}

/**
 * A TWO-DIMENSIONAL composite (C# twin: Navigable) — one Tab stop, arrows moving inside it. The
 * host reads the keys and reports an abstract MOVE; the composite decides what a move means. Rows
 * are structural: a grid whose cells are not inside rows is an invalid accessibility tree, and the
 * realizer emits them `display:contents` so layout never sees them.
 */
export interface NavigableNode extends VisualNodeValue {
  nodeKind: 'navigable';
  rows: VisualNodeValue[];
  onMove?: (move: NavigableMoveValue) => void;
  label?: string;
  role?: 'grid';
  /** Whether rows[0] holds the column headers (a calendar's day-name row). */
  hasHeaderRow?: boolean;
  /** The focused cell as [row, item] — announced through aria-activedescendant. A C# tuple
   * crosses as a plain array and the transpiler cannot promise its arity, so the type is what a
   * tuple actually becomes; the realizer reads exactly two entries. */
  activeCell?: readonly number[] | null;
}

/**
 * An EDITABLE code surface: the child draws the lines, this adds a caret, a selection and a
 * keyboard. Everything a key or a click MEANS lives in the controller, which is transpiled from the
 * same C# the native host calls — so the two surfaces cannot drift on what ⌥← does.
 */
export interface CodeSurfaceNode extends VisualNodeValue {
  nodeKind: 'codeSurface';
  child: VisualNodeValue;
  /** The live CodeEditorController — a transpiled class, not a copy of its state. */
  editor: CodeEditorLike;
  contentTop: number;
  lineHeight: number;
  contentLeft: number;
  columnWidth: number;
  onChanged?: (() => void) | null;
  label?: string | null;
  autofocus?: boolean;
  /** The two marks' ink (C# `CodeSurface.CaretColor` / `SelectionColor`) — an editor on an inverse
   * slab writes with an ink of its own, and the page theme's would vanish into the slab. */
  caretColor?: ColorTokenValue | null;
  selectionColor?: ColorTokenValue | null;
}

/** What the surface needs from the controller — the transpiled class satisfies it structurally. */
export interface CodeEditorLike {
  readonly selection: {
    anchor: CodePositionLike;
    focus: CodePositionLike;
    isEmpty: boolean;
    start: CodePositionLike;
    end: CodePositionLike;
  };
  readonly caret: CodePositionLike;
  readonly document: {
    lineCount: number;
    line(index: number): string;
    clamp(position: CodePositionLike): CodePositionLike;
  };
  readOnly: boolean;
  type(c: string): boolean;
  selectWord(at: CodePositionLike): void;
  selectLine(line: number): void;
}

export interface CodePositionLike {
  readonly line: number;
  readonly column: number;
}

/** A live camera surface — the session id names the MediaStream the runtime attaches. */
export interface CameraPreviewNode extends VisualNodeValue {
  nodeKind: 'cameraPreview';
  session?: { id: string } | null;
  width: number;
  height: number;
  cornerRadius?: CornerRadiiValue | null;
  alt?: string;
}

/**
 * An embedded, isolated web document (C# twin: `WebFrame`). `sandbox` crosses as the [Flags]
 * NUMBER — bit 1 scripts, 2 same-origin, 4 forms, 8 popups — and the lowering spells the tokens.
 */
export interface WebFrameNode extends VisualNodeValue {
  nodeKind: 'webFrame';
  source?: string | null;
  document?: string | null;
  sandbox: number;
  title?: string;
  width?: SizeValueValue;
  height?: SizeValueValue;
  cornerRadius?: CornerRadiiValue | null;
}

export interface ImageNode extends VisualNodeValue {
  nodeKind: 'image';
  source: string;
  /** The DARK-mode artwork (C# `Image.DarkSource`); null = one image serves both. */
  darkSource?: string | null;
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

/** A vector shape at ANY size — the same path data an icon carries. */
export interface VectorNode extends VisualNodeValue {
  nodeKind: 'vector';
  /** The RESOLVED glyph — curated names resolve at construction, pack glyphs arrive whole. */
  glyph: IconGlyphValue;
  /** The box's WIDTH in dp (C# `Vector.Size`). */
  size: number;
  /** The box's HEIGHT in dp — `size` unless the author gave the shape an aspect of its own. */
  height?: number;
  color?: ColorTokenValue | null;
  label?: string | null;
}

/**
 * W3: a component draws its OWN shapes inside the box the layout gives it, once per frame — a
 * sunburst, a simulation, a chart recomputed from data that never stops. The painter's vocabulary
 * is the ENGINE's (rects, circles, annular sectors, strokes), so the same calls draw on both
 * targets; here they become inline SVG.
 */
export interface CanvasNodeValue extends VisualNodeValue {
  nodeKind: 'canvas';
  draw: (painter: ICanvasPainter) => void;
  width?: SizeValueValue;
  height?: SizeValueValue;
  onPointerDown?: ((pointer: CanvasPointerValue) => void) | null;
  onPointerMove?: ((pointer: CanvasPointerValue) => void) | null;
  onPointerUp?: ((pointer: CanvasPointerValue) => void) | null;
  onPointerLeave?: (() => void) | null;
  label?: string | null;
}

/** The C# `ICanvasPainter` twin — the shapes a canvas can draw, in its own coordinates. */
export interface ICanvasPainter {
  readonly width: number;
  readonly height: number;
  fillRect(x: number, y: number, width: number, height: number, color: ColorTokenValue, cornerRadius?: number): void;
  strokeRect(x: number, y: number, width: number, height: number, color: ColorTokenValue, strokeWidth: number, cornerRadius?: number): void;
  fillCircle(centerX: number, centerY: number, radius: number, color: ColorTokenValue): void;
  fillAnnularSector(centerX: number, centerY: number, innerRadius: number, outerRadius: number, startAngle: number, endAngle: number, color: ColorTokenValue, cornerSmoothing?: number): void;
  line(x1: number, y1: number, x2: number, y2: number, color: ColorTokenValue, strokeWidth: number): void;
}

/** A pointer event in a canvas's own coordinates (the C# `CanvasPointer` twin). */
export interface CanvasPointerValue {
  x: number;
  y: number;
  pressed: boolean;
  modifiers: number;
}

/** `VectorPaintKind` transpiles to camelCase member strings. */
export type { VectorPaintKindValue } from './enums.generated';

/** One stop of a gradient: where it sits along the run (0 to 1) and the colour there. */
export interface VectorStopValue {
  offset: number;
  color: ColorValue;
}

/**
 * Mirrors `VectorGradient`, in the SVG's own terms. `userSpace` false (the default) makes the
 * coordinates FRACTIONS of the shape's own box; true makes them coordinates on the viewBox grid.
 * A linear run goes (x1,y1) → (x2,y2); a radial one is centred at (x1,y1) with `radius`.
 */
export interface VectorGradientValue {
  x1: number;
  y1: number;
  x2: number;
  y2: number;
  radius: number;
  userSpace: boolean;
  stops: VectorStopValue[];
}

/** Mirrors `VectorPaint`: not painted, painted in whatever tints me, a colour of its own, or a RUN
 * of colours — each a different answer, and a null could only have been one of them. */
export interface VectorPaintValue {
  kind: VectorPaintKindValue;
  color: ColorValue;
  gradient?: VectorGradientValue | null;
}

/** One shape of a drawing — path data on the artwork's own grid, plus how it paints. */
export interface VectorShapeValue {
  path: string;
  fill: VectorPaintValue;
  stroke: VectorPaintValue;
  strokeWidth: number;
  evenOdd: boolean;
  opacity: number;
}

/** Mirrors `VectorDrawing`: what a `.svg` FILE became, read once at build time. */
export interface VectorDrawingValue {
  minX: number;
  minY: number;
  width: number;
  height: number;
  shapes: VectorShapeValue[];
}

/** Vector ARTWORK: several shapes, each in the colour its designer chose. */
export interface DrawingNode extends VisualNodeValue {
  nodeKind: 'drawing';
  artwork: VectorDrawingValue;
  /** The box's WIDTH in dp. */
  width: number;
  /** The box's HEIGHT — the artwork's own aspect unless the author decided. */
  height: number;
  /** What answers the shapes the file left as `currentColor`. */
  tint?: ColorTokenValue | null;
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
  /** Spec S7: explicit stacking inside the Stack — higher paints on top; 0 = flow order. */
  zIndex?: number;
}

/** A shared component: the lowering expands it by calling `build(context)` (pure, mode-free). */
export interface ComponentNode extends VisualNodeValue {
  nodeKind: 'component';
  build(context: unknown): VisualNodeValue;
}
