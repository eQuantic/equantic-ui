import { describe, expect, it } from 'vitest';
import { lowerVisualNode } from './lowering';
import type { LoweringContext } from './lowering';
import { photonTheme } from './design-system.generated';
import type { HtmlNode } from '../core/types';
import { MergeSemantics, Pressable, Progress, Row, Text } from './vocabulary';
import type { VisualNodeValue } from './nodes';

const ctx: LoweringContext = { textPrimary: photonTheme.textPrimary };

const text = (content: string) => new Text(content, 'bodyM', photonTheme.textPrimary);
const line = (...children: unknown[]) => {
  const row = new Row(8);
  for (const child of children) row.add(child as never);
  return row;
};
const lower = (node: unknown) => lowerVisualNode(node as VisualNodeValue, ctx);

/** Every element of a lowered tree, each with the elements around it, outermost first. */
function* walk(node: HtmlNode, above: HtmlNode[] = []): Generator<{ node: HtmlNode; above: HtmlNode[] }> {
  yield { node, above };
  for (const child of node.children) yield* walk(child, [...above, node]);
}

/** The text a node holds, as a reader would read a part named by `aria-labelledby`. */
function said(node: HtmlNode): string {
  if (node.tag === '#text') return node.textContent ?? '';
  if (node.attributes['aria-label']) return ` ${node.attributes['aria-label']} `;
  return node.children.map(said).join(node.tag === 'div' ? ' ' : '');
}

/** The name a stop's `aria-labelledby` gives it in `tree`. */
function nameOf(stop: HtmlNode, tree: HtmlNode): string {
  const byId = new Map<string, HtmlNode>();
  for (const { node } of walk(tree)) if (node.attributes['id']) byId.set(node.attributes['id'], node);
  return (stop.attributes['aria-labelledby'] ?? '')
    .split(' ')
    .filter((id) => id)
    .map((id) => said(byId.get(id)!))
    .join(' ')
    .replace(/\s+/g, ' ')
    .trim();
}

/**
 * The browser's half of a merged subtree (C# twin: MergedReading, held to the same markup by the
 * component-parity fixture). What the fixture cannot say is drawn here: the shapes a hidden part must
 * never take, and a merge inside a merge.
 */
describe('a merged subtree on the web', () => {
  it('never hides a control inside an element that would otherwise be one part', () => {
    // A progress bar's host has a role of its own, so it would be read as one part, and a button inside it
    // would go hidden with it while it still took a Tab.
    const tree = lower(new MergeSemantics(line(text('Upload'), new Progress(line(text('42%'), new Pressable(text('Cancel')))))));

    const button = [...walk(tree)].find(({ node }) => node.tag === 'button');
    expect(button).toBeDefined();
    expect(button!.above.some((node) => node.attributes['aria-hidden'] === 'true')).toBe(false);
    expect(button!.node.attributes['aria-hidden']).toBeUndefined();
  });

  it('takes an inner merge group as its own: one stop, named by both', () => {
    const tree = lower(new MergeSemantics(line(new MergeSemantics(line(text('Wi-Fi'), text('On'))), text('Home'))));

    const groups = [...walk(tree)].filter(({ node }) => node.attributes['role'] === 'group');
    expect(groups).toHaveLength(1);
    expect(nameOf(groups[0].node, tree)).toBe('Wi-Fi On Home');
  });

  it('keeps what an inner merge named its control by', () => {
    const tree = lower(
      new MergeSemantics(line(text('Home'), new MergeSemantics(line(text('Wi-Fi'), new Pressable(text('Join')))))),
    );

    const button = [...walk(tree)].find(({ node }) => node.tag === 'button')!.node;
    expect(nameOf(button, tree)).toBe('Home Wi-Fi Join');
    expect([...walk(tree)].some(({ node }) => node.attributes['role'] === 'group')).toBe(false);
  });
});
