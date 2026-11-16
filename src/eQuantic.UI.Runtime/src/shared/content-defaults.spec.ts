/**
 * Content takes the look its component hands down (#1577): a Text and an Icon that name nothing are
 * drawn in the look of the nearest box that states one, as the C# server lowering draws them.
 */

import { beforeEach, describe, expect, it } from 'vitest';
import { StatefulComponent } from '../core/component';
import type { HtmlNode } from '../core/types';
import { ContentStyle } from './content-style';
import { lowerVisualNode } from './lowering';
import { componentLowerings } from './lowered-records';
import { effectiveStyle } from './style-atomizer';
import { ambientLoweringContext } from './photon-context';
import type { VisualNodeValue } from './nodes';
import { Box, BoxStyle, Column, Icon, IconGlyph, Text, VisualNode } from './vocabulary';
import { TypeStyle } from './value-types';

const lower = (node: unknown): HtmlNode => lowerVisualNode(node as VisualNodeValue, ambientLoweringContext());

const boxOf = (content: ContentStyle | null, child: unknown): Box =>
  new Box(new BoxStyle({ content }), child as VisualNode);

/** The first node in the tree whose class list names `cls`, or null. */
function find(node: HtmlNode, test: (n: HtmlNode) => boolean): HtmlNode | null {
  if (test(node)) return node;
  for (const child of node.children ?? []) {
    if (typeof child === 'object' && child !== null) {
      const hit = find(child as HtmlNode, test);
      if (hit) return hit;
    }
  }
  return null;
}
const classOf = (n: HtmlNode) => n.attributes['class'] ?? '';
const textNode = (root: HtmlNode) => find(root, (n) => /eq-type-/.test(classOf(n)))!;
const svgOf = (root: HtmlNode) => find(root, (n) => n.tag === 'svg')!;

describe('a text that names no look takes its box’s', () => {
  it('takes the role, the line limit and the alignment of the box', () => {
    const root = lower(boxOf(new ContentStyle({ role: 'label', maxLines: 1, align: 'center' }), new Text('Save')));
    const text = textNode(root);
    expect(classOf(text)).toContain('eq-type-label');
    const style = effectiveStyle(text);
    expect(style).toContain('text-align: center');
    expect(style).toContain('text-overflow: ellipsis');
  });

  it('takes a multi-line clamp', () => {
    const text = textNode(lower(boxOf(new ContentStyle({ maxLines: 3 }), new Text('Many lines'))));
    expect(effectiveStyle(text)).toContain('-webkit-line-clamp: 3');
  });

  it('takes the box’s type style as an override when it took the box’s role', () => {
    const type = new TypeStyle(15, 20, 'semiBold');
    const text = textNode(lower(boxOf(new ContentStyle({ role: 'label', type }), new Text('Save'))));
    expect(effectiveStyle(text)).toContain('font-size: 15px');
  });

  it('keeps a role it names whole: the box’s type does not reach it', () => {
    const type = new TypeStyle(15, 20, 'semiBold');
    const text = textNode(lower(boxOf(new ContentStyle({ role: 'label', type }), new Text('Note', 'caption'))));
    expect(classOf(text)).toContain('eq-type-caption');
    expect(classOf(text)).not.toContain('eq-type-label');
    expect(effectiveStyle(text)).not.toContain('font-size');
  });

  it('keeps its own line limit and alignment over the box’s', () => {
    const own = new Text('x', null, null, 2, 'end');
    const text = textNode(lower(boxOf(new ContentStyle({ maxLines: 1, align: 'center' }), own)));
    const style = effectiveStyle(text);
    expect(style).toContain('-webkit-line-clamp: 2');
    expect(style).toContain('text-align: end');
  });
});

describe('nested boxes merge, member by member', () => {
  it('a row that states only the line limit keeps the role of its button', () => {
    const inner = boxOf(new ContentStyle({ maxLines: 1 }), new Text('Save'));
    const text = textNode(lower(boxOf(new ContentStyle({ role: 'label' }), inner)));
    expect(classOf(text)).toContain('eq-type-label');
    expect(effectiveStyle(text)).toContain('text-overflow: ellipsis');
  });

  it('an inner box that names a role leaves the outer box’s type behind', () => {
    const type = new TypeStyle(15, 20, 'semiBold');
    const inner = boxOf(new ContentStyle({ role: 'caption' }), new Text('x'));
    const text = textNode(lower(boxOf(new ContentStyle({ role: 'label', type }), inner)));
    expect(classOf(text)).toContain('eq-type-caption');
    expect(effectiveStyle(text)).not.toContain('font-size');
  });

  it('merge is null when both are, the other when one is, and over’s members win', () => {
    expect(ContentStyle.merge(null, null)).toBeNull();
    const a = new ContentStyle({ role: 'label' });
    expect(ContentStyle.merge(a, null)).toBe(a);
    expect(ContentStyle.merge(null, a)).toBe(a);
    const merged = ContentStyle.merge(new ContentStyle({ role: 'label', iconSize: 20 }), new ContentStyle({ role: 'title' }))!;
    expect(merged.role).toBe('title');
    expect(merged.iconSize).toBe(20);
  });
});

describe('nothing stated lowers as before', () => {
  it('a text in no box that states a look is bodyL with no clamp and no alignment', () => {
    const text = textNode(lower(boxOf(null, new Text('Body'))));
    expect(classOf(text)).toContain('eq-type-bodyl');
    const style = effectiveStyle(text);
    expect(style).not.toContain('text-align');
    expect(style).not.toContain('line-clamp');
    expect(style).not.toContain('text-overflow');
  });

  it('a bare Text lowers as a Text that names bodyL', () => {
    const a = lower(new Text('Body'));
    const b = lower(new Text('Body', 'bodyL', null, 0, 'start'));
    expect(classOf(a)).toBe(classOf(b));
    expect(effectiveStyle(a)).toBe(effectiveStyle(b));
  });
});

describe('an icon that names no size takes its box’s', () => {
  const glyph = () => IconGlyph.fromIcons('search');
  it('takes the box’s icon size', () => {
    const svg = svgOf(lower(boxOf(new ContentStyle({ iconSize: 16 }), new Icon(glyph()))));
    const style = effectiveStyle(svg);
    expect(style).toContain('width: 16px');
    expect(style).toContain('height: 16px');
  });

  it('is 24 where none is stated, and keeps its own size where it names one', () => {
    expect(effectiveStyle(svgOf(lower(new Icon(glyph()))))).toContain('width: 24px');
    expect(effectiveStyle(svgOf(lower(boxOf(new ContentStyle({ iconSize: 16 }), new Icon(glyph(), 32)))))).toContain('width: 32px');
  });

  it('refuses a size off the whitelist, as the C# record does', () => {
    expect(() => new ContentStyle({ iconSize: 18 })).toThrow(RangeError);
  });
});

describe('a string is a node', () => {
  it('VisualNode.fromString is the Text of it, naming nothing', () => {
    const node = VisualNode.fromString('Title') as Text;
    expect(node).toBeInstanceOf(Text);
    expect(node.content).toBe('Title');
    expect(node.role).toBeNull();
    expect(node.maxLines).toBeNull();
    expect(node.align).toBeNull();
  });

  it('inside a box that states a role it is drawn in that role', () => {
    const text = textNode(lower(boxOf(new ContentStyle({ role: 'label' }), VisualNode.fromString('Title'))));
    expect(classOf(text)).toContain('eq-type-label');
  });
});

const nextFrame = () =>
  new Promise<void>((resolve) => requestAnimationFrame(() => requestAnimationFrame(() => resolve())));

class Counter extends StatefulComponent {
  count = 0;
  static kept: Counter | null = null;
  constructor() {
    super();
    Counter.kept ??= this;
  }
  adoptConfig(): void {}
  bump(): void {
    this.setState(() => this.count++);
  }
  build(): VisualNodeValue {
    return new Text(`n: ${this.count}`) as unknown as VisualNodeValue;
  }
}

class Page extends StatefulComponent {
  build(): VisualNodeValue {
    const column = new Column(0);
    column.add(new Counter() as never);
    return boxOf(new ContentStyle({ role: 'label' }), column) as unknown as VisualNodeValue;
  }
}

describe('a component lowered again alone keeps its look', () => {
  beforeEach(() => {
    document.body.innerHTML = '';
    Counter.kept = null;
  });

  it('a stateful component inside a box stating a role still writes eq-type-label after its state changes', async () => {
    const container = document.createElement('div');
    document.body.appendChild(container);
    new Page().mount(container);
    expect(container.innerHTML).toContain('eq-type-label');
    const before = componentLowerings();
    Counter.kept!.bump();
    await nextFrame();
    expect(componentLowerings() - before, 'only the counter lowered, alone').toBe(1);
    expect(container.textContent).toContain('n: 1');
    expect(container.innerHTML).toContain('eq-type-label');
    expect(container.innerHTML).not.toContain('eq-type-bodyl');
  });
});
