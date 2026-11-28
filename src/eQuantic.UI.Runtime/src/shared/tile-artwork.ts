/**
 * The SVG a drawing's shapes are written with, and the tile a `DrawingPattern` repeats as a data URL
 * (#1937) — the twins of the C# `DrawingElements`, `TileArtwork`, `SvgMarkup` and `DataUrl`, which
 * write the same bytes, so a tile's layer hashes to the same atomic class on both producers.
 *
 * A leaf beside `lowering`: it imports the CSS spellings and the hash and nothing that imports it.
 */

import type { HtmlNode } from '../core/types';
import { hex, num } from './css-values';
import { hashDeclaration } from './style-atomizer';
import type { ColorValue, VectorDrawingValue, VectorGradientValue, VectorPaintValue } from './nodes';

/** Whether a paint is a run of colours rather than one — the twin of `VectorPaint.IsGradient`. */
export function isGradient(value: VectorPaintValue): boolean {
  return (value.kind === 'linearGradient' || value.kind === 'radialGradient') && !!value.gradient;
}

/**
 * A gradient's id, byte for byte with the C# realizer: the FNV hash of everything that makes the
 * run what it is. Content-addressed rather than counted — SSR and this twin do not agree about how
 * many drawings came before, and a mismatched id repaints every path that names it.
 */
export function gradientId(run: VectorGradientValue): string {
  let text = `${run.userSpace ? 'u' : 'o'}|${num(run.x1)},${num(run.y1)},${num(run.x2)},${num(run.y2)},${num(run.radius)}`;
  for (const stop of run.stops) text += `|${num(stop.offset)}:${hex(stop.color)}`;
  return `eq-g-${hashDeclaration(text)}`;
}

/** The C# `WebRealizer.PaintCss`: a paint as an SVG paint. */
function paintCss(value: VectorPaintValue): string {
  return value.kind === 'inherit'
    ? 'currentColor'
    : value.kind === 'solid'
      ? hex(value.color)
      : isGradient(value)
        ? `url(#${gradientId(value.gradient!)})`
        : 'none';
}

/** The C# `WebRealizer.Gradients`: every run the artwork paints with, by id, in first-use order. */
export function drawingRuns(artwork: VectorDrawingValue): Map<string, VectorPaintValue> {
  const runs = new Map<string, VectorPaintValue>();
  for (const shape of artwork.shapes ?? [])
    for (const value of [shape.fill, shape.stroke])
      if (isGradient(value) && !runs.has(gradientId(value.gradient!)))
        runs.set(gradientId(value.gradient!), value);
  return runs;
}

/** One paint server, in SVG's own words — the twin of the C# GradientElement. */
export function gradientElement(id: string, paint: VectorPaintValue): HtmlNode {
  const run = paint.gradient!;
  const radial = paint.kind === 'radialGradient';
  const attributes: Record<string, string | undefined> = { id };
  if (radial) {
    attributes['cx'] = num(run.x1);
    attributes['cy'] = num(run.y1);
    attributes['r'] = num(run.radius);
  } else {
    attributes['x1'] = num(run.x1);
    attributes['y1'] = num(run.y1);
    attributes['x2'] = num(run.x2);
    attributes['y2'] = num(run.y2);
  }
  if (run.userSpace) attributes['gradientUnits'] = 'userSpaceOnUse';

  return {
    tag: radial ? 'radialGradient' : 'linearGradient',
    attributes,
    events: {},
    children: run.stops.map((stop) => {
      const stopAttributes: Record<string, string | undefined> = {
        offset: num(stop.offset),
        'stop-color': hex({ ...stop.color, a: 255 }),
      };
      if (stop.color.a < 255) stopAttributes['stop-opacity'] = num(stop.color.a / 255);
      return { tag: 'stop', attributes: stopAttributes, events: {}, children: [] };
    }),
  };
}

/**
 * The C# `DrawingElements.Shapes`: one `<path>` per shape, with the paint the artwork chose — the
 * picture alone, shared by the inline drawing and the tile. `currentColor` stays the WORD, so the
 * colour of whatever holds the shapes answers it.
 */
export function drawingShapes(artwork: VectorDrawingValue): HtmlNode[] {
  // `currentColor` has no alpha of its own — the C# realizer says why, and the two must agree.
  const alpha = (value: VectorPaintValue): number => (value.kind === 'none' ? 0 : value.color.a / 255);
  return (artwork.shapes ?? []).map((shape) => {
    const attributes: Record<string, string | undefined> = { d: shape.path, fill: paintCss(shape.fill) };
    if (shape.fill.kind === 'inherit' && alpha(shape.fill) < 1) attributes['fill-opacity'] = num(alpha(shape.fill));
    if (shape.stroke.kind !== 'none') {
      attributes['stroke'] = paintCss(shape.stroke);
      if (shape.stroke.kind === 'inherit' && alpha(shape.stroke) < 1)
        attributes['stroke-opacity'] = num(alpha(shape.stroke));
      attributes['stroke-width'] = num(shape.strokeWidth);
      attributes['stroke-linecap'] = 'round';
      attributes['stroke-linejoin'] = 'round';
    }
    if (shape.evenOdd) attributes['fill-rule'] = 'evenodd';
    if (shape.opacity < 1) attributes['opacity'] = num(shape.opacity);
    return { tag: 'path', attributes, events: {}, children: [] };
  });
}

/** The C# `DrawingElements.Inherits`: whether a shape is painted in the colour that holds it. */
export function drawingInherits(artwork: VectorDrawingValue): boolean {
  return (artwork.shapes ?? []).some((shape) => shape.fill.kind === 'inherit' || shape.stroke.kind === 'inherit');
}

/**
 * The C# `TileArtwork.Document`: the tile's standalone SVG, the artwork on its own grid, its shapes in
 * one group below an opacity of 1, and `color` stated only when a shape inherits it.
 */
export function tileArtwork(artwork: VectorDrawingValue, opacity: number, tint: ColorValue): HtmlNode {
  const attributes: Record<string, string | undefined> = {
    xmlns: 'http://www.w3.org/2000/svg',
    viewBox: `${num(artwork.minX)} ${num(artwork.minY)} ${num(artwork.width)} ${num(artwork.height)}`,
    width: num(artwork.width),
    height: num(artwork.height),
    preserveAspectRatio: 'none',
    fill: 'none',
  };
  if (drawingInherits(artwork)) attributes['color'] = hex(tint);
  const children: HtmlNode[] = [];
  const runs = drawingRuns(artwork);
  if (runs.size > 0)
    children.push({
      tag: 'defs',
      attributes: {},
      events: {},
      children: [...runs].map(([id, value]) => gradientElement(id, value)),
    });
  const shapes = drawingShapes(artwork);
  if (opacity < 1) children.push({ tag: 'g', attributes: { opacity: num(opacity) }, events: {}, children: shapes });
  else children.push(...shapes);
  return { tag: 'svg', attributes, events: {}, children };
}

/**
 * The C# `SvgMarkup`: attributes single-quoted in the order they were set, `&`, `'` and `<` as
 * entities, an element with no child closed on itself, and no whitespace between elements.
 */
export function svgMarkup(node: HtmlNode): string {
  let text = `<${node.tag}`;
  for (const [name, value] of Object.entries(node.attributes)) {
    if (value === undefined) continue;
    text += ` ${name}='${value.replace(/[&'<]/g, (c) => (c === '&' ? '&amp;' : c === "'" ? '&apos;' : '&lt;'))}'`;
  }
  if (node.children.length === 0) return `${text}/>`;
  return `${text}>${node.children.map((child) => svgMarkup(child as HtmlNode)).join('')}</${node.tag}>`;
}

const UTF8 = new TextEncoder();

/**
 * The C# `DataUrl.Encode`, by its table: `%`, `#`, `"`, `<`, `>`, every control character (0 to 31,
 * and 127) and every character past ASCII, as its UTF-8 bytes, become `%XX` with uppercase digits,
 * and everything else stays as it is. Not `encodeURIComponent`, which leaves `'()*!` where .NET's
 * own escaping does not, and escapes far more than this needs.
 */
export function dataUrlText(text: string): string {
  let out = '';
  for (const character of text) {
    const code = character.codePointAt(0)!;
    if (code < 0x80) {
      out += code < 0x20 || code === 0x7f || '%#"<>'.includes(character) ? escapeByte(code) : character;
    } else {
      for (const byte of UTF8.encode(character)) out += escapeByte(byte);
    }
  }
  return out;
}

function escapeByte(byte: number): string {
  return `%${byte.toString(16).toUpperCase().padStart(2, '0')}`;
}

/**
 * The C# `DataUrl.Css`: a URL as a CSS `url("…")`, a CSS string where `\` and `"` are escaped with a
 * backslash, and every control character (0 to 31, and 127), `{` and `}` as its code point in
 * hexadecimal and a space (`\A `, `\1 `, `\7B `). A raw control or brace was read as part of the
 * sheet's structure when its text was read again (the server's state marker, this registry's
 * adoption of `.class{`), not as part of the URL.
 */
export function cssUrl(url: string): string {
  let out = 'url("';
  for (const c of url) {
    const code = c.codePointAt(0)!;
    out +=
      c === '\\' || c === '"' ? `\\${c}`
        : code < 0x20 || code === 0x7f || c === '{' || c === '}' ? `\\${code.toString(16).toUpperCase()} `
          : c;
  }
  return `${out}")`;
}

/** The C# data URL of a tile's document: `data:image/svg+xml,` and its markup through the table. */
export function svgDataUrl(document: HtmlNode): string {
  return `data:image/svg+xml,${dataUrlText(svgMarkup(document))}`;
}
