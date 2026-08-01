/**
 * Client-side lowering of the shared abstract vocabulary to `HtmlNode` — the EXACT mirror of the C#
 * `WebRealizer` (SSR path). Hydration correctness depends on the two producing identical DOM, so
 * every rule here is normative and cross-pinned by tests on both sides:
 * - colors lower as `light-dark(#light, #dark)` straight from tokens (mode-free DOM);
 * - Box → div with `box-sizing: border-box` (Photon's inside border = the CSS parity contract);
 * - Row/Column → flex with the spec alignment defaults; Flexible → `flex: n 1 0%` + `min-size: 0`;
 * - Text → role-classed span with single-line ellipsis; Pressable → neutralized <button>;
 * - styles lower to ATOMIC CLASSES (style-atomizer.ts twin of the C# StyleAtomizer): identical
 *   declarations hash to identical class names on both sides, so hydration compares one sorted
 *   class string per element; only custom-property tails stay inline.
 */

import type { EventHandler, HtmlNode } from '../core/types';
import { installDragDismissController } from '../dom/drag-dismiss';
import { getActivePass } from './instance-store';
import { getPhotonTheme } from './photon-context';
import { atomizeEntries, atomizePseudo, ensureAdaptiveGate, mergeAtomicDeclaration } from './style-atomizer';
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
  IconNode,
  ImageNode,
  LinearGradientValue,
  DragDismissNode,
  LinkNode,
  LoopMotionNode,
  OverlayNode,
  PresenceNode,
  SpinnerNode,
  PositionedNode,
  PressableNode,
  ScrollViewNode,
  SizeValueValue,
  StackNode,
  SpacerNode,
  AdaptiveNodeValue,
  GridNode,
  AnchoredNode,
  StickyNode,
  StyleDiffValue,
  TextEntryNode,
  TextNode,
  TransformValue,
  VisualNodeValue,
} from './nodes';

export interface LoweringContext {
  /** The theme's TextPrimary token — the default Text color (mirrors `theme.TextPrimary`). */
  textPrimary: ColorTokenValue;
  /** Passed verbatim to `component.build(...)` when expanding shared components. */
  componentContext?: unknown;
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

export function tokenValue(token: ColorTokenValue): string {
  const light = hex(token.light);
  const dark = hex(token.dark);
  return light === dark ? light : `light-dark(${light}, ${dark})`;
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

const fontWeights: Record<string, number> = {
  regular: 400,
  medium: 500,
  semiBold: 600,
  bold: 700,
  extraBold: 800,
};

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
    case 'scrollView':
      return lowerScrollView(node as ScrollViewNode, context, path);
    case 'loopMotion':
      return lowerLoopMotion(node as LoopMotionNode, context, path);
    case 'presence':
      return lowerPresence(node as PresenceNode, context, horizontalAxis, path);
    case 'dragDismiss':
      return lowerDragDismiss(node as DragDismissNode, context, horizontalAxis, path);
    case 'link':
      return lowerLink(node as LinkNode, context, path);
    case 'image':
      return lowerImage(node as ImageNode);
    case 'icon':
      return lowerIcon(node as IconNode);
    case 'spinner':
      return lowerSpinner(node as SpinnerNode);
    case 'stack':
      return lowerStack(node as StackNode, context, path);
    case 'grid':
      return lowerGrid(node as unknown as GridNode, context, path);
    case 'adaptive':
      return lowerAdaptive(node as unknown as AdaptiveNodeValue, context, path);
    case 'sticky':
      return lowerSticky(node as unknown as StickyNode, context, path);
    case 'anchored':
      return lowerAnchored(node as unknown as AnchoredNode, context, path);
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
      return lowerNode(
        resolved.build(context.componentContext),
        context,
        horizontalAxis,
        path + '/0',
      );
    }
    default: {
      // Mixing seam: a WEB component (transpiled shared component or legacy HtmlElement) composed
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
function lowerLink(node: LinkNode, context: LoweringContext, path: string): HtmlNode {
  const anchor: HtmlNode = {
    tag: 'a',
    attributes: { class: 'eq-link', href: node.href },
    events: {},
    children: [],
  };
  if (node.label) anchor.attributes['aria-label'] = node.label;
  const child = lowerNode(node.child, context, null, path + '/0');
  if (child) anchor.children.push(child);
  return anchor;
}

/** Gestures v2 mirror: the drag marker + dismiss event ride the child's own root (no wrapper —
 * identical DOM to the C# SSR realizer). The pointer-capture controller installs lazily on the
 * first lowering: it drives the follow/glide and dispatches `eq-drag-dismiss` past the threshold,
 * which the reconciler-attached handler resolves into the component's onDismiss. */
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
  const child = lowerNode(node.child, context, null, path + '/0');
  if (child) layer.children.push(child);
  return layer;
}

/** Spec B9/B10 mirror: the REAL chrome-less <input> — identical DOM to the C# SSR realizer, plus
 * the client-only handlers (input → onChanged, Enter → onSubmit, focus/blur → onFocusChanged). */
function lowerTextEntry(node: TextEntryNode, context: LoweringContext): HtmlNode {
  const input = element('input', {
    width: '100%',
    padding: '0',
    background: 'none',
    border: 'none',
    color: tokenValue(context.textPrimary),
    'font-family': 'inherit',
  });
  prependClass(input, `eq-entry eq-type-${node.role.toLowerCase()}`);
  input.attributes['type'] = node.obscure === true ? 'password' : 'text';
  input.attributes['value'] = node.value;
  if (node.placeholder != null) input.attributes['placeholder'] = node.placeholder;
  if (node.disabled === true) {
    input.attributes['disabled'] = '';
    return input;
  }
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
  return input;
}

/** Mirror of C# TokenCss.Gradient: 2-stop linear-gradient with light-dark() stops. */
function gradientValue(gradient: LinearGradientValue): string {
  const direction =
    gradient.direction === 'toBottom'
      ? 'to bottom'
      : gradient.direction === 'toBottomRight'
        ? 'to bottom right'
        : gradient.direction === 'toBottomLeft'
          ? 'to bottom left'
          : 'to right';
  return `linear-gradient(${direction}, ${tokenValue(gradient.from)}, ${tokenValue(gradient.to)})`;
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
function lowerScrollView(node: ScrollViewNode, context: LoweringContext, path: string): HtmlNode {
  const children: HtmlNode[] = [];
  const child = lowerNode(node.child, context, null, path + '/0');
  if (child) children.push(child);
  return element(
    'div',
    {
      width: sizeValue(node.width),
      height: sizeValue(node.height),
      'overflow-y': node.axis === 'horizontal' ? 'hidden' : 'auto',
      'overflow-x': node.axis === 'vertical' ? 'hidden' : 'auto',
    },
    children,
  );
}

/** Spec A11 mirror: explicitly sized <img> with object-fit and the rrect clip. */
function lowerImage(node: ImageNode): HtmlNode {
  const fit = node.fit === 'contain' ? 'contain' : node.fit === 'stretch' ? 'fill' : 'cover';
  const radius = node.cornerRadius;
  const hasRadius =
    !!radius &&
    (radius.topLeft > 0 || radius.topRight > 0 || radius.bottomRight > 0 || radius.bottomLeft > 0);
  return {
    tag: 'img',
    attributes: {
      ...atomicAttrs({
        width: px(node.width),
        height: px(node.height),
        'border-radius': hasRadius && radius ? radiusValue(radius) : undefined,
        'object-fit': fit,
      }),
      src: node.source,
      alt: node.alt ?? '',
    },
    events: {},
    children: [],
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
  const glyph = node.glyph;
  const attributes: Record<string, string | undefined> = {
    ...atomicAttrs({
      width: px(node.size),
      height: px(node.size),
      color: node.color ? tokenValue(node.color) : undefined,
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
  if (node.label) attributes['aria-label'] = node.label;
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
    const child = stackChildren[i];
    if (child.nodeKind === 'positioned') {
      const positioned = child as PositionedNode;
      const lowered = lowerNode(positioned.child, context, null, path + '/' + i + '/0');
      if (!lowered) continue;
      children.push(
        element(
          'div',
          {
            position: 'absolute',
            // Spec S7: explicit stacking — flow order otherwise (painter's parity).
            'z-index': (positioned.zIndex ?? 0) !== 0 ? `${positioned.zIndex}` : undefined,
            top: positioned.top != null ? px(positioned.top) : undefined,
            right: positioned.end != null ? px(positioned.end) : undefined,
            bottom: positioned.bottom != null ? px(positioned.bottom) : undefined,
            left: positioned.start != null ? px(positioned.start) : undefined,
          },
          [lowered],
        ),
      );
    } else {
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
    },
    children,
  );
}

function textLeaf(content: string): HtmlNode {
  return { tag: '#text', attributes: {}, events: {}, children: [], textContent: content };
}

function lowerBox(box: BoxNode, context: LoweringContext, path: string): HtmlNode {
  const style = box.style ?? ({} as BoxStyleValue);
  const result = element('div', {
    'box-sizing': 'border-box',
    width: sizeValue(style.width),
    height: sizeValue(style.height),
    'min-width': style.minWidth && style.minWidth > 0 ? px(style.minWidth) : undefined,
    'min-height': style.minHeight && style.minHeight > 0 ? px(style.minHeight) : undefined,
    'max-width': style.maxWidth && style.maxWidth > 0 ? px(style.maxWidth) : undefined,
    'max-height': style.maxHeight && style.maxHeight > 0 ? px(style.maxHeight) : undefined,
    padding:
      style.padding && !isZeroInsets(style.padding) ? paddingValue(style.padding) : undefined,
    'background-color': style.background ? tokenValue(style.background) : undefined,
    'background-image': style.gradient ? gradientValue(style.gradient) : undefined,
    'border-radius':
      style.cornerRadius && !isZeroRadii(style.cornerRadius)
        ? radiusValue(style.cornerRadius)
        : undefined,
    'box-shadow': (() => {
      if (!style.elevation || style.elevation <= 0) return undefined;
      const spec = getPhotonTheme().elevation(style.elevation);
      if (!spec || (spec.blur === 0 && spec.offsetY === 0 && spec.spread === 0)) return undefined;
      return `0 ${px(spec.offsetY)} ${px(spec.blur)} ${px(spec.spread)} ${tokenValue(spec.color)}`;
    })(),
    border:
      style.borderWidth && style.borderWidth > 0 && style.borderColor
        ? `${px(style.borderWidth)} solid ${tokenValue(style.borderColor)}`
        : undefined,
    // The container side of loop motion: children clip to the rrect (native PushClip twin).
    overflow: style.clip ? 'hidden' : undefined,
    // Spec S1 — group opacity, center-anchored static transform, one-axis-derives aspect ratio.
    opacity: style.opacity != null && style.opacity < 1 ? num(style.opacity) : undefined,
    transform: style.transform ? transformValue(style.transform) : undefined,
    'aspect-ratio': style.aspectRatio && style.aspectRatio > 0 ? num(style.aspectRatio) : undefined,
  });

  if (style.hover) appendDiff(result, ':hover', style.hover);
  if (style.focus) appendDiff(result, ':focus-visible', style.focus);

  if (box.child) {
    const child = lowerNode(box.child, context, null, path + '/0');
    if (child) result.children.push(child);
  }
  return result;
}

/** Spec S5 mirror of the C# AppendDiff — identical declaration strings, pseudo-hashed classes. */
function appendDiff(node: HtmlNode, pseudo: string, diff: StyleDiffValue): void {
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
  const classes = atomizePseudo(pseudo, entries);
  if (classes) {
    const existing = node.attributes['class'];
    node.attributes['class'] = existing ? `${existing} ${classes}` : classes;
  }
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

function lowerText(text: TextNode, context: LoweringContext): HtmlNode {
  const style: StyleEntries = {
    color: tokenValue(text.color ?? context.textPrimary),
  };

  // Single line → shaping-style ellipsis (spec A8).
  if (text.maxLines === 1) {
    style['white-space'] = 'nowrap';
    style.overflow = 'hidden';
    style['text-overflow'] = 'ellipsis';
  }

  // System table override (e.g. Button labels) — inline styles beat the role class.
  if (text.styleOverride) {
    const override = text.styleOverride;
    style['font-size'] = px(override.size);
    style['line-height'] = px(override.lineHeight);
    const weight =
      typeof override.weight === 'number' ? override.weight : (fontWeights[override.weight] ?? 400);
    style['font-weight'] = String(weight);
    style['letter-spacing'] = px(override.tracking);
  }

  const node = element('span', style, [textLeaf(text.content)]);
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
  const node = element('button', {
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

  if (pressable.label) node.attributes['aria-label'] = pressable.label;
  if (disabled) node.attributes['disabled'] = '';
  if (!disabled && pressable.onPressed) node.events['click'] = pressable.onPressed as EventHandler;

  // Interaction states (spec §01): mechanics live in the generated stylesheet — every enabled
  // pressable carries the class (:focus-visible double ring is an a11y DEFAULT); the pressed swap
  // additionally ships its token value as a custom property at the style TAIL (the C# cross-pin).
  if (!disabled) {
    prependClass(node, 'eq-pressable');
    if (pressable.pressedBackground) {
      const tail = `--eq-pressed-bg: ${tokenValue(pressable.pressedBackground)}`;
      const existing = node.attributes['style'];
      node.attributes['style'] = existing ? `${existing}; ${tail}` : tail;
    }
  }

  const child = lowerNode(pressable.child, context, null, path + '/0');
  if (child) node.children.push(child);
  return node;
}

function lowerFlexible(
  flexible: FlexibleNode,
  context: LoweringContext,
  horizontalAxis: boolean | null,
  path: string,
): HtmlNode {
  // flex: n 1 0% — basis 0 matches the native leftover-by-weight distribution; min-size 0 lets text
  // shrink to ellipsis instead of pushing siblings (the truncation contract).
  const node = element('div', {
    flex: `${flexible.flex} 1 0%`,
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
  if (anchor) host.children.push(anchor);
  if (node.openOnHover === true) {
    host.children.push(buildAnchorPanel(node, context, path));
    return host;
  }
  if (node.open !== true) return host;

  if (node.onDismiss) {
    const scrim = lowerNode(
      {
        nodeKind: 'pressable',
        child: { nodeKind: 'box' },
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
      host.children.push(scrim);
    }
  }

  host.children.push(buildAnchorPanel(node, context, path));
  return host;
}

/** The C# BuildAnchorPanel twin. */
function buildAnchorPanel(node: AnchoredNode, context: LoweringContext, path: string): HtmlNode {
  const top =
    node.placement === 'topStart' || node.placement === 'topEnd' || node.placement === 'topCenter';
  const gap = node.gap ?? 4;
  const panel = element('div', top ? { 'margin-bottom': px(gap) } : { 'margin-top': px(gap) });
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

function lowerSticky(node: StickyNode, context: LoweringContext, path: string): HtmlNode {
  const wrapper = element('div', {
    position: 'sticky',
    top: px(node.offset),
    'z-index': '1',
  });
  const child = lowerNode(node.child, context, null, path + '/0');
  if (child) wrapper.children.push(child);
  return wrapper;
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
  addVariant(node.compact, node.medium ? 'eq-vc6' : 'eq-vc8', 0);
  if (node.medium) addVariant(node.medium, node.expanded ? 'eq-vm8' : 'eq-vm', 1);
  if (node.expanded) addVariant(node.expanded, 'eq-vx', 2);
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
