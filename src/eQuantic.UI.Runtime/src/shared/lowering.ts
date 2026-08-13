/**
 * Client-side lowering of the shared abstract vocabulary to `HtmlNode` — the EXACT mirror of the C#
 * `WebRealizer` (SSR path). Hydration correctness depends on the two producing identical DOM, so
 * every rule here is normative and cross-pinned by tests on both sides:
 * - colors lower as `light-dark(#light, #dark)` straight from tokens (mode-free DOM);
 * - Box → div with `box-sizing: border-box` (Photon's inside border = the CSS parity contract);
 * - Row/Column → flex with the spec alignment defaults; Flexible → `flex: n s basis` + `min-size: 0`;
 * - Text → role-classed span with single-line ellipsis; Pressable → neutralized <button>;
 * - styles lower to ATOMIC CLASSES (style-atomizer.ts twin of the C# StyleAtomizer): identical
 *   declarations hash to identical class names on both sides, so hydration compares one sorted
 *   class string per element; only custom-property tails stay inline.
 */

import type { EventHandler, HtmlNode } from '../core/types';
import { installDraggableController } from '../dom/draggable';
import { installDragDismissController } from '../dom/drag-dismiss';
import {
  describeComponentFailure,
  reportComponentFailure,
  setFailureRenderer,
} from './component-boundary';
import { getActivePass } from './instance-store';
import { getPhotonTheme, setInFlow } from './photon-context';
import { declareInView } from './in-view';
import { cssFontWeight } from './value-types';
import { CodeKeymap } from './components/CodeKeymap';
import {
  atomizeEntries,
  atomizePseudo,
  atomizeScrolled,
  ensureAdaptiveGate,
  gateCompactUntil,
  gateExpandedFrom,
  gateMediumFrom,
  hashDeclaration,
  mergeAtomicDeclaration,
} from './style-atomizer';
import type {
  BoxNode,
  BoxStyleValue,
  ColorTokenValue,
  ColorValue,
  ComponentNode,
  CornerRadiiValue,
  EdgeInsetsValue,
  FlexNodeValue,
  FlexibleNode,
  IconGlyphValue,
  IconNode,
  AdjustableNode,
  CameraPreviewNode,
  WebFrameNode,
  CodeSurfaceNode,
  CodePositionLike,
  ImageNode,
  GridPatternValue,
  LinearGradientValue,
  RadialGradientValue,
  DragDismissNode,
  DraggableNode,
  LinkNode,
  LoopMotionNode,
  OverlayNode,
  PresenceNode,
  SpinnerNode,
  PositionedNode,
  PressableNode,
  HoverableNode,
  InFlowNode,
  InViewNode,
  SimulatedNode,
  ScrollViewNode,
  SizeValueValue,
  StackNode,
  SpacerNode,
  VectorNode,
  AdaptiveNodeValue,
  GridNode,
  AnchoredNode,
  SafeAreaNode,
  StickyNode,
  StyleDiffValue,
  TextEntryNode,
  TextNode,
  TransformValue,
  TransitionSpecValue,
  ShortcutNode,
  KeyChordValue,
  VisualNodeValue,
} from './nodes';
import { declareShortcut } from '../dom/shortcuts';
import { declareScrollViewport } from './scroll-viewports';
import { SheetKeymap } from './components/SheetKeymap';
import { CellRef as CellRefCtor } from './components/CellRef';
import { SheetRange as SheetRangeCtor } from './components/SheetRange';
import { Curve, Motion } from './design-system.generated';
import { StyleChannels } from './value-types';

/** The C# `Anchored.MotionLiftDp` twin — the open/close nudge toward the anchor. */
const ANCHOR_MOTION_LIFT = 8;

export interface LoweringContext {
  /** The theme's TextPrimary token — the default Text color (mirrors `theme.TextPrimary`). */
  textPrimary: ColorTokenValue;
  /** Passed verbatim to `component.build(...)` when expanding shared components. */
  componentContext?: unknown;
}

/**
 * The contained failure as DOM — for the seams that own a whole tree rather than a node inside one:
 * a PAGE whose own build throws has no parent to contain it, and without this the mount throws and
 * the root is never written, which is the white screen itself.
 */
export function renderContainedFailure(componentName: string, error: unknown): HtmlNode {
  reportComponentFailure(componentName, error);
  const theme = getPhotonTheme();
  return lowerVisualNode(describeComponentFailure(componentName, error, theme), {
    textPrimary: theme.textPrimary,
  });
}

/** Lowers an abstract node tree to an HtmlNode the reconciler renders. */
export function lowerVisualNode(node: VisualNodeValue, context: LoweringContext): HtmlNode {
  // Inside a reconciler pass each lowered root takes a unique, order-stable prefix so several
  // bridges on one page cannot collide on identity paths; outside a pass paths are inert.
  const rootPath = getActivePass()?.store.nextRootPath() ?? 'r';
  return (
    lowerNode(node, context, null, rootPath) ?? {
      tag: 'span',
      attributes: {},
      events: {},
      children: [],
    }
  );
}

// ---- token → CSS value formatting (mirrors C# TokenCss) ------------------------------------------

function hex(color: ColorValue): string {
  const channel = (v: number) => v.toString(16).padStart(2, '0');
  const base = `#${channel(color.r)}${channel(color.g)}${channel(color.b)}`;
  return color.a === 255 ? base : base + channel(color.a);
}

/** The same token at a fraction of its alpha — the C# `Color.WithOpacity` twin. */
function withAlpha(token: ColorTokenValue, alpha: number): ColorTokenValue {
  const fade = (color: ColorValue): ColorValue => ({
    ...color,
    a: Math.round(color.a * alpha),
  });
  return { light: fade(token.light), dark: fade(token.dark) };
}

export function tokenValue(token: ColorTokenValue): string {
  const light = hex(token.light);
  const dark = hex(token.dark);
  return light === dark ? light : `light-dark(${light}, ${dark})`;
}

/**
 * The C# `BorderSides` flags, by value: Top 1, End 2, Bottom 4, Start 8, All 15. They cross as
 * numbers because that is what a [Flags] enum transpiles to, and both sides read the same bits.
 */
const BORDER_ALL = 15;

function sidesAreAll(style: { borderSides?: number }): boolean {
  return (style.borderSides ?? BORDER_ALL) === BORDER_ALL;
}

function partialBorder(style: { borderWidth?: number; borderSides?: number }): boolean {
  const sides = style.borderSides ?? BORDER_ALL;
  return (style.borderWidth ?? 0) > 0 && sides !== BORDER_ALL && sides !== 0;
}

/** `border-width` in CSS's own order — top, right, bottom, left — from the logical sides. */
function sideWidths(width: number, sides: number): string {
  const w = px(width);
  const edge = (bit: number) => ((sides & bit) !== 0 ? w : '0');
  return `${edge(1)} ${edge(2)} ${edge(4)} ${edge(8)}`;
}

export function px(dp: number): string {
  if (dp === 0) return '0';
  // Matches C# "0.##" formatting (up to 2 decimals, no trailing zeros).
  return `${parseFloat(dp.toFixed(2))}px`;
}

/** Bare invariant number — mirrors C# TokenCss.Number ("0.####"). */
function num(value: number): string {
  return `${parseFloat(value.toFixed(4))}`;
}

/** Mirrors C# TokenCss.Transform: translate → rotate → scale, only non-neutral parts. */
function transformValue(t: TransformValue): string | undefined {
  const tx = t.translateX ?? 0;
  const ty = t.translateY ?? 0;
  const rot = t.rotationDegrees ?? 0;
  const sx = t.scaleX ?? 1;
  const sy = t.scaleY ?? 1;
  const parts: string[] = [];
  if (tx !== 0 || ty !== 0) parts.push(`translate(${px(tx)}, ${px(ty)})`);
  if (rot !== 0) parts.push(`rotate(${num(rot)}deg)`);
  if (sx !== 1 || sy !== 1) parts.push(sx === sy ? `scale(${num(sx)})` : `scale(${num(sx)}, ${num(sy)})`);
  return parts.length > 0 ? parts.join(' ') : undefined;
}

function radiusValue(radii: CornerRadiiValue): string {
  const { topLeft, topRight, bottomRight, bottomLeft } = radii;
  return topLeft === topRight && topRight === bottomRight && bottomRight === bottomLeft
    ? px(topLeft)
    : `${px(topLeft)} ${px(topRight)} ${px(bottomRight)} ${px(bottomLeft)}`;
}

function paddingValue(insets: EdgeInsetsValue): string {
  return `${px(insets.top)} ${px(insets.end)} ${px(insets.bottom)} ${px(insets.start)}`;
}

/**
 * A FIXED size does not shrink — the C# `WebRealizer.Rigid` twin. CSS disagrees by default: a flex
 * item's `flex-shrink` is 1, so a box with `width: 68px` beside an overflowing sibling is quietly
 * squeezed and everything inside it moves. Photon never does that, so neither does the web.
 * (The code editor's gutter was the symptom: line numbers slid left on exactly the lines whose code
 * ran past the viewport, and slid back when a scroll took those lines out of the window.)
 */
function rigid(
  width: SizeValueValue | undefined,
  height: SizeValueValue | undefined,
): string | undefined {
  return width?.kind === 'fixed' || height?.kind === 'fixed' ? '0' : undefined;
}

function sizeValue(size: SizeValueValue | undefined): string | undefined {
  if (!size) return undefined;
  switch (size.kind) {
    case 'fixed':
      return px(size.value);
    case 'fill':
      return '100%';
    default:
      return undefined; // hug = auto
  }
}


// ---- style string assembly — the C# HtmlStyle.ToCssString property ORDER, subset used -------------

type StyleEntries = Partial<Record<string, string | undefined>>;

// ---- node lowering --------------------------------------------------------------------------------

function lowerNode(
  node: VisualNodeValue,
  context: LoweringContext,
  horizontalAxis: boolean | null,
  path: string,
): HtmlNode | null {
  switch (node.nodeKind) {
    case 'box':
      return lowerBox(node as BoxNode, context, path);
    case 'row':
    case 'column':
      return lowerFlex(node as FlexNodeValue, context, path);
    case 'text':
      return lowerText(node as TextNode, context);
    case 'textEntry':
      return lowerTextEntry(node as TextEntryNode, context);
    case 'overlay':
      return lowerOverlay(node as OverlayNode, context, path);
    case 'pressable':
      return lowerPressable(node as PressableNode, context, path);
    case 'flexible':
      return lowerFlexible(node as FlexibleNode, context, horizontalAxis, path);
    case 'spacer':
      return lowerSpacer(node as SpacerNode, horizontalAxis);
    case 'sheetSurface':
      return lowerSheetSurface(node as unknown as SheetSurfaceLowering, context, horizontalAxis, path);
    case 'scrollView':
      return lowerScrollView(node as ScrollViewNode, context, path);
    case 'loopMotion':
      return lowerLoopMotion(node as LoopMotionNode, context, path);
    case 'presence':
      return lowerPresence(node as PresenceNode, context, horizontalAxis, path);
    case 'dragDismiss':
      return lowerDragDismiss(node as DragDismissNode, context, horizontalAxis, path);
    case 'draggable':
      return lowerDraggable(node as DraggableNode, context, horizontalAxis, path);
    case 'link':
      return lowerLink(node as LinkNode, context, path);
    case 'cameraPreview':
      return lowerCameraPreview(node as unknown as CameraPreviewNode);
    case 'webFrame':
      return lowerWebFrame(node as unknown as WebFrameNode);
    case 'codeSurface':
      return lowerCodeSurface(node as unknown as CodeSurfaceNode, context, path);
    case 'image':
      return lowerImage(node as ImageNode);
    case 'icon':
      return lowerIcon(node as IconNode);
    // C# twin: a vector IS an icon free of the §07 size whitelist, and free of the square box.
    case 'vector': {
      const vector = node as unknown as VectorNode;
      return lowerGlyph(vector.glyph, vector.size, vector.height ?? vector.size,
        vector.color ?? null, vector.label ?? null);
    }
    case 'spinner':
      return lowerSpinner(node as SpinnerNode);
    case 'stack':
      return lowerStack(node as StackNode, context, path);
    case 'grid':
      return lowerGrid(node as unknown as GridNode, context, path);
    case 'adaptive':
      return lowerAdaptive(node as unknown as AdaptiveNodeValue, context, path);
    case 'shortcut':
      return lowerShortcut(node as unknown as ShortcutNode, context, horizontalAxis, path);
    case 'sticky':
      return lowerSticky(node as unknown as StickyNode, context, path);
    case 'safeArea':
      return lowerSafeArea(node as unknown as SafeAreaNode, context, path);
    case 'anchored':
      return lowerAnchored(node as unknown as AnchoredNode, context, path);
    case 'adjustable':
      return lowerAdjustable(node as unknown as AdjustableNode, context, path);
    case 'hoverable':
      return lowerHoverable(node as unknown as HoverableNode, context, path);
    case 'simulated':
      return lowerSimulated(node as unknown as SimulatedNode, context, horizontalAxis, path);
    case 'inFlow':
      return lowerInFlow(node as unknown as InFlowNode, context, horizontalAxis, path);
    case 'inView':
      return lowerInView(node as unknown as InViewNode, context, horizontalAxis, path);
    case 'positioned':
      // Outside a Stack there is no anchor frame — degrade to the child (parity with the realizers).
      return lowerNode((node as PositionedNode).child, context, horizontalAxis, path + '/0');
    case 'component': {
      // W6 slice 2: resolve against the positional store BEFORE building — a retained instance
      // (same path + type + key) keeps its state and adopts the fresh config. Mirrors the C#
      // LayoutEngine.MeasureComponent; the build output expands at the wrapper position (path/0).
      const pass = getActivePass();
      const resolved = (
        pass ? pass.store.reconcile(path, node, pass.invalidator) : node
      ) as ComponentNode;
      // The BOUNDARY (C# ComponentBoundary twin): a component's throw costs its own subtree and
      // nothing else. Without this the mount threw, nothing reached the root, and the page was white.
      let built: unknown;
      try {
        built = resolved.build(context.componentContext);
      } catch (error) {
        const name = (resolved as { constructor?: { name?: string } }).constructor?.name ?? 'Component';
        reportComponentFailure(name, error);
        built = describeComponentFailure(name, error, getPhotonTheme());
      }
      return lowerNode(built as VisualNodeValue, context, horizontalAxis, path + '/0');
    }
    default: {
      // Mixing seam: a WEB component (transpiled shared component or low-level HtmlElement) composed
      // inside an abstract tree has no nodeKind but renders itself — embed its HtmlNode directly.
      const renderable = node as { render?: () => HtmlNode };
      if (typeof renderable.render === 'function') return renderable.render();
      return null;
    }
  }
}

function element(tag: string, style: StyleEntries, children: HtmlNode[] = []): HtmlNode {
  return {
    tag,
    attributes: atomicAttrs(style),
    events: {},
    children,
  };
}

/**
 * STYLE-SEMANTICS-PLAN §2: regular declarations become sorted atomic classes (rules memoized in the
 * registry); only the custom-property tail stays inline. `semanticClass` (eq-type-*, eq-spinner…)
 * goes FIRST — the same merge order the C# AtomizeTree uses, so hydration compares equal strings.
 */
function atomicAttrs(style: StyleEntries, semanticClass?: string): Record<string, string> {
  const atomized = atomizeEntries(style);
  const cls = semanticClass
    ? atomized.class
      ? `${semanticClass} ${atomized.class}`
      : semanticClass
    : atomized.class;
  const attributes: Record<string, string> = {};
  if (cls) attributes['class'] = cls;
  if (atomized.style) attributes['style'] = atomized.style;
  return attributes;
}

/** Prepend a semantic class to an already-atomized element (the post-element() assignment sites). */
function prependClass(node: HtmlNode, semanticClass: string): void {
  const existing = node.attributes['class'];
  node.attributes['class'] = existing ? `${semanticClass} ${existing}` : semanticClass;
}

/** Spec §06 mirror: enter motion — NO wrapper; the mount-playing animation class rides the lowered
 * child's own root (identical DOM to the C# SSR realizer, so hydration adopts it untouched). */
function lowerPresence(
  node: PresenceNode,
  context: LoweringContext,
  horizontalAxis: boolean | null,
  path: string,
): HtmlNode | null {
  const child = lowerNode(node.child, context, horizontalAxis, path + '/0');
  if (!child) return null;
  const slide = node.enter === 'slideUp';
  prependClass(child, slide ? 'eq-presence-slideup' : 'eq-presence-fade');
  // The EXIT marker — the reconciler defers this element's removal while the reverse plays.
  child.attributes['data-eq-exit'] = slide ? 'slideup' : 'fade';
  return child;
}

/** Navigation mirror: a real <a href> (the SPA router intercepts internal clicks) with UA chrome
 * neutralized by the generated .eq-link — the child owns all visuals (the Pressable contract). */
/**
 * The editable code surface. The child renders the lines; this wraps them in a focusable box and
 * paints the two marks that say where you are — a caret and a band per selected line — as absolutely
 * positioned children.
 * <p>
 * The keyboard goes straight to the transpiled `CodeKeymap`, the same function the native host
 * calls. Nothing about what a key MEANS is decided here: this file only turns a DOM event into the
 * arguments that function takes, and turns (line, column) into pixels — which with a monospaced
 * face is multiplication, not measurement.
 * </p>
 */
function lowerCodeSurface(node: CodeSurfaceNode, context: LoweringContext, path: string): HtmlNode {
  const editor = node.editor;
  const surface: HtmlNode = {
    tag: 'div',
    attributes: {
      class: 'eq-code-surface',
      tabindex: '0',
      role: 'textbox',
      'aria-multiline': 'true',
      // pre: token runs carry REAL spaces between words — HTML would collapse them.
      //
      // It does NOT scroll. It used to, and that put the code in one coordinate space and the two
      // marks below in another: the marks are absolute children of this box, so anything that
      // scrolled INSIDE it moved the text out from under them — scroll a long line sideways and the
      // caret stays behind, and a click is read at the column it would have hit unscrolled. The
      // scroll views live outside the surface now (CodeEditor), so this box travels WITH the code
      // and the arithmetic below is true wherever the file has been scrolled to.
      style: 'position:relative;outline:none;white-space:pre;',
    },
    events: {},
    children: [],
  };
  if (node.label) surface.attributes['aria-label'] = node.label;
  if (node.autofocus) surface.attributes['autofocus'] = '';
  // The surface's identity across rebuilds — every keystroke hands over a new element, so anything
  // that has to find it AFTER the render (see revealCaret) resolves by this, never by a reference.
  surface.attributes['data-eq-code'] = path;

  const child = lowerNode(node.child, context, null, path + '/0');
  if (child) surface.children.push(child);

  // The marks, under the code: a band is translucent so the text reads through it, and a caret sits
  // between glyphs where nothing covers it. Their ink comes from the NODE — an editor on an inverse
  // slab writes with an ink of its own, and the page theme's would vanish into the slab.
  const theme = getPhotonTheme();
  const caretInk = tokenValue(node.caretColor ?? theme.textPrimary);
  const selectionInk = tokenValue(withAlpha(node.selectionColor ?? theme.focusRing, SELECTION_ALPHA));
  const selection = editor.selection;
  if (!selection.isEmpty) {
    for (let line = selection.start.line; line <= selection.end.line; line++) {
      const from = line === selection.start.line ? selection.start.column : 0;
      const lineLength = editor.document.line(line).length;
      const to = line === selection.end.line ? selection.end.column : lineLength + 1;
      if (to <= from) continue;
      surface.children.push(mark(
        node.contentLeft + from * node.columnWidth,
        node.contentTop + line * node.lineHeight,
        (to - from) * node.columnWidth, node.lineHeight,
        'eq-code-selection', selectionInk, 1));
    }
  }
  const caret = selection.focus;
  surface.children.push(mark(
    node.contentLeft + caret.column * node.columnWidth,
    node.contentTop + caret.line * node.lineHeight,
    CARET_WIDTH, node.lineHeight, 'eq-code-caret', caretInk, 0));

  if (typeof document === 'undefined') return surface;   // SSR: the marks are enough

  const changed = () => node.onChanged?.();
  surface.events['keydown'] = ((event: KeyboardEvent) => {
    const modifiers =
      (event.shiftKey ? 1 : 0) | (event.altKey ? 2 : 0) |
      ((event.metaKey || event.ctrlKey) ? 4 : 0);
    if (handleCodeKey(editor, event.key, modifiers)) {
      event.preventDefault();
      changed();
      revealCaret(path);
      return;
    }
    // A printable character is TEXT, not a command — and what a keystroke produces is the
    // browser's business, which is why it arrives as a string rather than a key name.
    if (event.key.length === 1 && !event.metaKey && !event.ctrlKey) {
      if (editor.type(event.key)) {
        event.preventDefault();
        changed();
        revealCaret(path);
      }
    }
  }) as unknown as EventHandler;

  surface.events['pointerdown'] = ((event: PointerEvent) => {
    const box = (event.currentTarget as HTMLElement).getBoundingClientRect();
    const at = positionIn(node, event.clientX - box.left, event.clientY - box.top);
    if (event.detail >= 3) editor.selectLine(at.line);
    else if (event.detail === 2) editor.selectWord(at);
    else (editor as unknown as { selection: unknown }).selection = { anchor: at, focus: at };
    (event.currentTarget as HTMLElement).focus();
    changed();
  }) as unknown as EventHandler;

  return surface;
}

/**
 * Brings the caret back into the viewport after a key moved it.
 *
 * Arrowing off the bottom of a long file, or right along a line wider than the box, used to leave
 * the caret exactly where the arithmetic put it — outside the visible rectangle. You kept typing
 * into a place you could not see, and the only way back was the scrollbar.
 *
 * After the render, not during it: the key mutates the controller, the app re-renders, and only then
 * is there a caret element at the new position to scroll to. `nearest` on both axes scrolls the
 * least that works — an editor already fully in view does not drag the page with it.
 */
function revealCaret(path: string): void {
  // Scrolling is a courtesy; a keystroke is not. An environment that cannot schedule skips it.
  if (typeof document === 'undefined') return;

  const reveal = () => {
    // Checked again, HERE. The whole point of this callback is that it runs later, and later is
    // long enough for the document to be gone — a test environment torn down between files, a
    // frame delivered after teardown. A deferred callback that assumes its world outlived it
    // throws where nothing can catch it: two unhandled ReferenceErrors, in CI only.
    if (typeof document === 'undefined') return;
    // By PATH, never by a captured element. The keystroke rebuilds the tree, so the surface the
    // handler ran on is a corpse by the time this runs — and scrollIntoView on a detached caret
    // succeeds silently, which is how this looked implemented and did nothing.
    const caret = document.querySelector(`[data-eq-code="${path}"] .eq-code-caret`);
    caret?.scrollIntoView({ block: 'nearest', inline: 'nearest' });
  };

  // AFTER the render, and the render is scheduled on an animation frame — so a microtask (or this
  // frame's) would look for a caret that has not moved yet, find the old one, and correctly decide
  // it is already on screen. The frame after the flush is the first one where the caret is where
  // the keystroke put it.
  //
  // BOTH routes, for the reason the render scheduler itself keeps both: a hidden or throttled tab
  // stops delivering frames, and the render still happens on the timeout backstop. Scheduling only
  // on a frame is how this was written first, and in a tab that never painted it simply never ran.
  // Revealing twice is free — the second call finds the caret already on screen.
  if (typeof requestAnimationFrame === 'function') {
    requestAnimationFrame(() => requestAnimationFrame(reveal));
  }
  if (typeof setTimeout === 'function') setTimeout(reveal, 48);
}

/**
 * The stacking PLANES, because CSS gives you one number and a page needs three (C# `ChromeLayer`).
 *
 * CONTENT is elevation 1–5 — the design system's own scale, and the only one an author names.
 * CHROME is above all of it: a pinned header is not a raised card. Photon needs none of this, since
 * paint order IS child order there.
 */
const CHROME_LAYER = '100';

/** The caret's width in px — the C# `PhotonRealizer.CaretWidth` twin. */
const CARET_WIDTH = 2;

/** How much of the selection band shows through — the C# `PhotonRealizer.SelectionAlpha` twin. */
const SELECTION_ALPHA = 0.28;

/**
 * One absolutely-positioned rectangle: a caret, or one line's band.
 *
 * The ink is painted HERE, from the node's own colours. It used to be left to a stylesheet class
 * that nobody ever wrote, so both marks were rendered — correct geometry, correct size — and filled
 * with nothing: the caret existed at the right place in the DOM and was invisible, which is the
 * worst of both worlds, because you cannot see where you are typing and nothing looks broken.
 */
function mark(
  left: number,
  top: number,
  width: number,
  height: number,
  className: string,
  ink: string,
  radius: number,
): HtmlNode {
  return {
    tag: 'div',
    attributes: {
      class: className,
      style: `position:absolute;left:${left}px;top:${top}px;width:${width}px;height:${height}px;`
        + `background-color:${ink};`
        + (radius > 0 ? `border-radius:${radius}px;` : '')
        + 'pointer-events:none;',
    },
    events: {},
    children: [],
  };
}

/** The (line, column) a point lands on. Division, not a search — the face is monospaced. */
function positionIn(node: CodeSurfaceNode, x: number, y: number): CodePositionLike {
  const line = Math.floor((y - node.contentTop) / node.lineHeight);
  const column = Math.round((x - node.contentLeft) / node.columnWidth);
  return node.editor.document.clamp({ line: Math.max(0, line), column: Math.max(0, column) });
}

/**
 * The transpiled keymap — the SAME function the native host calls. Imported directly: the module
 * cycle it closes (lowering → CodeKeymap → runtime-exports → components) is the benign kind,
 * because a keydown handler dereferences the binding when a key is pressed, not while the module
 * is evaluating. Reading it off a global instead looked safer and was simply dead: nothing ever
 * assigned one, so the web editor took no keys at all.
 */
function handleCodeKey(editor: unknown, key: string, modifiers: number): boolean {
  return CodeKeymap.handle(editor as never, key, modifiers, webClipboard());
}

/**
 * ⌘C/⌘X/⌘V inside the editor. The browser's async clipboard cannot answer a keydown in time, so
 * this is the SYNCHRONOUS half — the copy it does works, and a paste falls back to the browser's
 * own `paste` event on the surface.
 */
let clipboardText: string | null = null;
function webClipboard() {
  return {
    read: () => clipboardText,
    write: (text: string) => {
      clipboardText = text;
      void navigator?.clipboard?.writeText?.(text)?.catch(() => {});
    },
  };
}

function lowerLink(node: LinkNode, context: LoweringContext, path: string): HtmlNode {
  const anchor: HtmlNode = {
    tag: 'a',
    attributes: { class: 'eq-link', href: node.destination },
    events: {},
    children: [],
  };
  if (node.label) anchor.attributes['aria-label'] = node.label;
  // Read by the router when it decides where the new page starts (C# twin). On the anchor because
  // that is what the router's one delegated click listener meets.
  if (node.keepsPosition) anchor.attributes['data-eq-keep-position'] = '';
  // WARM ON HOVER (C# twin): app-internal destinations only — an absolute URL is somebody else's.
  if (node.destination.startsWith('/')) anchor.attributes['data-prefetch'] = '';
  const child = lowerNode(node.child, context, null, path + '/0');
  if (child) anchor.children.push(child);
  return anchor;
}

/** Gestures v2 mirror: the drag marker + dismiss event ride the child's own root (no wrapper —
 * identical DOM to the C# SSR realizer). The pointer-capture controller installs lazily on the
 * first lowering: it drives the follow/glide and dispatches `eq-drag-dismiss` past the threshold,
 * which the reconciler-attached handler resolves into the component's onDismiss. */
/**
 * C# twin: the gesture's RULES travel as data and one controller does the tracking. The rest offset
 * is a plain transform, so a row that is already open renders open before any script has run.
 */
function lowerDraggable(
  node: DraggableNode,
  context: LoweringContext,
  horizontalAxis: boolean | null,
  path: string,
): HtmlNode | null {
  const child = lowerNode(node.child, context, horizontalAxis, path + '/0');
  if (!child) return null;

  const horizontal = node.axis === 'horizontal';
  const rest = node.restOffset ?? 0;
  child.attributes['data-eq-drag'] = horizontal ? 'x' : 'y';
  child.attributes['data-eq-drag-min'] = String(node.min ?? 0);
  child.attributes['data-eq-drag-max'] = String(node.max ?? 0);
  child.attributes['data-eq-drag-rest'] = String(rest);
  if (node.normalized) child.attributes['data-eq-drag-normalized'] = '1';
  if (node.follows === false) child.attributes['data-eq-drag-follows'] = '0';
  if (node.onMoved) child.attributes['data-eq-drag-moves'] = '1';

  if (rest !== 0 && node.follows !== false) {
    const shift = horizontal ? `translateX(${rest}px)` : `translateY(${rest}px)`;
    const existing = child.attributes.style ? `${child.attributes.style};` : '';
    child.attributes.style = `${existing}transform:${shift};transition:transform ${Motion.baseMs}ms`;
  }

  if (node.onReleased) {
    const released = node.onReleased;
    child.events['eq-drag-released'] = ((ev: Event) => {
      released((ev as CustomEvent<number>).detail ?? 0);
    }) as EventHandler;
  }

  if (node.onMoved) {
    const moved = node.onMoved;
    child.events['eq-drag-moved'] = ((ev: Event) => {
      moved((ev as CustomEvent<number>).detail ?? 0);
    }) as EventHandler;
  }

  installDraggableController();
  return child;
}

function lowerDragDismiss(
  node: DragDismissNode,
  context: LoweringContext,
  horizontalAxis: boolean | null,
  path: string,
): HtmlNode | null {
  const child = lowerNode(node.child, context, horizontalAxis, path + '/0');
  if (!child) return null;
  child.attributes['data-eq-drag-dismiss'] = '96'; // DragDismiss.ThresholdDp — cross-pinned
  if (node.onDismiss) {
    child.events['eq-drag-dismiss'] = node.onDismiss as EventHandler;
  }
  installDragDismissController();
  return child;
}

/** Phase C mirror: the generated fixed inset-0 stacking layer (.eq-overlay). */
function lowerOverlay(node: OverlayNode, context: LoweringContext, path: string): HtmlNode {
  const layer: HtmlNode = {
    tag: 'div',
    attributes: {
      class: node.modal === false ? 'eq-overlay eq-overlay-passthrough' : 'eq-overlay',
    },
    events: {},
    children: [],
  };
  // §10 dialog semantics + the trap marker (C# twin: LowerOverlay). A modal layer that is OPEN is
  // a dialog: aria-modal is what tells assistive tech the page behind is inert, tabindex=-1 makes
  // the container itself the initial-focus fallback, and data-eq-trap is what the client's
  // focus-trap controller discovers after each pass. A non-modal layer (toasts) is none of this,
  // and a closed one is not a dialog RIGHT NOW — it is hidden, out of hit-testing and focus.
  const modalAndOpen = node.modal !== false && node.open !== false;
  if (modalAndOpen) {
    layer.attributes['role'] = 'dialog';
    layer.attributes['aria-modal'] = 'true';
    layer.attributes['tabindex'] = '-1';
    layer.attributes['data-eq-trap'] = '';
  }
  if (node.motion) {
    // Keep-mounted open/close (C# twin): the layer fades both ways; closed is hidden, so it leaves
    // hit-testing and the focus order.
    appendDeclarations(layer, {
      transition: motionTransition(node.motion, false),
      ...(node.open === false
        ? { opacity: '0', visibility: 'hidden', 'pointer-events': 'none' }
        : {}),
    });
  }
  const child = lowerNode(node.child, context, null, path + '/0');
  if (child) layer.children.push(child);
  return layer;
}

/** Spec B9/B10 mirror: the REAL chrome-less <input> — identical DOM to the C# SSR realizer, plus
 * the client-only handlers (input → onChanged, Enter → onSubmit, focus/blur → onFocusChanged). */
function lowerTextEntry(node: TextEntryNode, context: LoweringContext): HtmlNode {
  // C# twin: >1 line IS a textarea — its value is CONTENT, not an attribute.
  const lines = node.lines ?? 1;
  const multiline = lines > 1;
  const input = element(multiline ? 'textarea' : 'input', {
    width: '100%',
    padding: '0',
    background: 'none',
    border: 'none',
    color: tokenValue(context.textPrimary),
    'font-family': 'inherit',
    ...(multiline ? { resize: 'vertical' } : {}),
  });
  prependClass(input, `eq-entry eq-type-${node.role.toLowerCase()}`);
  if (multiline) {
    input.attributes['rows'] = String(lines);
    if (node.value.length > 0) input.children = [textLeaf(node.value)];
  } else {
    input.attributes['type'] = node.obscure === true ? 'password' : 'text';
    input.attributes['value'] = node.value;
  }
  if (node.placeholder != null) input.attributes['placeholder'] = node.placeholder;
  // The accessible NAME — a placeholder vanishes under text, so it never substitutes for one.
  if (node.label) input.attributes['aria-label'] = node.label;
  // C# twin: the attribute marks intent; the client also FOCUSES on mount — a dialog opened later
  // never gets the browser's parse-time autofocus.
  if (node.autofocus === true) {
    input.attributes['autofocus'] = '';
    input.events['eq:mounted'] = ((element: HTMLElement) => element.focus()) as unknown as EventHandler;
  }
  if (node.disabled === true) {
    input.attributes['disabled'] = '';
  } else {
    const onChanged = node.onChanged;
    if (onChanged) {
      // The reconciler's input/change convention: the wrapper extracts the element value and calls
      // the handler with it — exactly the C# Action<string> shape.
      input.events['input'] = onChanged as unknown as EventHandler;
    }
    const onSubmit = node.onSubmit;
    if (onSubmit) {
      input.events['keydown'] = ((e: KeyboardEvent) => {
        if (e.key === 'Enter') onSubmit();
      }) as unknown as EventHandler;
    }
    const onFocusChanged = node.onFocusChanged;
    if (onFocusChanged) {
      input.events['focus'] = (() => onFocusChanged(true)) as EventHandler;
      input.events['blur'] = (() => onFocusChanged(false)) as EventHandler;
    }
  }
  if (node.invalid === true) input.attributes['aria-invalid'] = 'true';

  // C# twin: the STABLE .eq-field shell — input + the sr-only description twin, present (empty)
  // from the first paint so a later error swap ANNOUNCES, and so the shape never flips (a
  // presence-flip would replace the <input> and drop focus mid-edit). The id is the content's
  // FNV hash — the atomizer's identity rule, deterministic across SSR and client.
  const description = element('span', {});
  prependClass(description, 'eq-desc');
  description.attributes['aria-live'] = 'polite';
  if (node.description) {
    const descriptionId = `eq-desc-${hashDeclaration(node.description)}`;
    description.attributes['id'] = descriptionId;
    description.children.push(textLeaf(node.description));
    input.attributes['aria-describedby'] = descriptionId;
  }
  const host = element('div', {});
  prependClass(host, 'eq-field');
  host.children.push(input, description);
  return host;
}

/** Mirror of C# TokenCss.Gradient: linear-gradient with light-dark() stops; a `via` midpoint
 * lands as the third stop at its percentage position. */
function gradientValue(gradient: LinearGradientValue): string {
  const direction =
    gradient.direction === 'toBottom'
      ? 'to bottom'
      : gradient.direction === 'toBottomRight'
        ? 'to bottom right'
        : gradient.direction === 'toBottomLeft'
          ? 'to bottom left'
          : 'to right';
  const via = gradient.via
    ? ` ${tokenValue(gradient.via)} ${num((gradient.viaPosition ?? 0.5) * 100)}%,`
    : '';
  return `linear-gradient(${direction}, ${tokenValue(gradient.from)},${via} ${tokenValue(gradient.to)})`;
}

/** Mirror of C# TokenCss.Bezier. */
function bezier(easing: readonly number[] | undefined): string {
  const c = easing ?? Curve.standard;
  return `cubic-bezier(${num(c[0])}, ${num(c[1])}, ${num(c[2])}, ${num(c[3])})`;
}

/**
 * Mirror of C# TokenCss.Transition (spec S6) — one entry per covered channel, all sharing the
 * duration/easing/delay. The channel → property mapping is NORMATIVE and must stay byte-identical
 * to the C# side: SSR emits these declarations and hydration re-derives them, so class identity
 * (and therefore a repaint-free handover) depends on the exact string.
 */
function transitionValue(spec: TransitionSpecValue): string {
  const timing =
    `${num(spec.durationMs ?? Motion.baseMs)}ms ${bezier(spec.easing)}` +
    (spec.delayMs && spec.delayMs > 0 ? ` ${num(spec.delayMs)}ms` : '');
  const properties: string[] = [];
  const channels = spec.channels ?? 0;
  if (channels & StyleChannels.colors)
    properties.push('background-color', 'background-image', 'border-color', 'color', 'fill', 'stroke');
  if (channels & StyleChannels.opacity) properties.push('opacity');
  if (channels & StyleChannels.transform) properties.push('transform');
  if (channels & StyleChannels.shadow) properties.push('box-shadow');
  if (channels & StyleChannels.filters) properties.push('filter', 'backdrop-filter');
  if (channels & StyleChannels.size) properties.push('width', 'height', 'max-width', 'max-height');
  return properties.map((property) => `${property} ${timing}`).join(', ');
}

/** The C# MotionTransition twin: the Anchored open/close list. `visibility` always rides along —
 * it flips at the fade's end on exit and its start on enter, which is what parks a closed panel
 * outside hit-testing and the focus order without any JS. */
function motionTransition(motion: TransitionSpecValue, withTransform: boolean): string {
  const timing = `${num(motion.durationMs ?? Motion.baseMs)}ms ${bezier(motion.easing)}`;
  return withTransform
    ? `opacity ${timing}, transform ${timing}, visibility ${timing}`
    : `opacity ${timing}, visibility ${timing}`;
}

/** Mirror of C# TokenCss.Glow: the elliptical spotlight as a CSS radial-gradient(). */
function glowValue(glow: RadialGradientValue): string {
  return (
    `radial-gradient(${px(glow.radiusX)} ${px(glow.radiusY)} at ` +
    `${num(glow.centerX * 100)}% ${num(glow.centerY * 100)}%, ` +
    `${tokenValue(glow.from)}, ${tokenValue(glow.to)})`
  );
}

/** Mirror of C# TokenCss.GridPattern: the two repeating hairline layers. */
function gridPatternValue(pattern: GridPatternValue): string {
  const color = tokenValue(pattern.color);
  const line = px(pattern.lineWidth);
  return (
    `linear-gradient(to right, ${color} ${line}, transparent ${line}), ` +
    `linear-gradient(to bottom, ${color} ${line}, transparent ${line})`
  );
}

/** Mirror of C# TokenCss.GridPatternSize: one size entry per grid layer. */
function gridPatternSize(pattern: GridPatternValue): string {
  const cell = px(pattern.cell);
  return `${cell} ${cell}, ${cell} ${cell}`;
}

/** Fraction → CSS percentage, mirroring C# TokenCss.Percent ("0.##": -0.35 → "-35%"). */
function pct(fraction: number): string {
  return `${parseFloat((fraction * 100).toFixed(2))}%`;
}

/** Spec §06 mirror: the generated eq-slide-x keyframes animate the wrapper; endpoints ride custom
 * properties at the style TAIL (the C# cross-pin), duration rides the animation shorthand. */
function lowerLoopMotion(node: LoopMotionNode, context: LoweringContext, path: string): HtmlNode {
  const wrapper = element('div', {
    animation: `eq-slide-x ${node.durationMs}ms linear infinite`,
    '--eq-loop-from': pct(node.fromX),
    '--eq-loop-to': pct(node.toX),
  });
  prependClass(wrapper, node.hideAtRest === true ? 'eq-loop eq-loop-rest-hidden' : 'eq-loop');
  const child = lowerNode(node.child, context, null, path + '/0');
  if (child) wrapper.children.push(child);
  return wrapper;
}

/** Spec A6 mirror: native browser scrolling — overflow auto on the axis, hidden on the cross. */
interface SheetSurfaceLowering {
  child: VisualNodeValue;
  controller: import('./components/SheetController').SheetController;
  firstRow?: number;
  firstCol?: number;
  headerWidth?: number;
  headerHeight?: number;
  onChanged?: (() => void) | null;
  label?: string | null;
}

/**
 * The spreadsheet's web half: a focusable region whose keyboard goes through the SHARED
 * SheetKeymap (the same transpiled code the native host calls — the two cannot drift), whose
 * clicks resolve cells by the same prefix-sum arithmetic, and whose clipboard speaks TSV through
 * the browser's own copy/cut/paste events (no permissions dance — the user's ⌘C is the grant).
 */
function lowerSheetSurface(
  node: SheetSurfaceLowering,
  context: LoweringContext,
  horizontalAxis: boolean | null,
  path: string,
): HtmlNode {
  const child = lowerNode(node.child, context, horizontalAxis, path + '/0');
  // user-select off: a drag on the grid EXTENDS the sheet selection — the browser's native text
  // sweep would paint blue over the cells and fight the band.
  const view = element('div', { outline: 'none', 'user-select': 'none', '-webkit-user-select': 'none' }, child ? [child] : []);
  view.attributes['tabindex'] = '0';
  view.attributes['role'] = 'grid';
  if (node.label) view.attributes['aria-label'] = node.label;

  const sheet = node.controller;
  const changed = () => node.onChanged?.();

  const cellFor = (target: HTMLElement, clientX: number, clientY: number): CellRefCtor => {
    const box = target.getBoundingClientRect();
    const gridX = clientX - box.left - (node.headerWidth ?? 0);
    const gridY = clientY - box.top - (node.headerHeight ?? 0);
    const doc = sheet.document;
    let col = node.firstCol ?? 0;
    let acc = 0;
    while (col < doc.cols - 1 && acc + doc.colWidth(col) <= gridX) {
      acc += doc.colWidth(col);
      col++;
    }
    let row = node.firstRow ?? 0;
    acc = 0;
    while (row < doc.rows - 1 && acc + doc.rowHeight(row) <= gridY) {
      acc += doc.rowHeight(row);
      row++;
    }
    return new CellRefCtor(Math.max(0, Math.min(row, doc.rows - 1)), Math.max(0, Math.min(col, doc.cols - 1)));
  };
  const cellAt = (event: MouseEvent): CellRefCtor =>
    cellFor(event.currentTarget as HTMLElement, event.clientX, event.clientY);

  // The bottom-right corner of the selection's bottom-right CELL — where the fill handle sits.
  // The inverse of cellFor, in client coordinates.
  const fillHandleCorner = (target: HTMLElement): { x: number; y: number } => {
    const box = target.getBoundingClientRect();
    const doc = sheet.document;
    let x = box.left + (node.headerWidth ?? 0);
    for (let c = node.firstCol ?? 0; c <= sheet.selection.rightCol; c++) x += doc.colWidth(c);
    let y = box.top + (node.headerHeight ?? 0);
    for (let r = node.firstRow ?? 0; r <= sheet.selection.bottomRow; r++) y += doc.rowHeight(r);
    return { x, y };
  };

  view.events['keydown'] = ((event: KeyboardEvent) => {
    const command = event.metaKey || event.ctrlKey;
    // ⌘C/⌘X/⌘V fall through to the browser's copy/cut/paste events below — intercepting the
    // keydown would starve them of clipboardData.
    if (command && (event.key === 'c' || event.key === 'x' || event.key === 'v')) return;
    if (SheetKeymap.handle(sheet, event.key, command, event.shiftKey)) {
      event.preventDefault();
      changed();
      return;
    }
    if (!command && event.key.length === 1) {
      SheetKeymap.type(sheet, event.key);
      event.preventDefault();
      changed();
    }
  }) as EventHandler;

  view.events['mousedown'] = ((event: MouseEvent) => {
    // The keyboard follows the click: focus explicitly — relying on the browser's focus-on-click
    // left the keydown handler deaf on some engines.
    const target = event.currentTarget as HTMLElement;
    target.focus();
    if (sheet.editing) {
      sheet.commitEdit();   // clicking away lands the draft, like Excel
    }

    // Excel's little square: a grab near the selection's bottom-right corner starts a FILL drag.
    const corner = fillHandleCorner(target);
    const onFillHandle =
      Math.abs(event.clientX - corner.x) <= 8 && Math.abs(event.clientY - corner.y) <= 8;
    if (onFillHandle) {
      sheet.beginFill();
      const move = (ev: MouseEvent) => {
        sheet.updateFill(cellFor(target, ev.clientX, ev.clientY));
        changed();
      };
      const up = () => {
        document.removeEventListener('mousemove', move);
        document.removeEventListener('mouseup', up);
        sheet.commitFill();
        changed();
      };
      document.addEventListener('mousemove', move);
      document.addEventListener('mouseup', up);
      event.preventDefault();
      changed();
      return;
    }

    const cell = cellAt(event);
    if (event.shiftKey) {
      sheet.selectTo(cell);   // the anchor stands, the selection stretches to the click
      changed();
      return;
    }
    sheet.selection = new SheetRangeCtor(cell);

    // Drag-select: the anchor stays where the press landed, the focus follows the pointer —
    // document-level listeners so the sweep survives leaving the grid.
    const move = (ev: MouseEvent) => {
      const at = cellFor(target, ev.clientX, ev.clientY);
      sheet.selection = new SheetRangeCtor(sheet.selection.anchor, at);
      changed();
    };
    const up = () => {
      document.removeEventListener('mousemove', move);
      document.removeEventListener('mouseup', up);
    };
    document.addEventListener('mousemove', move);
    document.addEventListener('mouseup', up);
    changed();
  }) as EventHandler;

  view.events['dblclick'] = ((event: MouseEvent) => {
    const cell = cellAt(event);
    sheet.beginEdit(sheet.document.getCell(cell));
    changed();
  }) as EventHandler;

  view.events['copy'] = ((event: ClipboardEvent) => {
    event.clipboardData?.setData('text/plain', sheet.copyTsv());
    event.preventDefault();
  }) as EventHandler;

  view.events['cut'] = ((event: ClipboardEvent) => {
    event.clipboardData?.setData('text/plain', sheet.copyTsv());
    sheet.clearSelection();
    event.preventDefault();
    changed();
  }) as EventHandler;

  view.events['paste'] = ((event: ClipboardEvent) => {
    const text = event.clipboardData?.getData('text/plain');
    if (text) {
      sheet.pasteTsv(text);
      changed();
    }
    event.preventDefault();
  }) as EventHandler;

  return view;
}

function lowerScrollView(node: ScrollViewNode, context: LoweringContext, path: string): HtmlNode {
  const children: HtmlNode[] = [];
  const child = lowerNode(node.child, context, null, path + '/0');
  if (child) children.push(child);
  const horizontal = node.axis === 'horizontal';
  const view = element(
    'div',
    {
      width: sizeValue(node.width),
      // A scroll view IS the window its parent gives it — never its content (native parity: the
      // realizer hands it layout bounds and clips always). Auto height would grow with content
      // and scroll nothing.
      height: sizeValue(node.height) ?? '100%',
      // …and never PUSHED wider than it either: a flex item's min-width defaults to auto, so one
      // long line grew the scroller and every ancestor instead of scrolling (C# twin).
      'min-width': '0',
      'min-height': '0',
      'max-width': '100%',
      'overflow-y': horizontal ? 'hidden' : 'auto',
      'overflow-x': node.axis === 'vertical' ? 'hidden' : 'auto',
    },
    children,
  );
  // The out-channels a WINDOWED LIST lives on (the native realizer has carried them all along):
  // the scroll event reports where the view is; the after-pass sweep reports how tall the
  // viewport turned out to be (and applies the initial offset once, on adoption — the browser
  // owns the position after that).
  if (node.onScrolled) {
    const onScrolled = node.onScrolled;
    view.events['scroll'] = ((event: Event) => {
      const target = event.target as HTMLElement;
      onScrolled(horizontal ? target.scrollLeft : target.scrollTop);
    }) as EventHandler;
  }
  if (node.onViewportChanged || node.offset) {
    view.attributes['data-eq-scroll'] = path;
    declareScrollViewport(path, {
      horizontal,
      offset: node.offset,
      onViewportChanged: node.onViewportChanged ?? undefined,
    });
  }
  return view;
}

/** Spec A11 mirror: explicitly sized <img> with object-fit and the rrect clip. */
/**
 * The live surface (C# twin: PhotonRealizer.EmitCameraPreview). Without a session it is the SAME
 * SurfaceSubtle placeholder Image degrades to — both realizers must agree, or hydration does not.
 * With one, a muted autoplaying video the after-pass sweep wires to its MediaStream by id.
 */
function lowerCameraPreview(node: CameraPreviewNode): HtmlNode {
  const radius = node.cornerRadius;
  const hasRadius =
    !!radius &&
    (radius.topLeft > 0 || radius.topRight > 0 || radius.bottomRight > 0 || radius.bottomLeft > 0);
  const sizing = atomicAttrs({
    width: px(node.width),
    height: px(node.height),
    'border-radius': hasRadius && radius ? radiusValue(radius) : undefined,
    'object-fit': 'cover',
    background: 'var(--eq-color-surface-subtle)',
  });

  if (!node.session) {
    return { tag: 'div', attributes: { ...sizing }, events: {}, children: [] };
  }
  return {
    tag: 'video',
    attributes: {
      ...sizing,
      'data-eq-camera': node.session.id,
      autoplay: '',
      muted: '',
      playsinline: '',
      'aria-label': node.alt ?? '',
    },
    events: {},
    children: [],
  };
}

/**
 * A sandboxed iframe (C# twin: WebRealizer.LowerWebFrame). `sandbox` is ALWAYS emitted — empty
 * means fully locked — with one `allow-` token per set flag. Inline document wins over source;
 * the border is the frame's own 1990s default, so it goes.
 */
function lowerWebFrame(node: WebFrameNode): HtmlNode {
  const tokens: string[] = [];
  if (node.sandbox & 1) tokens.push('allow-scripts');
  if (node.sandbox & 2) tokens.push('allow-same-origin');
  if (node.sandbox & 4) tokens.push('allow-forms');
  if (node.sandbox & 8) tokens.push('allow-popups');

  const radius = node.cornerRadius;
  const hasRadius =
    !!radius &&
    (radius.topLeft > 0 || radius.topRight > 0 || radius.bottomRight > 0 || radius.bottomLeft > 0);

  const attributes: Record<string, string> = {
    ...atomicAttrs({
      width: sizeValue(node.width),
      height: sizeValue(node.height),
      'flex-shrink': rigid(node.width, node.height),
      border: '0',
      display: 'block',
      'border-radius': hasRadius && radius ? radiusValue(radius) : undefined,
    }),
    sandbox: tokens.join(' '),
    title: node.title ?? '',
  };
  if (node.document) attributes['srcdoc'] = node.document;
  else if (node.source) attributes['src'] = node.source;

  return { tag: 'iframe', attributes, events: {}, children: [] };
}

function lowerImage(node: ImageNode): HtmlNode {
  const fit = node.fit === 'contain' ? 'contain' : node.fit === 'stretch' ? 'fill' : 'cover';
  const radius = node.cornerRadius;
  const hasRadius =
    !!radius &&
    (radius.topLeft > 0 || radius.topRight > 0 || radius.bottomRight > 0 || radius.bottomLeft > 0);
  const sizing = atomicAttrs({
    width: px(node.width),
    height: px(node.height),
    'border-radius': hasRadius && radius ? radiusValue(radius) : undefined,
    'object-fit': fit,
  });
  const light: HtmlNode = {
    tag: 'img',
    attributes: { ...sizing, src: node.source, alt: node.alt ?? '' },
    events: {},
    children: [],
  };
  if (!node.darkSource) return light;

  // A PAIR of artworks (C# twin): both ship, the generated .eq-themed-* rules show one — the OS
  // preference by default, the app's forced data-theme when it set one.
  light.attributes['class'] = light.attributes['class']
    ? `${light.attributes['class']} eq-themed-light`
    : 'eq-themed-light';
  const dark: HtmlNode = {
    tag: 'img',
    attributes: { ...sizing, class: 'eq-themed-dark', src: node.darkSource, alt: node.alt ?? '' },
    events: {},
    children: [],
  };
  return {
    tag: 'span',
    attributes: { ...atomicAttrs({ display: 'contents' }) },
    events: {},
    children: [light, dark],
  };
}

/** Spec B15 mirror: 8 rrect bars in the 16 viewBox, phase stagger via per-bar negative
 * animation-delays over the generated 800ms fade — byte parity with the C# SSR realizer. */
function lowerSpinner(node: SpinnerNode): HtmlNode {
  const svg: HtmlNode = {
    tag: 'svg',
    attributes: {
      ...atomicAttrs(
        {
          width: px(node.size),
          height: px(node.size),
          color: node.color ? tokenValue(node.color) : undefined,
        },
        'eq-spinner',
      ),
      viewBox: '0 0 16 16',
      fill: 'currentColor',
      'aria-hidden': 'true',
    },
    events: {},
    children: [],
  };
  for (let i = 0; i < 8; i++) {
    svg.children.push({
      tag: 'rect',
      attributes: {
        style: `animation-delay: -${i * 100}ms`,
        x: '7',
        y: '0',
        width: '2',
        height: '5',
        rx: '1',
        transform: `rotate(${i * 45} 8 8)`,
      },
      events: {},
      children: [],
    });
  }
  return svg;
}

/** Spec A10 mirror: inline SVG, registry path, fill=currentColor riding the color token. */
function lowerIcon(node: IconNode): HtmlNode {
  return lowerGlyph(node.glyph, node.size, node.size, node.color ?? null, node.label ?? null);
}

/** The shape both an icon and a vector draw as — the C# `LowerGlyph`, box included. */
function lowerGlyph(
  glyph: IconGlyphValue,
  width: number,
  height: number,
  color: ColorTokenValue | null,
  label: string | null,
): HtmlNode {
  const attributes: Record<string, string | undefined> = {
    ...atomicAttrs({
      // Block, not inline — the C# realizer says why; the two lowerings must agree byte-for-byte.
      display: 'block',
      width: px(width),
      height: px(height),
      color: color ? tokenValue(color) : undefined,
    }),
    viewBox: glyph.viewBox ?? '0 0 24 24',
  };
  // Fill glyphs are alpha masks; stroke glyphs are the outline family (2dp round — spec §07).
  if (glyph.style === 'stroke') {
    attributes['fill'] = 'none';
    attributes['stroke'] = 'currentColor';
    attributes['stroke-width'] = String(glyph.strokeWidth ?? 2);
    attributes['stroke-linecap'] = 'round';
    attributes['stroke-linejoin'] = 'round';
  } else {
    attributes['fill'] = 'currentColor';
  }
  if (label) attributes['aria-label'] = label;
  else attributes['aria-hidden'] = 'true';

  return {
    tag: 'svg',
    attributes,
    events: {},
    children: [{ tag: 'path', attributes: { d: glyph.path }, events: {}, children: [] }],
  };
}

/** Spec A3 mirror of the C# WebRealizer: single-cell grid stack + absolute Positioned anchors. */
function lowerStack(node: StackNode, context: LoweringContext, path: string): HtmlNode {
  const alignIndex: Record<string, number> = {
    topStart: 0,
    topCenter: 1,
    topEnd: 2,
    centerStart: 3,
    center: 4,
    centerEnd: 5,
    bottomStart: 6,
    bottomCenter: 7,
    bottomEnd: 8,
  };
  const index = alignIndex[node.align] ?? 0;
  const flexWord = (part: number) =>
    part === 1 ? 'center' : part === 2 ? 'flex-end' : 'flex-start';
  const cellJustify = flexWord(index % 3);
  const cellAlign = flexWord(Math.trunc(index / 3));

  const children: HtmlNode[] = [];
  const stackChildren = node.children ?? [];
  for (let i = 0; i < stackChildren.length; i++) {
    // Through the COMPONENT to what it builds, exactly as the C# realizer does — `Positioned` is a
    // contract with the parent, and one returned by a component was not recognised. Resolved ONCE,
    // through the store, so the instance that builds here is the instance that stays.
    const child = resolveStackChild(stackChildren[i], context, path + '/' + i);
    if (child?.nodeKind === 'positioned') {
      const positioned = child as PositionedNode;
      const lowered = lowerNode(positioned.child, context, null, path + '/' + i + '/0');
      if (!lowered) continue;
      // An absolutely-positioned box with ONE edge shrink-wraps; with two it spans between them.
      // Native measures a positioned child against the stack's full extent, so a child that FILLS
      // is the stack's width there and its own content's width here — the same tree, two
      // geometries (C# twin).
      const filling = fills(positioned.child);
      const spanX = filling.width && (positioned.start == null) !== (positioned.end == null);
      const spanY = filling.height && (positioned.top == null) !== (positioned.bottom == null);
      children.push(
        element(
          'div',
          {
            position: 'absolute',
            // Spec S7 (C# twin): explicit stacking WINS; otherwise the child's own depth.
            'z-index': `${(positioned.zIndex ?? 0) !== 0 ? positioned.zIndex : i + 1}`,
            top: positioned.top != null ? px(positioned.top) : spanY ? '0' : undefined,
            right: positioned.end != null ? px(positioned.end) : spanX ? '0' : undefined,
            bottom: positioned.bottom != null ? px(positioned.bottom) : spanY ? '0' : undefined,
            left: positioned.start != null ? px(positioned.start) : spanX ? '0' : undefined,
          },
          [lowered],
        ),
      );
    } else if (child) {
      const lowered = lowerNode(child, context, null, path + '/' + i);
      if (!lowered) continue;
      // The cell IS the stack's available space (native MeasureStack contract): stretched to the
      // grid cell, aligning its child via flex — Fill children cover, hug children anchor.
      children.push(
        element(
          'div',
          {
            'grid-area': '1 / 1',
            display: 'flex',
            'justify-content': cellJustify,
            'align-items': cellAlign,
            width: '100%',
            height: '100%',
            // …and may not grow PAST it: a grid item's automatic minimum size is min-content, so a
            // layer holding a scroller would size the track to the scroller's CONTENT and swell the
            // stack to the widest line in the file.
            'min-width': '0',
            'min-height': '0',
            // Spec A3 (C# twin): paint order IS child order — a child with backdrop-filter/filter/
            // opacity creates a stacking context that CSS would otherwise paint above every plain
            // sibling drawn after it (a blurred scrim covering its own dialog).
            'z-index': `${i + 1}`,
          },
          [lowered],
        ),
      );
    }
  }

  return element(
    'div',
    {
      display: 'grid',
      position: 'relative',
      top: undefined,
      width: sizeValue(node.width),
      height: sizeValue(node.height),
      'flex-shrink': rigid(node.width, node.height),
    },
    children,
  );
}

function textLeaf(content: string): HtmlNode {
  return { tag: '#text', attributes: {}, events: {}, children: [], textContent: content };
}

/** The C# PointerCursor member names (camelCase over the wire) → the CSS keywords they mirror. */
function cssCursor(cursor: string): string {
  switch (cursor) {
    case 'colResize':
      return 'col-resize';
    case 'rowResize':
      return 'row-resize';
    case 'notAllowed':
      return 'not-allowed';
    default:
      return cursor; // pointer, text, crosshair — already the CSS keyword
  }
}

function lowerBox(box: BoxNode, context: LoweringContext, path: string): HtmlNode {
  const style = box.style ?? ({} as BoxStyleValue);
  const entries: Record<string, string | undefined> = {
    'box-sizing': 'border-box',
    width: sizeValue(style.width),
    height: sizeValue(style.height),
    'flex-shrink': rigid(style.width, style.height),
    // FILL is a CEILING (C# twin): an item's automatic minimum size overrides width, so a Fill
    // box grew past its parent to fit its longest content. An explicit minWidth still wins.
    'min-width': style.minWidth && style.minWidth > 0
      ? px(style.minWidth)
      : style.width?.kind === 'fill' ? '0' : undefined,
    'min-height': style.minHeight && style.minHeight > 0
      ? px(style.minHeight)
      : style.height?.kind === 'fill' ? '0' : undefined,
    'max-width': style.maxWidth && style.maxWidth > 0 ? px(style.maxWidth) : undefined,
    'max-height': style.maxHeight && style.maxHeight > 0 ? px(style.maxHeight) : undefined,
    padding:
      style.padding && !isZeroInsets(style.padding) ? paddingValue(style.padding) : undefined,
    'background-color': style.background ? tokenValue(style.background) : undefined,
    // Mirror of the C# realizer: the FIRST layer paints on top, so a gradient sits over the grid,
    // and background-size carries one entry per layer (`auto` stands in for the gradient).
    'background-image': backgroundLayers(style),
    'background-size': backgroundLayerSizes(style),
    'border-radius':
      style.cornerRadius && !isZeroRadii(style.cornerRadius)
        ? radiusValue(style.cornerRadius)
        : undefined,
    'box-shadow': (() => {
      // Depth (elevation) and the colored HALO compose as one list — the C# twin's rule.
      const parts: string[] = [];
      if (style.elevation && style.elevation > 0) {
        const spec = getPhotonTheme().elevation(style.elevation);
        if (spec && (spec.blur !== 0 || spec.offsetY !== 0 || spec.spread !== 0))
          parts.push(
            `0 ${px(spec.offsetY)} ${px(spec.blur)} ${px(spec.spread)} ${tokenValue(spec.color)}`,
          );
      }
      if (style.shadows && style.shadows.length > 0)
        for (const entry of style.shadows)
          parts.push(
            `0 ${px(entry.offsetY)} ${px(entry.blur)} ${px(entry.spread)} ${tokenValue(entry.color)}`.replace('0 0px', '0 0'),
          );
      if (style.shadow)
        parts.push(
          `0 ${px(style.shadow.offsetY)} ${px(style.shadow.blur)} ${px(style.shadow.spread)} ${tokenValue(style.shadow.color)}`,
        );
      if (style.insetHighlight) parts.push(`inset 0 1px 0 ${tokenValue(style.insetHighlight)}`);
      return parts.length > 0 ? parts.join(', ') : undefined;
    })(),
    // The shorthand for a full border; per-side widths when the box draws only some edges —
    // a section rule, an accent bar, a table cell sharing its neighbour's line (C# twin).
    border:
      style.borderWidth && style.borderWidth > 0 && style.borderColor && sidesAreAll(style)
        ? `${px(style.borderWidth)} solid ${tokenValue(style.borderColor)}`
        : undefined,
    // KEBAB, quoted, like every other multi-word property in this object: the key IS the CSS
    // property name. camelCase here made the browser drop the rule AND made the class hash differ
    // from the C# one, so a hydrated box got a different class than the server had given it.
    'border-width':
      style.borderWidth && style.borderWidth > 0 && !sidesAreAll(style)
        ? sideWidths(style.borderWidth, style.borderSides ?? BORDER_ALL)
        : undefined,
    'border-style': partialBorder(style) ? 'solid' : undefined,
    'border-color':
      partialBorder(style) && style.borderColor ? tokenValue(style.borderColor) : undefined,
    // The container side of loop motion: children clip to the rrect (native PushClip twin).
    overflow: style.clip ? 'hidden' : undefined,
    // Spec S1 — group opacity, center-anchored static transform, one-axis-derives aspect ratio.
    opacity: style.opacity != null && style.opacity < 1 ? num(style.opacity) : undefined,
    // ELEMENT blur (blur-3xl glow washes) — this box's own pixels (C# twin).
    filter: style.blur && style.blur > 0 ? `blur(${px(style.blur)})` : undefined,
    // Spec S3 frosted glass (native BackdropBlur pass-split twin); Safari still needs the prefix.
    'backdrop-filter':
      style.backdropBlur && style.backdropBlur > 0 ? `blur(${px(style.backdropBlur)})` : undefined,
    '-webkit-backdrop-filter':
      style.backdropBlur && style.backdropBlur > 0 ? `blur(${px(style.backdropBlur)})` : undefined,
    transform: style.transform ? transformValue(style.transform) : undefined,
    'aspect-ratio': style.aspectRatio && style.aspectRatio > 0 ? num(style.aspectRatio) : undefined,
    // Spec S6 — changes to the covered channels glide instead of snapping (C# twin).
    transition: style.transition ? transitionValue(style.transition) : undefined,
    // The CSS cursor mirror — the C# enum's camelCase member names lower to the CSS keywords.
    cursor: style.cursor && style.cursor !== 'default' ? cssCursor(style.cursor) : undefined,
  };

  // ELEVATION DECIDES WHAT IS ON TOP, not just how deep the shadow is (C# twin). A raised surface
  // that anything drawn after it covers is not raised — which is what chrome pinned over a
  // scrolling page kept discovering. z-index needs a positioned box, and `relative` is the one that
  // changes nothing else; a box that already positions itself keeps its own.
  if (style.elevation && style.elevation > 0) {
    entries['z-index'] = `${style.elevation}`;
    if (entries['position'] === undefined) entries['position'] = 'relative';
  }

  // A SIMULATED state REPLACES the base declarations, before they are atomized. Adding a second
  // class for the same property instead would leave the winner to stylesheet insertion order —
  // every atomic class has equal specificity — and would emit a class set the C# side does not.
  if (style.hover && simulatedState & SIMULATED_HOVERED) applyDiff(entries, style.hover);
  if (style.focus && simulatedState & SIMULATED_FOCUSED) applyDiff(entries, style.focus);

  const result = element('div', entries);

  if (style.hover && !(simulatedState & SIMULATED_HOVERED)) appendDiff(result, ':hover', style.hover);
  if (style.focus && !(simulatedState & SIMULATED_FOCUSED)) appendDiff(result, ':focus-visible', style.focus);

  if (box.child) {
    const child = lowerNode(box.child, context, null, path + '/0');
    if (child) result.children.push(child);
  }
  return result;
}

/** Spec S5 mirror of the C# AppendDiff — identical declaration strings, pseudo-hashed classes. */
/** A StyleDiff's set members, written over the given entries — what a simulated state does. */
function applyDiff(entries: Record<string, string | undefined>, diff: StyleDiffValue): void {
  Object.assign(entries, diffEntries(diff));
}

function appendDiff(node: HtmlNode, pseudo: string, diff: StyleDiffValue): void {
  const classes = atomizePseudo(pseudo, diffEntries(diff));
  if (classes) {
    const existing = node.attributes['class'];
    node.attributes['class'] = existing ? `${existing} ${classes}` : classes;
  }
}

/** The declarations a StyleDiff carries — one builder, so the pseudo path and the simulated path
 * cannot drift into showing different things for the same diff. */
function diffEntries(diff: StyleDiffValue): Record<string, string | undefined> {
  const entries: Record<string, string | undefined> = {};
  if (diff.background) entries['background-color'] = tokenValue(diff.background);
  if (diff.borderWidth != null && diff.borderColor) {
    entries['border'] = `${px(diff.borderWidth)} solid ${tokenValue(diff.borderColor)}`;
  } else if (diff.borderColor) {
    entries['border-color'] = tokenValue(diff.borderColor);
  }
  if (diff.elevation != null) {
    const spec = getPhotonTheme().elevation(diff.elevation);
    if (spec && (spec.blur !== 0 || spec.offsetY !== 0 || spec.spread !== 0)) {
      entries['box-shadow'] = `0 ${px(spec.offsetY)} ${px(spec.blur)} ${px(spec.spread)} ${tokenValue(spec.color)}`;
    }
  }
  if (diff.opacity != null) entries['opacity'] = num(diff.opacity);
  if (diff.gradient) entries['background-image'] = gradientValue(diff.gradient);
  return entries;
}

function lowerFlex(flex: FlexNodeValue, context: LoweringContext, path: string): HtmlNode {
  const horizontal = flex.nodeKind === 'row';
  const result = element('div', {
    'box-sizing': 'border-box',
    display: 'flex',
    'flex-direction': horizontal ? 'row' : 'column',
    // Spec S3 wrap mirror: "row-gap column-gap" in the stacking order of the container's axis.
    'flex-wrap': flex.wrap ? 'wrap' : undefined,
    gap: gapValue(flex, horizontal),
    'justify-content': mainAlign(flex.main),
    'align-items': crossAlign(flex.cross),
    width: sizeValue(flex.width),
    height: sizeValue(flex.height),
    'flex-shrink': rigid(flex.width, flex.height),
    // FILL means "the parent's extent, never more" — and a flex item's automatic minimum size
    // OVERRIDES width, so long content grew a Fill row past its parent (C# twin).
    'min-width': flex.width?.kind === 'fill' ? '0' : undefined,
    'min-height': flex.height?.kind === 'fill' ? '0' : undefined,
    padding: flex.padding && !isZeroInsets(flex.padding) ? paddingValue(flex.padding) : undefined,
    'background-color': flex.background ? tokenValue(flex.background) : undefined,
    'border-radius':
      flex.cornerRadius && !isZeroRadii(flex.cornerRadius)
        ? radiusValue(flex.cornerRadius)
        : undefined,
  });

  for (let i = 0; i < flex.children.length; i++) {
    const lowered = lowerNode(flex.children[i], context, horizontal, path + '/' + i);
    if (lowered) {
      // Spec S1 align-self: the child overrides the container's cross alignment for itself.
      const self = flex.children[i].alignSelf;
      if (self) mergeAtomicDeclaration(lowered, 'align-self', crossAlign(self));
      result.children.push(lowered);
    }
  }
  return result;
}

function mainAlign(value: FlexNodeValue['main']): string {
  switch (value) {
    case 'center':
      return 'center';
    case 'end':
      return 'flex-end';
    case 'spaceBetween':
      return 'space-between';
    default:
      return 'flex-start';
  }
}

function gapValue(flex: FlexNodeValue, horizontal: boolean): string | undefined {
  const run = flex.runGap ?? flex.gap;
  if (flex.wrap && run !== flex.gap) {
    return horizontal ? `${px(run)} ${px(flex.gap)}` : `${px(flex.gap)} ${px(run)}`;
  }
  return flex.gap > 0 ? px(flex.gap) : undefined;
}

function crossAlign(value: FlexNodeValue['cross']): string {
  switch (value) {
    case 'start':
      return 'flex-start';
    case 'center':
      return 'center';
    case 'end':
      return 'flex-end';
    default:
      return 'stretch';
  }
}

/** Mirror of C# WebRealizer.BackgroundLayers: CSS paint order — the FIRST entry sits on top. */
function backgroundLayers(style: BoxStyleValue): string | undefined {
  const layers: string[] = [];
  if (style.gradient) layers.push(gradientValue(style.gradient));
  if (style.glow) layers.push(glowValue(style.glow));
  if (style.pattern) layers.push(gridPatternValue(style.pattern));
  return layers.length === 0 ? undefined : layers.join(', ');
}

/** Mirror of C# WebRealizer.BackgroundLayerSizes: one entry per layer (the grid contributes two). */
function backgroundLayerSizes(style: BoxStyleValue): string | undefined {
  if (!style.pattern) return undefined;
  const sizes: string[] = [];
  if (style.gradient) sizes.push('auto');
  if (style.glow) sizes.push('auto');
  sizes.push(gridPatternSize(style.pattern));
  return sizes.join(', ');
}

/**
 * The monospaced face, with an APP-SETTABLE hook in front of it.
 *
 * A mono Text lands on an atomic class, and an atomic class beats a `body { font-family }` rule —
 * so an app that wanted its own code face had nowhere to put it: the class name is a content hash
 * that moves between builds, which is no hook at all. The variable is the hook. Set
 * `--eq-font-mono` on `:root` and every mono run follows, with this stack as the fallback when
 * nothing does.
 *
 * The measurer reads the SAME variable (see photon-context) — measuring with a different face than
 * the one that draws is how a code editor's caret ends up beside the character it is on.
 */
const MONO_STACK =
  "var(--eq-font-mono, ui-monospace, 'SFMono-Regular', Menlo, Consolas, monospace)";

function lowerText(text: TextNode, context: LoweringContext): HtmlNode {
  const style: StyleEntries = {
    color: tokenValue(text.color ?? context.textPrimary),
    // Line alignment inside the paragraph (C# twin) — wrapped lines of a centered headline
    // must center too.
    'text-align':
      text.align === 'center' ? 'center' : text.align === 'end' ? 'end' : undefined,
    // Authored \n is a HARD break (pre-line keeps normal wrapping between them) — C# twin.
    // Mono text is CODE (indentation survives); plain text only keeps its newlines.
    'white-space': text.mono === true ? 'pre-wrap' : text.content.includes('\n') ? 'pre-line' : undefined,
    'font-family': text.mono === true ? MONO_STACK : undefined,
    'font-variant-numeric': text.tabular === true ? 'tabular-nums' : undefined,
    // The slant (C# twin): the node's own, or the ROLE's when the theme cuts that role italic.
    'font-style':
      text.italic === true || text.styleOverride?.italic === true ? 'italic' : undefined,
    // Spec S6 (C# twin): recolors glide instead of flipping.
    transition: text.transition ? transitionValue(text.transition) : undefined,
  };

  // Gradient text — mirrors the C# realizer, including keeping `color` as the readable fallback
  // for a browser without background-clip:text. Property ORDER follows HtmlStyle.ToCssString.
  if (text.gradient) {
    style['background-image'] = gradientValue(text.gradient);
    style['-webkit-background-clip'] = 'text';
    style['background-clip'] = 'text';
    style['-webkit-text-fill-color'] = 'transparent';
  }

  // Single line → shaping-style ellipsis (spec A8).
  if (text.maxLines === 1) {
    // MONO keeps `pre` (C# twin): nowrap collapses runs of spaces, and in code the spaces ARE
    // the content. `pre` refuses to wrap just the same, so the ellipsis contract holds.
    style['white-space'] = text.mono === true ? 'pre' : 'nowrap';
    style.overflow = 'hidden';
    style['text-overflow'] = 'ellipsis';
    // BLOCK, or the other two do nothing (C# twin): a Text lowers to a `span`, and `overflow` and
    // `text-overflow` are inert on a non-replaced inline box, so a squeezed single-line Text
    // painted its full width out of its parent instead of ellipsising inside it. Block takes the
    // width the parent allows; inline-block would size to content and spill again.
    style.display = 'block';
  } else if (text.maxLines > 1) {
    // MULTI-LINE clamp (C# twin): exactly N lines, then an ellipsis — what keeps a grid of cards
    // on one baseline when the copy is not the site's to control.
    style.display = '-webkit-box';
    style['-webkit-box-orient'] = 'vertical';
    style['-webkit-line-clamp'] = `${text.maxLines}`;
    style.overflow = 'hidden';
  }

  // System table override (e.g. Button labels) — inline styles beat the role class.
  if (text.styleOverride) {
    const override = text.styleOverride;
    style['font-size'] = px(override.size);
    style['line-height'] = px(override.lineHeight);
    const weight =
      cssFontWeight(override.weight);
    style['font-weight'] = String(weight);
    style['letter-spacing'] = px(override.tracking);
  }

  // RICH runs (C# twin): inline spans with their own color/mono face; wrapping stays
  // paragraph-level. When absent the plain content is the single leaf.
  const children =
    text.spans && text.spans.length > 0
      ? text.spans.map((run) => {
          // An <a> when the run links, a <span> otherwise — both inline, so the paragraph still
          // wraps between WORDS. Mirrors the C# realizer exactly: a server emitting an anchor and a
          // client emitting a span is a hydration mismatch on every linked sentence.
          const runNode = element(
            run.destination ? 'a' : 'span',
            {
              color: run.color ? tokenValue(run.color) : undefined,
              'font-family': run.mono === true ? MONO_STACK : undefined,
              'font-weight': run.weight ? String(cssFontWeight(run.weight)) : undefined,
              'font-style': run.italic === true ? 'italic' : undefined,
              // SIZE only — no line-height: a run keeps the paragraph's line box (C# twin).
              // The browser has no Dynamic Type factor, so the C# `ScaledSize(typeScale)` reduces
              // to the 0.5dp snap it ends with; snapping here keeps the two class strings equal.
              // Missing on THIS side until the slant axis landed, and it showed: the server sized
              // inline code at 13.5 and hydration re-sized it to the paragraph's own size, so
              // every `code` span on a documentation page grew the moment the page booted.
              'font-size': run.styleOverride
                ? px(Math.round(run.styleOverride.size * 2) / 2)
                : undefined,
            },
            [textLeaf(run.content)],
          );
          if (run.destination)
            runNode.attributes = { ...runNode.attributes, href: run.destination };
          return runNode;
        })
      : [textLeaf(text.content)];
  const node = element('span', style, children);
  prependClass(node, `eq-type-${text.role.toLowerCase()}`);
  return node;
}

/** Whether a node requests Fill per axis — wrappers must stretch for the 100% chain to reach it. */
function fills(node: VisualNodeValue): { width: boolean; height: boolean } {
  switch (node.nodeKind) {
    case 'box': {
      const style = (node as BoxNode).style ?? ({} as BoxStyleValue);
      return { width: style.width?.kind === 'fill', height: style.height?.kind === 'fill' };
    }
    case 'row':
    case 'column': {
      const flex = node as FlexNodeValue;
      return { width: flex.width?.kind === 'fill', height: flex.height?.kind === 'fill' };
    }
    case 'stack': {
      const stack = node as StackNode;
      return { width: stack.width?.kind === 'fill', height: stack.height?.kind === 'fill' };
    }
    case 'pressable':
      return fills((node as PressableNode).child);
    case 'adjustable':
      return fills((node as AdjustableNode).child as VisualNodeValue);
    case 'hoverable':
      return fills((node as HoverableNode).child);
    case 'simulated':
      return fills((node as SimulatedNode).child);
    case 'inFlow':
      return fills((node as InFlowNode).child);
    case 'inView':
      return fills((node as InViewNode).child);
    case 'flexible':
      return fills((node as FlexibleNode).child);
    case 'loopMotion':
      return fills((node as LoopMotionNode).child);
    default:
      return { width: false, height: false };
  }
}

function lowerPressable(
  pressable: PressableNode,
  context: LoweringContext,
  path: string,
): HtmlNode {
  const disabled = pressable.disabled === true;
  const fill = fills(pressable.child);
  const child = lowerNode(pressable.child, context, null, path + '/0');

  // HTML forbids a button inside a button (and an anchor inside an anchor): the parser closes the
  // outer one and hands back an empty shell. A Pressable AROUND a control — a Menu making its
  // trigger open the panel — is ordinary composition, so the OUTER one yields: it keeps the press,
  // the child keeps being the real control, and the markup stays legal. The C# realizer decides
  // this the same way, from the same lowered subtree, or hydration disagrees about the whole thing.
  const wrapping = child != null && wrapsAnInteractive(child);
  const node = element(wrapping ? 'span' : 'button', {
    padding: '0',
    background: 'none',
    border: 'none',
    'font-family': 'inherit',
    'text-align': 'start',
    cursor: disabled ? undefined : 'pointer',
    // A Fill child needs the 100% chain to pass through the button (scrim et al.).
    width: fill.width ? '100%' : undefined,
    height: fill.height ? '100%' : undefined,
  });

  if (wrapping) {
    node.attributes['role'] = 'button';
    if (!disabled) node.attributes['tabindex'] = '0';
  }
  if (pressable.label) node.attributes['aria-label'] = pressable.label;
  // Selection stated, not merely painted — a fill colour says nothing to a screen reader.
  if (pressable.role === 'radio') {
    // One choice of an exclusive set: the wrapping Adjustable is the one Tab stop, so the item
    // leaves the tab order (roving) and states its selection as checked-ness, not pressed-ness.
    node.attributes['role'] = 'radio';
    node.attributes['aria-checked'] = pressable.selected === true ? 'true' : 'false';
    node.attributes['tabindex'] = '-1';
  } else if (pressable.selected !== undefined && pressable.selected !== null) {
    node.attributes['aria-pressed'] = pressable.selected ? 'true' : 'false';
  }
  if (disabled && !wrapping) node.attributes['disabled'] = '';
  if (disabled && wrapping) node.attributes['aria-disabled'] = 'true';
  if (!disabled && pressable.onPressed) node.events['click'] = pressable.onPressed as EventHandler;

  // Interaction states (spec §01): mechanics live in the generated stylesheet — every enabled
  // pressable carries the class (:focus-visible double ring is an a11y DEFAULT); the pressed swap
  // additionally ships its token value as a custom property at the style TAIL (the C# cross-pin).
  if (!disabled) {
    // eq-pressed carries the same declaration :active does (see the generated stylesheet), so a
    // simulated press cannot drift from a real one — it is the same selector list.
    prependClass(node, simulatedState & SIMULATED_PRESSED ? 'eq-pressable eq-pressed' : 'eq-pressable');
    if (pressable.pressedBackground) {
      const tail = `--eq-pressed-bg: ${tokenValue(pressable.pressedBackground)}`;
      const existing = node.attributes['style'];
      node.attributes['style'] = existing ? `${existing}; ${tail}` : tail;
    }
  }

  if (child) node.children.push(child);
  return node;
}

/** Whether a lowered subtree already carries an interactive ELEMENT — see {@link lowerPressable}. */
function wrapsAnInteractive(node: HtmlNode): boolean {
  if (node.tag === 'button' || node.tag === 'a') return true;
  return (node.children ?? []).some(wrapsAnInteractive);
}

function lowerFlexible(
  flexible: FlexibleNode,
  context: LoweringContext,
  horizontalAxis: boolean | null,
  path: string,
): HtmlNode {
  // flex: grow shrink basis. A basis of 0 stays `0%` — the historical output, and the one that
  // matches the native leftover-by-weight distribution. A non-zero basis is the size the line
  // breaker measures against in a WRAPPING container, which is what lets two panes sit side by
  // side while there is room and take a line each when there is not. min-size 0 lets text shrink to
  // ellipsis instead of pushing siblings (the truncation contract).
  const basis =
    flexible.basis !== undefined && flexible.basis > 0 ? `${flexible.basis}px` : '0%';
  const shrink = flexible.shrink ?? 1;
  const node = element('div', {
    flex: `${flexible.flex} ${shrink} ${basis}`,
    // Spec B14: weight changes animate; the component omits the flag on a regression (snap).
    transition:
      flexible.animateChanges === true
        ? 'flex-grow var(--eq-motion-base) var(--eq-curve-standard)'
        : undefined,
    'min-width': horizontalAxis !== false ? '0' : undefined,
    'min-height': horizontalAxis === false ? '0' : undefined,
  });
  const child = lowerNode(flexible.child, context, horizontalAxis, path + '/0');
  if (child) node.children.push(child);
  return node;
}

/** Spec S7 mirror of the C# LowerSticky: CSS sticky at `offset` from the viewport start. */
/**
 * Wave 3 mirror of the C# LowerAnchored: position:relative host (generated .eq-anchorhost),
 * invisible fixed scrim as a REAL pressable while dismissible, and the absolute panel positioned
 * ENTIRELY by the generated placement classes — the gap rides the margin as an atomic declaration.
 */
function lowerAnchored(node: AnchoredNode, context: LoweringContext, path: string): HtmlNode {
  const host: HtmlNode = {
    tag: 'div',
    attributes: {
      class: node.openOnHover === true ? 'eq-anchorhost eq-hoverreveal' : 'eq-anchorhost',
    },
    events: {},
    children: [],
  };
  const anchor = lowerNode(node.anchor, context, null, path + '/0');
  if (anchor) {
    // C# twin: a painted scrim dims the page, never its own anchor. Motion panels keep the class
    // in BOTH states — the scrim is still fading out after close.
    if (node.scrimStyle && node.onDismiss && (node.open === true || node.motion))
      prependClass(anchor, 'eq-anchor-above');
    host.children.push(anchor);
  }
  if (node.openOnHover === true) {
    host.children.push(buildAnchorPanel(node, context, path));
    return host;
  }
  // C# twin: open/close MOTION keeps panel + scrim mounted in both states (element identity is
  // what lets the CSS transitions glide); without it, closed renders nothing.
  if (node.open !== true && !node.motion) return host;

  if (node.onDismiss) {
    // Escape closes an open panel — a menu you opened with the keyboard has to be closable with it.
    // Declared through the same channel a Shortcut node uses, so the stacking comes for free: the
    // dispatcher runs the LAST declared match, which is the panel on top. C# twin: the Photon
    // realizer registers the identical binding while the panel is open.
    declareShortcut({ chord: chordId({ key: 'Escape' }), handler: node.onDismiss });

    // Mega-menu dimming (C# twin): scrimStyle paints the outside-tap scrim as a full Box.
    const scrim = lowerNode(
      {
        nodeKind: 'pressable',
        child: {
          nodeKind: 'box',
          style: {
            ...(node.scrimStyle ?? {}),
            width: { kind: 'fill', value: 0 },
            height: { kind: 'fill', value: 0 },
          },
        },
        onPressed: node.onDismiss,
        label: 'Dismiss',
      } as unknown as VisualNodeValue,
      context,
      null,
      path + '/s',
    );
    if (scrim) {
      const existing = scrim.attributes['class'];
      scrim.attributes['class'] = existing ? `${existing} eq-anchor-scrim` : 'eq-anchor-scrim';
      if (node.motion) {
        // C# twin: the scrim cross-fades with the panel and, closed, is hidden — out of
        // hit-testing and the focus order.
        appendDeclarations(scrim, {
          transition: motionTransition(node.motion, false),
          ...(node.open === true
            ? {}
            : { opacity: '0', visibility: 'hidden', 'pointer-events': 'none' }),
        });
      }
      host.children.push(scrim);
    }
  }

  host.children.push(buildAnchorPanel(node, context, path));
  return host;
}

/** Atomizes extra declarations onto an ALREADY-lowered node (the C# realizer's equivalent of
 * assigning more members on `element.Style` before the atomizer runs). */
function appendDeclarations(node: HtmlNode, entries: Record<string, string | undefined>): void {
  const extra = atomicAttrs(entries);
  if (extra['class']) {
    const existing = node.attributes['class'];
    node.attributes['class'] = existing ? `${existing} ${extra['class']}` : extra['class'];
  }
  if (extra['style']) {
    const existing = node.attributes['style'];
    node.attributes['style'] = existing ? `${existing}; ${extra['style']}` : extra['style'];
  }
}

/**
 * Spec S8 (the C# LowerShortcut twin): the child lowers UNCHANGED and carries the binding marker;
 * the live registration is what makes the chord fire. Mounting is the subscription — the pass's
 * declarations replace the previous set when it commits, so an unmounted subtree stops matching.
 */
function lowerShortcut(
  node: ShortcutNode,
  context: LoweringContext,
  horizontalAxis: boolean | null,
  path: string,
): HtmlNode | null {
  const child = lowerNode(node.child, context, horizontalAxis, path + '/0');
  if (!child) return null;
  const chord = chordId(node.chord);
  // C# twin: nested shortcuts share one child root, so the marker LISTS them.
  const existing = child.attributes['data-eq-shortcut'];
  child.attributes['data-eq-shortcut'] = existing ? `${existing} ${chord}` : chord;
  if (node.onPressed) declareShortcut({ chord, handler: node.onPressed });
  return child;
}

/** The chord's wire form — the C# WebRealizer.ChordId twin (fixed modifier order). */
function chordId(chord: KeyChordValue | undefined): string {
  if (!chord) return '';
  const modifiers = chord.modifiers ?? 0;
  const parts: string[] = [];
  if (modifiers & 4) parts.push('command');
  if (modifiers & 8) parts.push('control');
  if (modifiers & 2) parts.push('alt');
  if (modifiers & 1) parts.push('shift');
  parts.push((chord.key ?? '').toLowerCase());
  return parts.join('+');
}

/**
 * S5 programmable hover (the C# LowerHoverable twin): a layout-transparent div whose
 * mouseenter/mouseleave feed the boolean callback. Fill passes through like Pressable's button.
 */
/**
 * ADJUSTMENT semantics (C# twin: the Photon host's arrow dispatch): one focusable wrapper for the
 * whole control, arrows nudge, and the inner press targets stay pointer-only — the wrapper is the
 * Tab stop, so the browser's own focus order matches the native one.
 */
function lowerAdjustable(node: AdjustableNode, context: LoweringContext, path: string): HtmlNode {
  const fill = fills(node.child);
  const host = element('div', {
    width: fill.width ? '100%' : undefined,
    height: fill.height ? '100%' : undefined,
  });
  const role = node.role ?? 'slider';
  host.attributes['role'] = role;
  host.attributes['tabindex'] = '0';
  if (node.label) host.attributes['aria-label'] = node.label;
  if (node.onAdjust) {
    const adjust = node.onAdjust;
    // A slider's vertical axis is VALUE (up increases); a radiogroup's or tablist's is READING
    // ORDER (down is next) — WAI-ARIA's own split, one mapping per role family.
    const downIsNext = role !== 'slider';
    host.events['keydown'] = ((event: KeyboardEvent) => {
      const direction = event.key === 'ArrowRight' ? 1
        : event.key === 'ArrowLeft' ? -1
        : event.key === 'ArrowUp' ? (downIsNext ? -1 : 1)
        : event.key === 'ArrowDown' ? (downIsNext ? 1 : -1)
        : 0;
      if (direction === 0) return;
      event.preventDefault();   // an arrow that also scrolls the page is two answers to one key
      adjust(direction);
    }) as unknown as EventHandler;
  }
  const child = lowerNode(node.child, context, null, path + '/0');
  if (child) host.children.push(child);
  return host;
}

/**
 * The C# `SimulatedState` flags, by value — a [Flags] enum crosses as its number.
 */
const SIMULATED_HOVERED = 1;
const SIMULATED_PRESSED = 2;
const SIMULATED_FOCUSED = 4;

/** What the subtree being lowered right now is being DRAWN as (the C# WebRealizer ambient twin). */
let simulatedState = 0;

/**
 * Draws the subtree in the given states (the C# `LowerSimulated`). `:hover` and `:focus-visible`
 * cannot be forced from CSS — nothing can, which is the point of a pseudo-class — so the diffs
 * those rules carry fold into the BASE style instead. Same declarations, so the picture is honest.
 */
function lowerSimulated(
  node: SimulatedNode,
  context: LoweringContext,
  horizontalAxis: boolean | null,
  path: string,
): HtmlNode | null {
  const previous = simulatedState;
  // Nested previews COMBINE: a hovered card holding a pressed button is two nodes, and the inner
  // one must not turn the outer's hover off.
  simulatedState = previous | node.state;
  try {
    return lowerNode(node.child, context, horizontalAxis, path + '/0');
  } finally {
    simulatedState = previous;
  }
}

/**
 * Arms the in-flow intent while the child BUILDS (the C# `LowerInFlow`). The overlay reads it in
 * its own build — the only moment the answer can change anything, because by the time a tree exists
 * the scrim and the layer are already in it.
 */
/**
 * A Stack child as the STACK sees it: a component is resolved through the store and built ONCE, so
 * a `Positioned` it returns still positions and the instance that builds is the instance that
 * stays. Anything else passes through untouched.
 *
 * The lowering of the result happens where it always did — this only decides which branch the child
 * takes, which is the decision `child.nodeKind === 'positioned'` was getting wrong.
 */
function resolveStackChild(
  child: VisualNodeValue,
  context: LoweringContext,
  path: string,
): VisualNodeValue | null {
  let node = child;
  // Bounded: a component whose build returns itself would otherwise spin here.
  for (let hops = 0; hops < 8 && node?.nodeKind === 'component'; hops++) {
    const pass = getActivePass();
    const resolved = (
      pass ? pass.store.reconcile(path, node, pass.invalidator) : node
    ) as ComponentNode;
    try {
      node = resolved.build(context.componentContext) as VisualNodeValue;
    } catch (error) {
      const name = (resolved as { constructor?: { name?: string } }).constructor?.name ?? 'Component';
      reportComponentFailure(name, error);
      return describeComponentFailure(name, error, getPhotonTheme()) as VisualNodeValue;
    }
  }
  return node;
}

/**
 * The C# `LowerInView` twin: a display:contents wrapper the runtime observes. The server emits the
 * same element without the marker — it cannot watch anything — so hydration is an attribute diff
 * rather than a replaced subtree.
 */
function lowerInView(
  node: InViewNode,
  context: LoweringContext,
  horizontalAxis: boolean | null,
  path: string,
): HtmlNode | null {
  const child = lowerNode(node.child, context, horizontalAxis, path + '/0');
  if (!child) return null;

  const wrapper = element('div', { display: 'contents' }, [child]);
  // The marker goes on the CHILD, never on the wrapper. display:contents generates no box, so an
  // IntersectionObserver pointed at the wrapper watches a 0×0 rectangle at the origin — it observes
  // faithfully and reports about a place the heading never is. The wrapper still exists because the
  // server emits it and hydration is an attribute diff; it just has nothing to measure.
  const observed = child.tag ? child : wrapper;
  observed.attributes['data-eq-inview'] = path;
  declareInView(path, {
    threshold: node.threshold ?? 0,
    onChanged: node.onChanged ?? (() => {}),
  });
  return wrapper;
}

function lowerInFlow(
  node: InFlowNode,
  context: LoweringContext,
  horizontalAxis: boolean | null,
  path: string,
): HtmlNode | null {
  const previous = setInFlow(true);
  try {
    return lowerNode(node.child, context, horizontalAxis, path + '/0');
  } finally {
    setInFlow(previous);
  }
}

function lowerHoverable(node: HoverableNode, context: LoweringContext, path: string): HtmlNode {
  const fill = fills(node.child);
  const host = element('div', {
    width: fill.width ? '100%' : undefined,
    height: fill.height ? '100%' : undefined,
  });
  if (node.onChanged) {
    const changed = node.onChanged;
    host.events['mouseenter'] = (() => changed(true)) as EventHandler;
    host.events['mouseleave'] = (() => changed(false)) as EventHandler;
  }
  const child = lowerNode(node.child, context, null, path + '/0');
  if (child) host.children.push(child);
  return host;
}

/** The C# BuildAnchorPanel twin. */
function buildAnchorPanel(node: AnchoredNode, context: LoweringContext, path: string): HtmlNode {
  const top =
    node.placement === 'topStart' || node.placement === 'topEnd' || node.placement === 'topCenter';
  const gap = node.gap ?? 4;
  // HOVER BRIDGE (C# twin): hover-open panels carry the gap as PADDING — part of the hoverable
  // area — so crossing it never drops the host's :hover. Click-open panels keep the margin.
  const gapProp = node.openOnHover === true ? 'padding' : 'margin';
  const motion = node.openOnHover === true ? null : node.motion;
  const closed = motion && node.open !== true;
  // C# twin: open/close glide — fade + a MotionLiftDp nudge toward the anchor; closed ends
  // visibility:hidden (gone from hit-testing and the Tab order once the exit completes).
  const panel = element('div', {
    ...(top ? { [`${gapProp}-bottom`]: px(gap) } : { [`${gapProp}-top`]: px(gap) }),
    transition: motion ? motionTransition(motion, true) : undefined,
    opacity: closed ? '0' : undefined,
    transform: closed ? `translateY(${px(top ? ANCHOR_MOTION_LIFT : -ANCHOR_MOTION_LIFT)})` : undefined,
    visibility: closed ? 'hidden' : undefined,
    'pointer-events': closed ? 'none' : undefined,
  });
  if (node.matchAnchorWidth === true) prependClass(panel, 'eq-anchor-match');
  prependClass(panel, anchorPlacementClass(node.placement));
  prependClass(panel, 'eq-anchor-panel');
  const content = lowerNode(node.panel, context, null, path + '/1');
  if (content) panel.children.push(content);
  return panel;
}

/** The C# PlacementClass twin. */
function anchorPlacementClass(placement: AnchoredNode['placement']): string {
  switch (placement) {
    case 'bottomEnd':
      return 'eq-anchor-b-end';
    case 'topStart':
      return 'eq-anchor-t-start';
    case 'topEnd':
      return 'eq-anchor-t-end';
    case 'bottomCenter':
      return 'eq-anchor-b-center';
    case 'topCenter':
      return 'eq-anchor-t-center';
    default:
      return 'eq-anchor-b-start';
  }
}

/**
 * C# twin of the SafeArea lowering: the browser fills `env(safe-area-inset-*)` — zero where there is
 * no cutout, the real number where there is. An edge the caller left out keeps only its own padding.
 */
function lowerSafeArea(node: SafeAreaNode, context: LoweringContext, path: string): HtmlNode {
  const extra = node.extra;
  const inset = (flag: number, name: string, own: number): string => {
    const env = `env(safe-area-inset-${name}, 0px)`;
    if ((node.edges & flag) === 0) return px(own);
    return own === 0 ? env : `calc(${env} + ${px(own)})`;
  };

  const wrapper = element('div', {
    'padding-top': inset(1, 'top', extra?.top ?? 0),
    'padding-bottom': inset(2, 'bottom', extra?.bottom ?? 0),
    'padding-left': inset(4, 'left', extra?.start ?? 0),
    'padding-right': inset(8, 'right', extra?.end ?? 0),
  });

  const child = lowerNode(node.child, context, null, path + '/0');
  if (child) wrapper.children.push(child);
  return wrapper;
}

function lowerSticky(node: StickyNode, context: LoweringContext, path: string): HtmlNode {
  const float = node.float === true;
  const wrapper = element('div', {
    // Floating chrome (the fixed header) paints above the page; plain sticky stays in flow.
    position: float ? 'fixed' : 'sticky',
    top: px(node.offset),
    left: float ? '0' : undefined,
    right: float ? '0' : undefined,
    // CHROME, both of them — a band of its own, above anything the CONTENT can reach (C# twin's
    // ChromeLayer). Plain sticky sat at 1, one step above nothing, which is a number competing with
    // other numbers: a raised card (elevation carries 1–5) would out-stack the pinned header and
    // scroll straight over it.
    'z-index': CHROME_LAYER,
    // Spec S6 (C# twin): the scrolled swap GLIDES instead of flipping in one frame.
    transition: node.transition ? transitionValue(node.transition) : undefined,
  });

  // SCROLL-LINKED diff (C# twin): declarations land under the root-gated scrolled variant, and
  // the tiny window listener that toggles `eq-scrolled` installs once per page.
  if (node.scrolledStyle) {
    const diff = node.scrolledStyle;
    const entries: Record<string, string | undefined> = {};
    if (diff.background) entries['background-color'] = tokenValue(diff.background);
    if (diff.borderWidth != null && diff.borderColor)
      entries['border-bottom'] = `${px(diff.borderWidth)} solid ${tokenValue(diff.borderColor)}`;
    if (diff.opacity != null) entries['opacity'] = num(diff.opacity);
    if (diff.backdropBlur != null && diff.backdropBlur > 0) {
      entries['backdrop-filter'] = `blur(${px(diff.backdropBlur)})`;
      entries['-webkit-backdrop-filter'] = `blur(${px(diff.backdropBlur)})`;
    }
    const classes = atomizeScrolled(entries);
    if (classes) mergeScrolledClasses(wrapper, classes);
    installScrolledController();
  }

  const child = lowerNode(node.child, context, null, path + '/0');
  if (child) wrapper.children.push(child);
  return wrapper;
}

/** Append variant classes to an element's class attribute (sorted upstream). */
function mergeScrolledClasses(node: HtmlNode, classes: string): void {
  const existing = node.attributes['class'];
  node.attributes['class'] = existing ? `${existing} ${classes}` : classes;
}

let scrolledControllerInstalled = false;

/** The scroll listener behind Sticky.ScrolledStyle: `eq-scrolled` on <html> past 8px. */
function installScrolledController(): void {
  if (scrolledControllerInstalled || typeof window === 'undefined') return;
  scrolledControllerInstalled = true;
  const apply = () =>
    document.documentElement.classList.toggle('eq-scrolled', window.scrollY > 8);
  window.addEventListener('scroll', apply, { passive: true });
  apply();
}

/** Spec S6 mirror of the C# LowerAdaptive: every declared variant gated by the fixed media rules. */
function lowerAdaptive(node: AdaptiveNodeValue, context: LoweringContext, path: string): HtmlNode {
  if (!node.medium && !node.expanded) {
    return (
      lowerNode(node.compact, context, null, path + '/0') ?? element('div', { display: 'contents' })
    );
  }
  const wrapper = element('div', { display: 'contents' });
  const addVariant = (variant: VisualNodeValue, gate: string, index: number) => {
    const lowered = lowerNode(variant, context, null, `${path}/${index}`);
    if (!lowered) return;
    ensureAdaptiveGate(gate);
    wrapper.children.push({
      tag: 'div',
      attributes: { class: gate },
      events: {},
      children: [lowered],
    });
  };
  // Ranges chain off the DECLARED variants and this node's thresholds (C# twin) — a design that
  // switches at 1024 gets 1024 gates, not the spec's 600/840.
  const mediumFrom = node.mediumFrom ?? 600;
  const expandedFrom = node.expandedFrom ?? 840;
  addVariant(node.compact, gateCompactUntil(node.medium ? mediumFrom : expandedFrom), 0);
  if (node.medium)
    addVariant(node.medium, gateMediumFrom(mediumFrom, node.expanded ? expandedFrom : 0), 1);
  if (node.expanded) addVariant(node.expanded, gateExpandedFrom(expandedFrom), 2);
  return wrapper;
}

/** Spec S4 mirror: CSS Grid — identical track/gap/span strings to the C# LowerGrid. */
function lowerGrid(grid: GridNode, context: LoweringContext, path: string): HtmlNode {
  const tracks = grid.columns
    .map((t) => (t.kind === 'fixed' ? px(t.value) : t.kind === 'fill' ? `${num(t.value)}fr` : 'auto'))
    .join(' ');
  const rowGap = grid.rowGap ?? grid.gap;
  const result = element('div', {
    'box-sizing': 'border-box',
    display: 'grid',
    'grid-template-columns': tracks,
    gap: rowGap !== grid.gap ? `${px(rowGap)} ${px(grid.gap)}` : grid.gap > 0 ? px(grid.gap) : undefined,
    width: sizeValue(grid.width),
    height: sizeValue(grid.height),
    'flex-shrink': rigid(grid.width, grid.height),
    padding: grid.padding && !isZeroInsets(grid.padding) ? paddingValue(grid.padding) : undefined,
  });
  for (let i = 0; i < grid.children.length; i++) {
    const lowered = lowerNode(grid.children[i], context, null, path + '/' + i);
    if (lowered) {
      const span = grid.children[i].gridSpan;
      if (span && span > 1) mergeAtomicDeclaration(lowered, 'grid-column', `span ${span}`);
      result.children.push(lowered);
    }
  }
  return result;
}

function lowerSpacer(spacer: SpacerNode, horizontalAxis: boolean | null): HtmlNode | null {
  if (horizontalAxis === null) return null; // layout-only outside a flex container

  const style: StyleEntries =
    spacer.flex > 0
      ? {
          flex: `${spacer.flex} 1 0%`,
          transition: spacer.animateChanges
            ? 'flex-grow var(--eq-motion-base) var(--eq-curve-standard)'
            : undefined,
        }
      : horizontalAxis
        ? { width: px(spacer.fixedLength), 'flex-shrink': '0' }
        : { height: px(spacer.fixedLength), 'flex-shrink': '0' };

  const node = element('div', style);
  node.attributes['aria-hidden'] = 'true';
  return node;
}

function isZeroInsets(insets: EdgeInsetsValue): boolean {
  return insets.start === 0 && insets.top === 0 && insets.end === 0 && insets.bottom === 0;
}

function isZeroRadii(radii: CornerRadiiValue): boolean {
  return (
    radii.topLeft === 0 && radii.topRight === 0 && radii.bottomRight === 0 && radii.bottomLeft === 0
  );
}

// The page-level seam lives in core/component.ts, which cannot import this module (components →
// runtime exports → component base = a cycle, and a cycle means an undefined base class). So the
// lowering hands its renderer DOWN to the leaf module instead. See component-boundary.ts.
setFailureRenderer(renderContainedFailure);
