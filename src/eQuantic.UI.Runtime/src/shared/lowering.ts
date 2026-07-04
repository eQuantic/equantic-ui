/**
 * Client-side lowering of the shared abstract vocabulary to `HtmlNode` — the EXACT mirror of the C#
 * `WebRealizer` (SSR path). Hydration correctness depends on the two producing identical DOM, so
 * every rule here is normative and cross-pinned by tests on both sides:
 * - colors lower as `light-dark(#light, #dark)` straight from tokens (mode-free DOM);
 * - Box → div with `box-sizing: border-box` (Photon's inside border = the CSS parity contract);
 * - Row/Column → flex with the spec alignment defaults; Flexible → `flex: n 1 0%` + `min-size: 0`;
 * - Text → role-classed span with single-line ellipsis; Pressable → neutralized <button>;
 * - style STRINGS follow the C# `HtmlStyle.ToCssString()` property order, byte for byte.
 */

import type { EventHandler, HtmlNode } from '../core/types';
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
  PressableNode,
  SizeValueValue,
  SpacerNode,
  TextNode,
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
  return (
    lowerNode(node, context, null) ?? {
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

const styleOrder = [
  'display', 'flex-direction', 'justify-content', 'align-items', 'gap', 'flex', 'flex-grow',
  'flex-shrink', 'width', 'height', 'min-width', 'min-height', 'max-width', 'max-height', 'padding',
  'background', 'background-color', 'border', 'border-radius', 'color', 'font-family', 'font-size',
  'font-weight', 'line-height', 'text-align', 'letter-spacing', 'box-shadow', 'opacity', 'cursor',
  'overflow', 'white-space', 'text-overflow', 'box-sizing',
] as const;

function styleString(entries: StyleEntries): string {
  const parts: string[] = [];
  for (const name of styleOrder) {
    const value = entries[name];
    if (value !== undefined) parts.push(`${name}: ${value}`);
  }
  return parts.join('; ');
}

// ---- node lowering --------------------------------------------------------------------------------

function lowerNode(
  node: VisualNodeValue,
  context: LoweringContext,
  horizontalAxis: boolean | null,
): HtmlNode | null {
  switch (node.nodeKind) {
    case 'box':
      return lowerBox(node as BoxNode, context);
    case 'row':
    case 'column':
      return lowerFlex(node as FlexNodeValue, context);
    case 'text':
      return lowerText(node as TextNode, context);
    case 'pressable':
      return lowerPressable(node as PressableNode, context);
    case 'flexible':
      return lowerFlexible(node as FlexibleNode, context, horizontalAxis);
    case 'spacer':
      return lowerSpacer(node as SpacerNode, horizontalAxis);
    case 'component':
      return lowerNode((node as ComponentNode).build(context.componentContext), context, horizontalAxis);
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
    attributes: { style: styleString(style) },
    events: {},
    children,
  };
}

function textLeaf(content: string): HtmlNode {
  return { tag: '#text', attributes: {}, events: {}, children: [], textContent: content };
}

function lowerBox(box: BoxNode, context: LoweringContext): HtmlNode {
  const style = box.style ?? ({} as BoxStyleValue);
  const result = element('div', {
    'box-sizing': 'border-box',
    width: sizeValue(style.width),
    height: sizeValue(style.height),
    'min-width': style.minWidth && style.minWidth > 0 ? px(style.minWidth) : undefined,
    'min-height': style.minHeight && style.minHeight > 0 ? px(style.minHeight) : undefined,
    'max-width': style.maxWidth && style.maxWidth > 0 ? px(style.maxWidth) : undefined,
    'max-height': style.maxHeight && style.maxHeight > 0 ? px(style.maxHeight) : undefined,
    padding: style.padding && !isZeroInsets(style.padding) ? paddingValue(style.padding) : undefined,
    'background-color': style.background ? tokenValue(style.background) : undefined,
    'border-radius':
      style.cornerRadius && !isZeroRadii(style.cornerRadius) ? radiusValue(style.cornerRadius) : undefined,
    border:
      style.borderWidth && style.borderWidth > 0 && style.borderColor
        ? `${px(style.borderWidth)} solid ${tokenValue(style.borderColor)}`
        : undefined,
  });

  if (box.child) {
    const child = lowerNode(box.child, context, null);
    if (child) result.children.push(child);
  }
  return result;
}

function lowerFlex(flex: FlexNodeValue, context: LoweringContext): HtmlNode {
  const horizontal = flex.nodeKind === 'row';
  const result = element('div', {
    'box-sizing': 'border-box',
    display: 'flex',
    'flex-direction': horizontal ? 'row' : 'column',
    gap: flex.gap > 0 ? px(flex.gap) : undefined,
    'justify-content': mainAlign(flex.main),
    'align-items': crossAlign(flex.cross),
    width: sizeValue(flex.width),
    height: sizeValue(flex.height),
    padding: flex.padding && !isZeroInsets(flex.padding) ? paddingValue(flex.padding) : undefined,
    'background-color': flex.background ? tokenValue(flex.background) : undefined,
    'border-radius':
      flex.cornerRadius && !isZeroRadii(flex.cornerRadius) ? radiusValue(flex.cornerRadius) : undefined,
  });

  for (const child of flex.children) {
    const lowered = lowerNode(child, context, horizontal);
    if (lowered) result.children.push(lowered);
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
  node.attributes['class'] = `eq-type-${text.role.toLowerCase()}`;
  return node;
}

function lowerPressable(pressable: PressableNode, context: LoweringContext): HtmlNode {
  const disabled = pressable.disabled === true;
  const node = element('button', {
    padding: '0',
    background: 'none',
    border: 'none',
    'font-family': 'inherit',
    'text-align': 'start',
    cursor: disabled ? undefined : 'pointer',
  });

  if (pressable.label) node.attributes['aria-label'] = pressable.label;
  if (disabled) node.attributes['disabled'] = '';
  if (!disabled && pressable.onPressed) node.events['click'] = pressable.onPressed as EventHandler;

  const child = lowerNode(pressable.child, context, null);
  if (child) node.children.push(child);
  return node;
}

function lowerFlexible(
  flexible: FlexibleNode,
  context: LoweringContext,
  horizontalAxis: boolean | null,
): HtmlNode {
  // flex: n 1 0% — basis 0 matches the native leftover-by-weight distribution; min-size 0 lets text
  // shrink to ellipsis instead of pushing siblings (the truncation contract).
  const node = element('div', {
    flex: `${flexible.flex} 1 0%`,
    'min-width': horizontalAxis !== false ? '0' : undefined,
    'min-height': horizontalAxis === false ? '0' : undefined,
  });
  const child = lowerNode(flexible.child, context, horizontalAxis);
  if (child) node.children.push(child);
  return node;
}

function lowerSpacer(spacer: SpacerNode, horizontalAxis: boolean | null): HtmlNode | null {
  if (horizontalAxis === null) return null; // layout-only outside a flex container

  const style: StyleEntries =
    spacer.flex > 0
      ? { flex: `${spacer.flex} 1 0%` }
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
