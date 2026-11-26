import { describe, expect, it } from 'vitest';
import { lowerVisualNode } from './lowering';
import type { LoweringContext } from './lowering';
import { photonTheme } from './design-system.generated';
import type { HtmlNode } from '../core/types';
import { Anchored, Column, Overlay, Pressable, Row, Text, TitlePart } from './vocabulary';
import type { VisualNodeValue } from './nodes';

const ctx: LoweringContext = { textPrimary: photonTheme.textPrimary };

const text = (content: string) => new Text(content, 'bodyM', photonTheme.textPrimary);
const column = (...children: unknown[]) => {
  const node = new Column(8);
  for (const child of children) node.add(child as never);
  return node;
};
const line = (...children: unknown[]) => {
  const node = new Row(8);
  for (const child of children) node.add(child as never);
  return node;
};
const lower = (node: unknown) => lowerVisualNode(node as VisualNodeValue, ctx);

/** Every element of a lowered tree, outermost first. */
function* walk(node: HtmlNode): Generator<HtmlNode> {
  yield node;
  for (const child of node.children) yield* walk(child);
}

/** The text a node holds, as a reader would read a part named by `aria-labelledby`. */
function said(node: HtmlNode): string {
  if (node.tag === '#text') return node.textContent ?? '';
  return node.children.map(said).join(node.tag === 'div' ? ' ' : '');
}

/** The first element of `tree` with `role`. */
function withRole(tree: HtmlNode, role: string): HtmlNode {
  const found = [...walk(tree)].find((node) => node.attributes['role'] === role);
  expect(found, `an element with role="${role}"`).toBeDefined();
  return found!;
}

/** The words a dialog's `aria-labelledby` points at in `tree`, or null when it has none. */
function titleOf(dialog: HtmlNode, tree: HtmlNode): string | null {
  const ids = dialog.attributes['aria-labelledby'];
  if (ids === undefined) return null;
  const byId = new Map<string, HtmlNode>();
  for (const node of walk(tree)) if (node.attributes['id']) byId.set(node.attributes['id'], node);
  return ids
    .split(' ')
    .filter((id) => id)
    .map((id) => said(byId.get(id)!))
    .join(' ')
    .replace(/\s+/g, ' ')
    .trim();
}

/**
 * The browser's half of the part that names a dialog (#1916; C# twin: LowerTitlePart, held to the same markup by the
 * component-parity fixture). What the fixture cannot say is drawn here: a dialog panel, the nearest layer, two parts,
 * and a part lowered again in the same layer.
 */
describe('the part that names a dialog, on the web', () => {
  it('names a modal layer by the words of a composite part', () => {
    const tree = lower(new Overlay(column(new TitlePart(line(text('Rename'), text('file'))), text('The name is shared.'))));
    const dialog = withRole(tree, 'dialog');
    expect(titleOf(dialog, tree)).toBe('Rename file');
  });

  it('lists two parts in reading order', () => {
    const tree = lower(new Overlay(column(new TitlePart(text('Step 2')), new TitlePart(text('Payment')))));
    const dialog = withRole(tree, 'dialog');
    expect(dialog.attributes['aria-labelledby']!.split(' ')).toHaveLength(2);
    expect(titleOf(dialog, tree)).toBe('Step 2 Payment');
  });

  it('keeps the label beside the parts, for the browser to fall back on', () => {
    const tree = lower(new Overlay(new TitlePart(text('Rename file')), { label: 'Settings' }));
    const dialog = withRole(tree, 'dialog');
    expect(dialog.attributes['aria-label']).toBe('Settings');
    expect(titleOf(dialog, tree)).toBe('Rename file');
  });

  it('names only the nearest layer, so a part inside a toast names neither', () => {
    const tree = lower(
      new Overlay(new Overlay(new TitlePart(text('Saved')), { modal: false }), { label: 'Editor' }),
    );
    const dialog = withRole(tree, 'dialog');
    expect(dialog.attributes['aria-labelledby']).toBeUndefined();
    expect(dialog.attributes['aria-label']).toBe('Editor');
  });

  it('names a closed layer nothing, as it is no dialog right now', () => {
    const tree = lower(new Overlay(new TitlePart(text('Rename file')), { open: false }));
    expect(tree.attributes['role']).toBeUndefined();
    expect(tree.attributes['aria-labelledby']).toBeUndefined();
  });

  it('names a dialog panel by its part, and a menu panel by none', () => {
    const panel = () => column(new TitlePart(text('Pick a day')), text('July'));
    const trigger = () => new Pressable(text('Pick'), () => {});
    const dialogTree = lower(new Anchored(trigger(), panel(), { open: true, panelRole: 'dialog' }));
    const dialog = withRole(dialogTree, 'dialog');
    expect(titleOf(dialog, dialogTree)).toBe('Pick a day');

    const menuTree = lower(new Anchored(trigger(), panel(), { open: true, panelRole: 'menu' }));
    expect(withRole(menuTree, 'menu').attributes['aria-labelledby']).toBeUndefined();
  });

  it('gives a part outside every layer no id', () => {
    const tree = lower(new TitlePart(text('Orders')));
    expect([...walk(tree)].some((node) => node.attributes['id'] !== undefined)).toBe(false);
  });

  it('records a part lowered again in the same layer once', () => {
    const titles: string[] = [];
    const part = new TitlePart(text('Rename file'));
    lowerVisualNode(part as unknown as VisualNodeValue, { ...ctx, layerTitles: titles });
    lowerVisualNode(part as unknown as VisualNodeValue, { ...ctx, layerTitles: titles });
    expect(titles).toHaveLength(1);
  });
});
