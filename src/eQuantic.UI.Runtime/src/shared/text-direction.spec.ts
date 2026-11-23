/**
 * Text that runs right to left in the browser (#1568), the twin of the C# `TextDirectionTests`: a
 * component reads its direction from the nearest `Directionality` above it, or from its UI culture as the
 * server said it; the lowering writes every start and end as logical CSS under a `dir` host; and a
 * culture switch writes the document's `dir` from the culture's data, with no list of languages here.
 */

import { afterEach, beforeEach, describe, expect, it } from 'vitest';
import { StatefulComponent } from '../core/component';
import type { HtmlNode } from '../core/types';
import { BuildOwner } from './components/BuildOwner';
import { TableView } from './components/TableView';
import { photonTheme } from './design-system.generated';
import type { TextDirectionValue } from './enums.generated';
import { lowerVisualNode } from './lowering';
import type { VisualNodeValue } from './nodes';
import { ambientLoweringContext, ComponentContext } from './photon-context';
import { effectiveStyle } from './style-atomizer';
import { EdgeInsets } from './value-types';
import { UniformTableSpans } from './components/UniformTableSpans';
import { Box, BoxStyle, Column, Directionality, Positioned, Stack, Text } from './vocabulary';
import type { VisualNode } from './vocabulary';
import { activeTextDirection, installCulture, setCulture, setCultureFormatLoader, INVARIANT_FORMAT } from '../utils/culture';

const lower = (node: unknown): HtmlNode => lowerVisualNode(node as VisualNodeValue, ambientLoweringContext()) as HtmlNode;

/** Every declaration of an element, its atomic classes resolved, as `property: value` strings. */
const declarations = (node: HtmlNode): string[] =>
  effectiveStyle({ attributes: node.attributes })
    .split(';')
    .map((part) => part.trim().replace(/\s*:\s*/, ': '))
    .filter((part) => part.length > 0);

let log: string[] = [];

/** Says which way its text runs. */
class Reader extends StatefulComponent {
  constructor(private readonly label: string) {
    super();
  }

  build(context: unknown): VisualNodeValue {
    log.push(`${this.label} ${(context as ComponentContext).textDirection}`);
    return new Text(this.label, 'label') as unknown as VisualNodeValue;
  }
}

describe('the direction a component reads', () => {
  beforeEach(() => {
    log = [];
    installCulture('', '', {});
  });
  afterEach(() => installCulture('', '', {}));

  it("is the UI culture's with nothing above it, as the server said it", () => {
    installCulture('ar', 'ar', {}, INVARIANT_FORMAT, undefined, true);
    expect(new ComponentContext(photonTheme).textDirection).toBe('rightToLeft');
    expect(activeTextDirection()).toBe('rightToLeft');

    installCulture('en', 'en', {}, INVARIANT_FORMAT, undefined, false);
    expect(new ComponentContext(photonTheme).textDirection).toBe('leftToRight');
  });

  it('is the nearest Directionality above its position, and a change builds what read it', () => {
    installCulture('ar', 'ar', {}, INVARIANT_FORMAT, undefined, true);
    const owner = new BuildOwner((component, context) => component.build(context) as unknown as VisualNode);
    const frame = () => new ComponentContext(photonTheme);
    const tree = (direction: TextDirectionValue) => {
      const column = new Column(0);
      column.add(new Directionality(direction, new Reader('code') as never) as never);
      column.add(new Reader('page') as never);
      return column as unknown as VisualNode;
    };

    owner.build(tree('leftToRight'), frame() as never, 400, true);
    owner.endFrame();
    expect(log).toEqual(['code leftToRight', 'page rightToLeft']);

    log = [];
    owner.build(tree('rightToLeft'), frame() as never, 400, false);
    owner.endFrame();
    expect(log, 'the reader under the Directionality that changed, and not the page').toEqual(['code rightToLeft']);
  });
});

describe('the web under a direction', () => {
  it('lowers a Directionality to a host that draws no box and says dir (C# twin)', () => {
    const rtl = lower(new Directionality('rightToLeft', new Text('x', 'bodyM')));
    expect(rtl.tag).toBe('div');
    expect(rtl.attributes['dir']).toBe('rtl');
    expect(declarations(rtl)).toEqual(['display: contents']);
    expect(lower(new Directionality('leftToRight', new Text('x', 'bodyM'))).attributes['dir']).toBe('ltr');
  });

  it('writes a padding larger at its start as logical padding, and a symmetric one as before', () => {
    const asymmetric = declarations(lower(new Box(new BoxStyle({ padding: new EdgeInsets(24, 8, 8, 4) }))));
    expect(asymmetric).toContain('padding-block: 8px 4px');
    expect(asymmetric).toContain('padding-inline: 24px 8px');
    expect(asymmetric.some((d) => d.startsWith('padding: '))).toBe(false);

    const symmetric = declarations(lower(new Box(new BoxStyle({ padding: new EdgeInsets(16, 8, 16, 4) }))));
    expect(symmetric).toContain('padding: 8px 16px 4px 16px');
  });

  it('writes a border at one inline side as logical widths', () => {
    const start = declarations(
      lower(new Box(new BoxStyle({ borderWidth: 2, borderColor: photonTheme.border, borderSides: 8 }))),
    );
    expect(start).toContain('border-block-width: 0');
    expect(start).toContain('border-inline-width: 2px 0');
    expect(start.some((d) => d.startsWith('border-width'))).toBe(false);
  });

  it('measures a positioned start and end from the inline sides, and shifts toward the end', () => {
    const stack = new Stack()
      .with(new Positioned(new Text('x', 'bodyM'), 4, 8, null, 16))
      .with(new Positioned(new Text('c', 'bodyM'), null, null, null, null, { startFraction: 0.5, shiftX: -0.5 }));
    const lowered = lower(stack);

    const anchor = declarations(lowered.children[0] as HtmlNode);
    expect(anchor).toContain('inset-inline-start: 16px');
    expect(anchor).toContain('inset-inline-end: 8px');
    expect(anchor.some((d) => d.startsWith('left') || d.startsWith('right'))).toBe(false);

    const centred = declarations(lowered.children[1] as HtmlNode);
    expect(centred).toContain('inset-inline-start: 50%');
    // effectiveStyle resolves a variable to its fallback, so `var(--eq-inline, 1)` reads as its 1 here.
    expect(centred).toContain('transform: translate(calc(1 * -50%), 0)');
  });

  it('lays a table viewport out left to right under a right-to-left subtree', () => {
    const table = new TableView(new UniformTableSpans(2, 28), new UniformTableSpans(2, 96), (row: number, column: number) =>
      new Text(`${row}:${column}`, 'caption'),
    );
    const find = (node: HtmlNode): HtmlNode | null =>
      node.attributes?.['aria-rowcount'] !== undefined
        ? node
        : (node.children ?? []).map((child) => find(child as HtmlNode)).find((found) => found !== null) ?? null;

    const host = find(lower(new Directionality('rightToLeft', table as never)));

    expect(host?.attributes['dir']).toBe('ltr');
  });
});

describe('a culture switch', () => {
  beforeEach(() => installCulture('en', 'en', {}, INVARIANT_FORMAT, undefined, false));
  afterEach(() => {
    setCultureFormatLoader();
    installCulture('', '', {});
  });

  it("writes the document's dir from the culture's data, and back", async () => {
    expect(document.documentElement.dir).toBe('ltr');
    // The server answers for a culture as `/_equantic/culture/{name}.json` does: .NET says the direction.
    setCultureFormatLoader(async (name) => ({ format: INVARIANT_FORMAT, rightToLeft: name.startsWith('ar') }));

    await setCulture('ar');
    expect(document.documentElement.dir).toBe('rtl');
    expect(activeTextDirection()).toBe('rightToLeft');

    await setCulture('en');
    expect(document.documentElement.dir).toBe('ltr');
  });

  it('reads the UI culture\'s direction when the format culture is another', async () => {
    const asked: string[] = [];
    setCultureFormatLoader(async (name) => {
      asked.push(name);
      return { format: INVARIANT_FORMAT, rightToLeft: name === 'he' };
    });

    await setCulture('he', 'en-US');

    expect(document.documentElement.dir).toBe('rtl');
    expect(asked).toContain('he');
  });
});
