/**
 * The DOM half of the canvas painter, in its own module because TWO paths need it: the lowering,
 * which builds a tree of nodes for the renderer to write, and the surface controller, which draws
 * straight into an element it has just measured. One implementation of every shape — a second one
 * is how two targets drift apart.
 */

import type { HtmlNode } from '../core/types';
import type { ColorTokenValue, ICanvasPainter } from './nodes';
import { num, tokenValue } from './css-values';

/**
 * The DOM half of the canvas painter: every call becomes one SVG child, in call order, which is
 * paint order — exactly as the display list treats it on Photon.
 *
 * Colours stay `light-dark(...)` rather than being resolved: this target has a cascade to defer to,
 * so a canvas follows the theme the way every other colour here does.
 */
export class DomCanvasPainter implements ICanvasPainter {
  readonly shapes: HtmlNode[] = [];

  constructor(
    readonly width: number,
    readonly height: number,
  ) {}

  private shape(tag: string, attributes: Record<string, string | undefined>): void {
    this.shapes.push({ tag, attributes, events: {}, children: [] });
  }

  /**
   * The same shapes as real SVG ELEMENTS, for the path that draws into a measured element rather
   * than into a tree the renderer will write. One painter, two consumers — the alternative was a
   * second implementation of every shape, which is how two targets drift.
   */
  elements(): SVGElement[] {
    const NS = 'http://www.w3.org/2000/svg';
    return this.shapes.map((shape) => {
      const element = document.createElementNS(NS, shape.tag);
      for (const [name, value] of Object.entries(shape.attributes ?? {})) {
        if (value !== undefined) element.setAttribute(name, value);
      }
      return element;
    });
  }

  fillRect(x: number, y: number, width: number, height: number, color: ColorTokenValue, cornerRadius = 0): void {
    this.shape('rect', {
      x: num(x), y: num(y), width: num(width), height: num(height),
      fill: tokenValue(color),
      rx: cornerRadius > 0 ? num(cornerRadius) : undefined,
    });
  }

  strokeRect(x: number, y: number, width: number, height: number, color: ColorTokenValue,
    strokeWidth: number, cornerRadius = 0): void {
    // Inset by half the stroke: SVG centres a stroke on its path, and every border in this
    // framework is drawn INSIDE its bounds (the C# painter says the same, for the same reason).
    const inset = strokeWidth / 2;
    this.shape('rect', {
      x: num(x + inset), y: num(y + inset),
      width: num(Math.max(0, width - strokeWidth)),
      height: num(Math.max(0, height - strokeWidth)),
      fill: 'none', stroke: tokenValue(color), 'stroke-width': num(strokeWidth),
      rx: cornerRadius > 0 ? num(Math.max(0, cornerRadius - inset)) : undefined,
    });
  }

  fillCircle(centerX: number, centerY: number, radius: number, color: ColorTokenValue): void {
    this.shape('circle', {
      cx: num(centerX), cy: num(centerY), r: num(radius), fill: tokenValue(color),
    });
  }

  fillAnnularSector(centerX: number, centerY: number, innerRadius: number, outerRadius: number,
    startAngle: number, endAngle: number, color: ColorTokenValue, cornerSmoothing = 0): void {
    // The guards and clamps are the ENGINE's (C# DisplayList.FillAnnularSector), because a target
    // that quietly drew a reversed sector — or inked a hairline where the band has no width —
    // would break the write-once promise in the one place nobody looks: degenerate input.
    if (outerRadius <= 0 || endAngle <= startAngle || innerRadius >= outerRadius) return;
    innerRadius = Math.min(Math.max(innerRadius, 0), outerRadius);
    endAngle = Math.min(endAngle, startAngle + Math.PI * 2);
    cornerSmoothing = Math.min(Math.max(cornerSmoothing, 0), (outerRadius - innerRadius) / 2);

    const sweep = endAngle - startAngle;
    // A full ring cannot be one arc — start and end coincide, and SVG draws nothing. The smoothing
    // is NOT forwarded to the halves: a full ring has no corners on Photon, so rounding them would
    // draw a seam that exists on one target only.
    if (sweep >= Math.PI * 2 - 1e-4) {
      this.fillAnnularSector(centerX, centerY, innerRadius, outerRadius, startAngle, startAngle + Math.PI, color);
      this.fillAnnularSector(centerX, centerY, innerRadius, outerRadius, startAngle + Math.PI, startAngle + Math.PI * 2, color);
      return;
    }

    const at = (radius: number, angle: number): [number, number] => [
      centerX + radius * Math.cos(angle),
      centerY + radius * Math.sin(angle),
    ];
    const largeArc = Math.abs(sweep) > Math.PI ? 1 : 0;
    const clockwise = sweep > 0 ? 1 : 0;
    const [ox1, oy1] = at(outerRadius, startAngle);
    const [ox2, oy2] = at(outerRadius, endAngle);
    const [ix2, iy2] = at(innerRadius, endAngle);
    const [ix1, iy1] = at(innerRadius, startAngle);

    const d = innerRadius <= 0
      ? `M ${num(centerX)} ${num(centerY)} L ${num(ox1)} ${num(oy1)} `
        + `A ${num(outerRadius)} ${num(outerRadius)} 0 ${largeArc} ${clockwise} ${num(ox2)} ${num(oy2)} Z`
      : `M ${num(ox1)} ${num(oy1)} `
        + `A ${num(outerRadius)} ${num(outerRadius)} 0 ${largeArc} ${clockwise} ${num(ox2)} ${num(oy2)} `
        + `L ${num(ix2)} ${num(iy2)} `
        + `A ${num(innerRadius)} ${num(innerRadius)} 0 ${largeArc} ${1 - clockwise} ${num(ix1)} ${num(iy1)} Z`;

    this.shape('path', {
      d,
      fill: tokenValue(color),
      stroke: cornerSmoothing > 0 ? tokenValue(color) : undefined,
      'stroke-width': cornerSmoothing > 0 ? num(cornerSmoothing) : undefined,
      'stroke-linejoin': cornerSmoothing > 0 ? 'round' : undefined,
    });
  }

  line(x1: number, y1: number, x2: number, y2: number, color: ColorTokenValue, strokeWidth: number): void {
    this.shape('line', {
      x1: num(x1), y1: num(y1), x2: num(x2), y2: num(y2),
      stroke: tokenValue(color), 'stroke-width': num(strokeWidth),
    });
  }
}


/** A painter over a box of the given size. */
export function paintCanvas(width: number, height: number): DomCanvasPainter {
  return new DomCanvasPainter(width, height);
}
