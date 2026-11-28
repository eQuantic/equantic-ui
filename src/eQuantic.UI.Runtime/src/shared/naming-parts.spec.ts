import { describe, expect, it } from 'vitest';
import { lowerVisualNode } from './lowering';
import type { LoweringContext } from './lowering';
import { photonTheme } from './design-system.generated';
import type { HtmlNode } from '../core/types';
import { beginParts, inPartsWalk, partLowered } from './naming-parts';
import {
  Anchored,
  Column,
  DescriptionPart,
  Group,
  LabelPart,
  Overlay,
  Pressable,
  Row,
  Text,
  TextEntry,
} from './vocabulary';
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

/** The words the ids `attribute` of `target` lists point at in `tree`, or null when it lists none. */
function referenced(target: HtmlNode, tree: HtmlNode, attribute: string): string | null {
  const ids = target.attributes[attribute];
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

/** The `<input>` of a lowered tree. */
function input(tree: HtmlNode): HtmlNode {
  const found = [...walk(tree)].find((node) => node.tag === 'input');
  expect(found, 'an input').toBeDefined();
  return found!;
}

/**
 * The browser's half of the parts that name and describe their nearest nameable (#1916; C# twin: NamingParts, held to
 * the same markup by the component-parity fixture). What the fixture cannot say is drawn here: a dialog panel, the
 * nearest layer, two parts, a field and its own label, the refusal, and a part lowered again alone.
 */
describe('the part that names a dialog, on the web', () => {
  it('names a modal layer by the words of a composite part', () => {
    const tree = lower(new Overlay(column(new LabelPart(line(text('Rename'), text('file'))), text('The name is shared.'))));
    const dialog = withRole(tree, 'dialog');
    expect(titleOf(dialog, tree)).toBe('Rename file');
  });

  it('lists two parts in reading order', () => {
    const tree = lower(new Overlay(column(new LabelPart(text('Step 2')), new LabelPart(text('Payment')))));
    const dialog = withRole(tree, 'dialog');
    expect(dialog.attributes['aria-labelledby']!.split(' ')).toHaveLength(2);
    expect(titleOf(dialog, tree)).toBe('Step 2 Payment');
  });

  it('keeps the label beside the parts, for the browser to fall back on', () => {
    const tree = lower(new Overlay(new LabelPart(text('Rename file')), { label: 'Settings' }));
    const dialog = withRole(tree, 'dialog');
    expect(dialog.attributes['aria-label']).toBe('Settings');
    expect(titleOf(dialog, tree)).toBe('Rename file');
  });

  it('names only the nearest layer, so a part inside a toast names neither', () => {
    const tree = lower(
      new Overlay(new Overlay(new LabelPart(text('Saved')), { modal: false }), { label: 'Editor' }),
    );
    const dialog = withRole(tree, 'dialog');
    expect(dialog.attributes['aria-labelledby']).toBeUndefined();
    expect(dialog.attributes['aria-label']).toBe('Editor');
  });

  it('names a closed layer nothing, as it is no dialog right now', () => {
    const tree = lower(new Overlay(new LabelPart(text('Rename file')), { open: false }));
    expect(tree.attributes['role']).toBeUndefined();
    expect(tree.attributes['aria-labelledby']).toBeUndefined();
  });

  it('names a dialog panel by its part, and a menu panel by none', () => {
    const panel = () => column(new LabelPart(text('Pick a day')), text('July'));
    const trigger = () => new Pressable(text('Pick'), () => {});
    const dialogTree = lower(new Anchored(trigger(), panel(), { open: true, panelRole: 'dialog' }));
    const dialog = withRole(dialogTree, 'dialog');
    expect(titleOf(dialog, dialogTree)).toBe('Pick a day');

    const menuTree = lower(new Anchored(trigger(), panel(), { open: true, panelRole: 'menu' }));
    expect(withRole(menuTree, 'menu').attributes['aria-labelledby']).toBeUndefined();
  });

  it('gives a part outside every layer no id', () => {
    const tree = lower(new LabelPart(text('Orders')));
    expect([...walk(tree)].some((node) => node.attributes['id'] !== undefined)).toBe(false);
  });

});

describe('the parts that name and describe a field, a group and a check, on the web', () => {
  it('names a field by the label beside it, and describes it after its own description', () => {
    const tree = lower(
      column(
        new LabelPart(text('Email')),
        new TextEntry('', null, { description: 'Required' }),
        new DescriptionPart(text('We never share it.')),
      ),
    );
    const entry = input(tree);
    expect(referenced(entry, tree, 'aria-labelledby')).toBe('Email');
    expect(referenced(entry, tree, 'aria-describedby')).toBe('Required We never share it.');
  });

  it('leaves a field that names itself to its label, so a dialog title names the dialog', () => {
    const tree = lower(
      new Overlay(column(new LabelPart(text('Rename file')), new TextEntry('notes.txt', null, { label: 'Name' }))),
    );
    expect(input(tree).attributes['aria-labelledby']).toBeUndefined();
    expect(referenced(withRole(tree, 'dialog'), tree, 'aria-labelledby')).toBe('Rename file');
  });

  it('names a group by its part', () => {
    const tree = lower(new Group(column(new LabelPart(text('Shipping')), text('Two days'))));
    expect(referenced(withRole(tree, 'group'), tree, 'aria-labelledby')).toBe('Shipping');
  });

  it('names a checkbox by the part beside it', () => {
    const tree = lower(
      line(new Pressable(text(''), () => {}, { role: 'checkbox', selected: true }), new LabelPart(text('I accept'))),
    );
    expect(referenced(withRole(tree, 'checkbox'), tree, 'aria-labelledby')).toBe('I accept');
  });

  it('refuses a part beside two fields it could name, in the words every producer uses', () => {
    expect(() =>
      lower(column(new LabelPart(text('Email')), new TextEntry('', null), new TextEntry('', null))),
    ).toThrow(/A LabelPart reaches a column holding 2 controls it could name/);
  });
});

describe('a part lowered again alone', () => {
  const host = (): HtmlNode => ({ tag: 'div', attributes: {}, events: {}, children: [] });

  it('keeps the id its nameable, lowered before, already lists', () => {
    const part = inPartsWalk(() => {
      const lowered = host();
      partLowered(beginParts(), false, lowered, 'eq-label-r-0');
      return lowered;
    }, true);
    expect(part.attributes['id']).toBe('eq-label-r-0');
  });

  it('takes no id in a whole walk, where a part that reaches nothing names nothing', () => {
    const part = inPartsWalk(() => {
      const lowered = host();
      partLowered(beginParts(), false, lowered, 'eq-label-r-0');
      return lowered;
    });
    expect(part.attributes['id']).toBeUndefined();
  });
});
