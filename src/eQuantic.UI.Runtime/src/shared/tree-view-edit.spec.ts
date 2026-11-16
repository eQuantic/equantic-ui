/**
 * A TreeView edited in place in the browser (#1532, REAL eqc output), by the keyboard alone: F2 and the app's
 * request open a field over a node's label, Enter and leaving it commit, Escape cancels, and the keyboard goes back
 * to the tree; a new item's field under a node; a node's menu from the menu key on the tree's active item; a busy
 * node; a reload; a row the app builds. The C# twin is TreeEditTests (Photon, through the host).
 */

import { afterEach, describe, expect, it } from 'vitest';
import { StatefulComponent } from '../core/component';
import { resetShortcuts } from '../dom/shortcuts';
import { photonTheme } from './design-system.generated';
import { setPhotonTheme } from './photon-context';
import { Box, Column, Navigable, Pressable, Row, Text } from './vocabulary';
import { ListTheme } from './theme-slots';
import { MenuItem } from './components/MenuItem';
import { TreeEditRequest } from './components/TreeEditRequest';
import { TreeNode } from './components/TreeNode';
import { TreeReloadRequest } from './components/TreeReloadRequest';
import { TreeRowState } from './components/TreeRowState';
import { TreeView } from './components/TreeView';

setPhotonTheme(photonTheme);

const nextFrame = () =>
  new Promise<void>((resolve) => requestAnimationFrame(() => requestAnimationFrame(() => resolve())));

/** Frames until `seen` holds, failing past a bound: what the test waits for, not how long it takes. */
async function until(seen: () => boolean, frames = 30): Promise<void> {
  for (let frame = 0; frame < frames && !seen(); frame++) await nextFrame();
  expect(seen()).toBe(true);
}

const leaf = (key: string, label: string) => Object.assign(new TreeNode(key, label), { isLeaf: true });

const pressed: string[] = [];
const renamed: string[] = [];
const created: string[] = [];
const chosen: string[] = [];
let loads = 0;

/** The app's work: done unless a test holds it, so that test sees the node busy while it runs. */
let work: Promise<void> = Promise.resolve();
/** What a folder's load waits for: open unless a test holds it. */
let held: Promise<void> = Promise.resolve();
let release: () => void = () => undefined;
function holdLoads(): void {
  held = new Promise<void>((resolve) => (release = resolve));
}
let finish: () => void = () => undefined;
function holdWork(): void {
  work = new Promise<void>((resolve) => (finish = resolve));
}

let host: EditHost;

class EditHost extends StatefulComponent {
  edit: TreeEditRequest | null = null;
  reload: TreeReloadRequest | null = null;
  busy: string[] = [];
  rowBuilder: ((row: TreeRowState) => never) | null = null;
  style: ListTheme | null = null;

  constructor() {
    super();
    host = this;
  }

  change(fn: () => void): void {
    this.setState(fn);
  }

  build(): Column {
    const column = new Column(0);
    column.add(
      new TreeView(
        [
          Object.assign(new TreeNode('src', 'src'), {
            children: [leaf('src/App.cs', 'App.cs'), leaf('src/Ui.cs', 'Ui.cs')],
          }),
          new TreeNode('docs', 'docs'),
          leaf('README.md', 'README.md'),
        ],
        null,
        (node: TreeNode) => pressed.push(node.key),
        {
          label: 'Files',
          loadChildren: async (node: TreeNode) => {
            const call = ++loads;
            await held;
            return [leaf(`${node.key}/intro-${call}.md`, `intro-${call}.md`)];
          },
          onRenamed: async (node: TreeNode, name: string) => {
            renamed.push(`${node.key}>${name}`);
            await work;
          },
          onCreated: async (node: TreeNode, name: string) => {
            created.push(`${node.key}+${name}`);
            await work;
          },
          edit: this.edit,
          reload: this.reload,
          busyKeys: this.busy,
          menuBuilder: () => [new MenuItem('Rename'), new MenuItem('Delete')],
          onMenuSelect: (node: TreeNode, index: number) => chosen.push(`${node.key}#${index}`),
          rowBuilder: this.rowBuilder,
          style: this.style,
        },
      ) as never,
    );
    return column as never;
  }
}

function mount(configure?: (host: EditHost) => void): HTMLElement {
  const container = document.createElement('div');
  document.body.appendChild(container);
  const component = new EditHost();
  configure?.(component);
  component.mount(container);
  return container;
}

const tree = (container: HTMLElement) => container.querySelector('[role="tree"]') as HTMLElement;
const field = (container: HTMLElement) => tree(container).querySelector('input') as HTMLInputElement | null;

const key = (target: HTMLElement, name: string, init: KeyboardEventInit = {}) =>
  target.dispatchEvent(new KeyboardEvent('keydown', { key: name, bubbles: true, cancelable: true, ...init }));

const active = (container: HTMLElement) => {
  const id = tree(container).getAttribute('aria-activedescendant');
  return id ? (container.querySelector(`#${id}`) as HTMLElement).getAttribute('aria-label') : null;
};

function type(input: HTMLInputElement, text: string): void {
  input.value = text;
  input.dispatchEvent(new Event('input', { bubbles: true }));
}

/** The keyboard on App.cs, inside open src. */
async function onAppCs(container: HTMLElement): Promise<void> {
  tree(container).focus();
  key(tree(container), 'ArrowDown');
  await nextFrame();
  key(tree(container), 'ArrowRight');
  await nextFrame();
  key(tree(container), 'ArrowRight');
  await nextFrame();
  expect(active(container)).toBe('App.cs');
}

describe('a TreeView edited in place, in the browser', () => {
  afterEach(() => {
    resetShortcuts();
    document.body.innerHTML = '';
    pressed.length = 0;
    renamed.length = 0;
    created.length = 0;
    chosen.length = 0;
    loads = 0;
    work = Promise.resolve();
    held = Promise.resolve();
  });

  it('renames by F2: the label selected in a field, a new name and Enter, and the keyboard back on the tree', async () => {
    const container = mount();
    await onAppCs(container);

    key(tree(container), 'F2');
    await until(() => field(container) !== null);
    const input = field(container)!;
    expect(input.value).toBe('App.cs');
    expect(document.activeElement).toBe(input);
    expect([input.selectionStart, input.selectionEnd]).toEqual([0, 'App.cs'.length]);
    expect(input.getAttribute('aria-label')).toBe('Name');

    type(input, 'Main.cs');
    key(input, 'Enter');
    await until(() => field(container) === null);
    expect(renamed).toEqual(['src/App.cs>Main.cs']);
    expect(pressed).toEqual([]);
    await until(() => document.activeElement === tree(container));
    expect(active(container)).toBe('App.cs');
  });

  it('cancels on Escape, on an empty name and on the same name, and gives the keyboard back', async () => {
    const container = mount();
    await onAppCs(container);

    key(tree(container), 'F2');
    await until(() => field(container) !== null);
    type(field(container)!, 'Other.cs');
    key(field(container)!, 'Escape');
    await until(() => field(container) === null);
    await until(() => document.activeElement === tree(container));

    key(tree(container), 'F2');
    await until(() => field(container) !== null);
    type(field(container)!, '   ');
    key(field(container)!, 'Enter');
    await until(() => field(container) === null);

    key(tree(container), 'F2');
    await until(() => field(container) !== null);
    key(field(container)!, 'Enter');
    await until(() => field(container) === null);

    expect(renamed).toEqual([]);
  });

  it('leaves the keys typed into the field to the field', async () => {
    const container = mount();
    await onAppCs(container);
    key(tree(container), 'F2');
    await until(() => field(container) !== null);

    const input = field(container)!;
    expect(key(input, 'ArrowUp')).toBe(true);
    expect(key(input, 'a')).toBe(true);
    expect(key(input, 'Home')).toBe(true);
    await nextFrame();
    expect(active(container)).toBe('App.cs');
    expect(field(container)).not.toBeNull();
  });

  it('leaves a press inside the field to the field: no commit, and the keyboard stays', async () => {
    const container = mount();
    await onAppCs(container);
    key(tree(container), 'F2');
    await until(() => field(container) !== null);
    const input = field(container)!;
    const press = new MouseEvent('mousedown', { bubbles: true, cancelable: true });
    input.dispatchEvent(press);
    expect(press.defaultPrevented).toBe(false);
    await nextFrame();
    expect(field(container)).toBe(input);
    expect(document.activeElement).toBe(input);
    expect(renamed).toEqual([]);
  });

  it('keeps sending a grid the keys of a cell a press focused: only a field keeps its keys', async () => {
    const moves: string[] = [];
    class GridHost extends StatefulComponent {
      build(): Column {
        const row = new Row(0);
        row.add(new Pressable(new Box() as never, () => undefined, { role: 'gridCell' }) as never);
        row.add(new Pressable(new Box() as never, () => undefined, { role: 'gridCell' }) as never);
        const column = new Column(0);
        column.add(new Navigable((move) => moves.push(move), [row as never], { label: 'Days' }) as never);
        return column as never;
      }
    }
    const container = document.createElement('div');
    document.body.appendChild(container);
    new GridHost().mount(container);
    const cell = container.querySelector('[role="gridcell"]') as HTMLElement;
    cell.focus();
    key(cell, 'ArrowRight');
    expect(moves).toEqual(['nextItem']);
  });

  it('keeps a renamed node busy while the app works, and refuses it a press, an F2 and a menu', async () => {
    holdWork();
    const container = mount();
    await onAppCs(container);
    key(tree(container), 'F2');
    await until(() => field(container) !== null);
    type(field(container)!, 'Main.cs');
    key(field(container)!, 'Enter');
    await until(() => document.activeElement === tree(container));

    const item = () => container.querySelector('[aria-label="App.cs"]') as HTMLElement;
    await until(() => item().getAttribute('aria-disabled') === 'true');
    key(tree(container), 'Enter');
    key(tree(container), 'F2');
    key(tree(container), 'ContextMenu');
    await nextFrame();
    expect(pressed).toEqual([]);
    expect(field(container)).toBeNull();
    expect(container.querySelector('[role="menu"]')).toBeNull();

    finish();
    await until(() => item().getAttribute('aria-disabled') !== 'true');
  });

  it('creates under a node the app names: opened, a field first under it, and its children read again', async () => {
    const container = mount();
    tree(container).focus();
    host.change(() => (host.edit = Object.assign(new TreeEditRequest(1, 'docs'), { creates: true })));
    await until(() => field(container) !== null);
    expect(loads).toBe(1);
    const docs = container.querySelector('[aria-label="docs"]') as HTMLElement;
    expect(docs.getAttribute('aria-expanded')).toBe('true');
    const row = field(container)!.closest('[role="treeitem"]') as HTMLElement;
    expect(row.getAttribute('aria-level')).toBe('2');
    expect(row.getAttribute('aria-posinset')).toBe('1');
    await until(() => container.querySelector('[aria-label="intro-1.md"]') !== null);
    expect((container.querySelector('[aria-label="intro-1.md"]') as HTMLElement).getAttribute('aria-posinset')).toBe('2');

    type(field(container)!, 'notes.md');
    key(field(container)!, 'Enter');
    await until(() => container.querySelector('[aria-label="intro-2.md"]') !== null);
    expect(created).toEqual(['docs+notes.md']);
    expect(loads).toBe(2);
    expect(document.activeElement).toBe(tree(container));
  });

  it('opens a node menu from the menu key on the tree active item, and reports the choice', async () => {
    const container = mount();
    tree(container).focus();
    key(tree(container), 'ArrowDown');
    await nextFrame();
    expect(active(container)).toBe('src');

    key(tree(container), 'ContextMenu');
    await until(() => container.querySelector('[role="menu"]') !== null);
    const items = [...container.querySelectorAll('[role="menuitem"]')].map((item) => item.textContent);
    expect(items).toEqual(['Rename', 'Delete']);

    key(tree(container), 'ArrowDown');
    await nextFrame();
    key(tree(container), 'Enter');
    await until(() => container.querySelector('[role="menu"]') === null);
    expect(chosen).toEqual(['src#1']);
    expect(pressed).toEqual([]);
  });

  it('reads a loaded node children again on a reload, each version once', async () => {
    const container = mount();
    tree(container).focus();
    key(tree(container), 'ArrowDown');
    await nextFrame();
    key(tree(container), 'ArrowDown');
    await nextFrame();
    key(tree(container), 'ArrowRight');
    await until(() => container.querySelector('[aria-label="intro-1.md"]') !== null);

    host.change(() => (host.reload = new TreeReloadRequest(1, ['docs', 'src', 'README.md'])));
    await until(() => container.querySelector('[aria-label="intro-2.md"]') !== null);
    expect(loads).toBe(2);
    host.change(() => (host.busy = []));
    await nextFrame();
    await nextFrame();
    expect(loads).toBe(2);
  });

  it('ignores a rename the app asks for a node the person cannot see, and keeps the keyboard where it was', async () => {
    const container = mount();
    tree(container).focus();
    key(tree(container), 'ArrowDown');
    await nextFrame();
    key(tree(container), 'ArrowDown');
    await nextFrame();
    expect(active(container)).toBe('docs');
    host.change(() => (host.edit = new TreeEditRequest(1, 'src/App.cs')));
    await nextFrame();
    await nextFrame();
    expect(field(container)).toBeNull();
    expect(active(container)).toBe('docs');
  });

  it('reads a folder again after a load that was running when the reload was asked', async () => {
    const container = mount();
    tree(container).focus();
    key(tree(container), 'ArrowDown');
    await nextFrame();
    key(tree(container), 'ArrowDown');
    await nextFrame();
    key(tree(container), 'ArrowRight');
    await until(() => container.querySelector('[aria-label="intro-1.md"]') !== null);

    holdLoads();
    host.change(() => (host.reload = new TreeReloadRequest(1, ['docs'])));
    await until(() => loads === 2);
    host.change(() => (host.reload = new TreeReloadRequest(2, ['docs'])));
    await nextFrame();
    expect(loads).toBe(2);
    release();
    await until(() => container.querySelector('[aria-label="intro-3.md"]') !== null);
    expect(loads).toBe(3);
  });

  it('draws the app own row after the chevron, and keeps the tree item around it', async () => {
    const container = mount((component) => {
      component.rowBuilder = ((row: TreeRowState) => {
        const content = new Row(4);
        content.add(new Text(row.node.label) as never);
        content.add(new Text(`L${row.level}`) as never);
        return content as never;
      }) as never;
      component.style = new ListTheme({ treeIndent: 24 });
    });
    const items = [...container.querySelectorAll('[role="treeitem"]')] as HTMLElement[];
    expect(items.map((item) => item.getAttribute('aria-label'))).toEqual(['src', 'docs', 'README.md']);
    expect(items[0].textContent).toContain('L1');
    expect(items[0].getAttribute('aria-expanded')).toBe('false');
  });
});
