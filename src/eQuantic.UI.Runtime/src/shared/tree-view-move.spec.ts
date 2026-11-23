/**
 * A TreeView whose nodes move (#1533, REAL eqc output), by a mouse and by the keyboard: before, after or inside
 * another, never inside itself, never where the app refuses, a closed folder opened under a pause, the moved node
 * busy while the app works and both parents read again after. jsdom lays nothing out, so each row is given the box
 * the tree would draw (32 tall, from y 0) and `elementsFromPoint` answers from those boxes. The C# twin is
 * TreeMoveTests (Photon, through the host).
 */

import { afterEach, beforeEach, describe, expect, it } from 'vitest';
import { StatefulComponent } from '../core/component';
import { resetShortcuts } from '../dom/shortcuts';
import { resetSortableController } from '../dom/sortable';
import { photonTheme } from './design-system.generated';
import { setPhotonTheme } from './photon-context';
import { Column } from './vocabulary';
import { TreeMove } from './components/TreeMove';
import { TreeNode } from './components/TreeNode';
import { TreeView } from './components/TreeView';

setPhotonTheme(photonTheme);

const nextFrame = () =>
  new Promise<void>((resolve) => requestAnimationFrame(() => requestAnimationFrame(() => resolve())));

/** Frames until `seen` holds, failing past a bound: what the test waits for, not how long it takes. */
async function until(seen: () => boolean, frames = 30): Promise<void> {
  for (let frame = 0; frame < frames && !seen(); frame++) await nextFrame();
  expect(seen()).toBe(true);
}

const wait = (ms: number) => new Promise<void>((resolve) => setTimeout(resolve, ms));

const leaf = (key: string, label: string) => Object.assign(new TreeNode(key, label), { isLeaf: true });

/** What each folder holds, read through the loader as the server would answer. */
const listing: Record<string, string[]> = {
  src: ['App.cs', 'Program.cs', 'Ui.cs'],
  docs: ['intro.md'],
  locked: ['secret.md'],
};

const moved: string[] = [];
const loads: string[] = [];
const pressed: string[] = [];
let work: Promise<void> = Promise.resolve();
let finish: () => void = () => undefined;
function holdWork(): void {
  work = new Promise<void>((resolve) => (finish = resolve));
}

const say = (move: TreeMove) => `${move.node.key}>${move.parent?.key ?? ''}|${move.after?.key ?? ''}`;

class MoveHost extends StatefulComponent {
  constructor(private readonly expanded: string[]) {
    super();
  }

  build(): Column {
    const column = new Column(0);
    column.add(
      new TreeView(
        [new TreeNode('src', 'src'), new TreeNode('docs', 'docs'), new TreeNode('locked', 'locked'), leaf('README.md', 'README.md')],
        null,
        (node: TreeNode) => pressed.push(node.key),
        null,
        {
          label: 'Files',
          expandedKeys: this.expanded,
          loadChildren: async (node: TreeNode) => {
            loads.push(node.key);
            return (listing[node.key] ?? []).map((name) => leaf(`${node.key}/${name}`, name));
          },
          onMoved: async (move: TreeMove) => {
            moved.push(say(move));
            await work;
          },
          canMove: (move: TreeMove) => move.parent?.key !== 'locked',
        },
      ) as never,
    );
    return column as never;
  }
}

type Box = { left: number; top: number; width: number; height: number };
const boxes = new Map<Element, Box>();

function give(element: Element, box: Box): void {
  boxes.set(element, box);
  (element as HTMLElement).getBoundingClientRect = () =>
    ({ ...box, right: box.left + box.width, bottom: box.top + box.height, x: box.left, y: box.top, toJSON: () => box }) as DOMRect;
}

/** Each row's box as the tree draws it, in the order it draws them, and the tree's around them. */
function layout(container: HTMLElement): void {
  boxes.clear();
  const tree = container.querySelector('[data-eq-sort]')!;
  give(tree, { left: 0, top: 0, width: 300, height: 400 });
  Array.from(tree.querySelectorAll('[data-eq-sort-item]')).forEach((row, at) =>
    give(row, { left: 0, top: at * 32, width: 300, height: 32 }));
}

async function mount(expanded: string[] = ['src']): Promise<HTMLElement> {
  const container = document.createElement('div');
  document.body.appendChild(container);
  new MoveHost(expanded).mount(container);
  await until(() => expanded.every((key) => loads.includes(key)) && rows(container).length >= 4 + expanded.length);
  await nextFrame();
  layout(container);
  return container;
}

const tree = (container: HTMLElement) => container.querySelector('[role="tree"]') as HTMLElement;
const rows = (container: HTMLElement) => Array.from(container.querySelectorAll('[role="treeitem"]')) as HTMLElement[];
const row = (container: HTMLElement, label: string) => rows(container).find((at) => at.getAttribute('aria-label') === label)!;
/** A row's own elements: one more of them while the line is drawn over it. */
const parts = (element: HTMLElement) => element.querySelectorAll('*').length;

function pointer(target: EventTarget, type: string, x: number, y: number): void {
  target.dispatchEvent(new PointerEvent(type, { clientX: x, clientY: y, bubbles: true, cancelable: true, pointerType: 'mouse', button: 0 }));
}

/** A mouse presses a row and carries it to `y`; released there unless `release` is false. */
function carry(container: HTMLElement, label: string, y: number, release = true): void {
  const from = boxes.get(row(container, label))!;
  pointer(row(container, label), 'pointerdown', 10, from.top + 16);
  pointer(document, 'pointermove', 10, from.top + 16 + (y > from.top ? 8 : -8));
  pointer(document, 'pointermove', 10, y);
  if (release) pointer(document, 'pointerup', 10, y);
}

/** A mouse presses a row at x 30 and carries it to `y`, `across` pixels toward the end of the rows (#1594). */
function carryAcross(container: HTMLElement, label: string, y: number, across: number, release = true): void {
  const from = boxes.get(row(container, label))!;
  pointer(row(container, label), 'pointerdown', 30, from.top + 16);
  pointer(document, 'pointermove', 30, from.top + 16 + (y > from.top ? 8 : -8));
  pointer(document, 'pointermove', 30 + across, y);
  if (release) pointer(document, 'pointerup', 30 + across, y);
}

const key = (target: HTMLElement, name: string) =>
  target.dispatchEvent(new KeyboardEvent('keydown', { key: name, bubbles: true, cancelable: true }));

const active = (container: HTMLElement) => {
  const id = tree(container).getAttribute('aria-activedescendant');
  return id ? (container.querySelector(`#${id}`) as HTMLElement).getAttribute('aria-label') : null;
};

describe('a TreeView whose nodes move, in the browser', () => {
  beforeEach(() => {
    (document as unknown as { elementsFromPoint: (x: number, y: number) => Element[] }).elementsFromPoint = (x, y) =>
      Array.from(boxes.entries())
        .filter(([, b]) => x >= b.left && x < b.left + b.width && y >= b.top && y < b.top + b.height)
        .map(([element]) => element)
        .sort((p, q) => (p.contains(q) ? 1 : q.contains(p) ? -1 : 0));
  });

  afterEach(async () => {
    // A drag's swallowed click is waited for for 50 ms: the next case's first click is not its.
    await wait(60);
    resetSortableController();
    resetShortcuts();
    document.body.innerHTML = '';
    boxes.clear();
    moved.length = 0;
    loads.length = 0;
    pressed.length = 0;
    work = Promise.resolve();
  });

  // Rows with src open: src 0, App.cs 32, Program.cs 64, Ui.cs 96, docs 128, locked 160, README.md 192.

  it('moves a file inside a closed folder over its middle half, as its first child', async () => {
    const container = await mount();
    expect(row(container, 'README.md').hasAttribute('data-eq-sort-item')).toBe(true);
    // The rows stay out of the Tab order: the tree is the one stop.
    expect(row(container, 'README.md').getAttribute('tabindex')).toBe('-1');
    carry(container, 'README.md', 144);
    expect(moved).toEqual(['README.md>docs|']);
  });

  it('moves a file between two others, after the one above', async () => {
    const container = await mount();
    carry(container, 'Ui.cs', 68);
    expect(moved).toEqual(['src/Ui.cs>src|src/App.cs']);
  });

  it('draws a line over the row it would land before, and nothing once it is released', async () => {
    const container = await mount();
    const before = parts(row(container, 'Program.cs'));
    carry(container, 'Ui.cs', 68, false);
    await until(() => parts(row(container, 'Program.cs')) > before);
    pointer(document, 'pointerup', 10, 68);
    await until(() => parts(row(container, 'Program.cs')) === before);
  });

  it('never offers a place inside the carried node itself', async () => {
    const container = await mount();
    const before = parts(row(container, 'Program.cs'));
    // src over Program.cs's top quarter: before its own child, so inside itself.
    carry(container, 'src', 68, false);
    await nextFrame();
    expect(parts(row(container, 'Program.cs'))).toBe(before);
    pointer(document, 'pointerup', 10, 68);
    expect(moved).toEqual([]);
  });

  it('never puts a node inside a file', async () => {
    const container = await mount();
    // README.md over App.cs's middle half.
    carry(container, 'README.md', 48);
    expect(moved).toEqual([]);
  });

  it("drops nothing where the app's guard refuses", async () => {
    const container = await mount();
    carry(container, 'README.md', 176);
    expect(moved).toEqual([]);
  });

  it('opens a closed folder held under a carried node, and leaves it closed when the node moves on', async () => {
    const container = await mount();
    carry(container, 'README.md', 144, false);
    await wait(300);
    pointer(document, 'pointermove', 10, 20);
    await wait(600);
    expect(loads).not.toContain('docs');

    pointer(document, 'pointermove', 10, 144);
    await wait(800);
    await until(() => loads.includes('docs') && rows(container).some((at) => at.getAttribute('aria-label') === 'intro.md'));
    expect(moved).toEqual([]);
    // The rows that opened above it took new elements: the carried one is still README.md's.
    expect(document.querySelector('[data-eq-sort-lifted]')?.getAttribute('data-eq-sort-item')).toBe('README.md');
    pointer(document, 'pointerup', 10, 300);
  });

  it('keeps the node busy while the app moves it, then reads both parents again', async () => {
    const container = await mount(['src', 'docs']);
    holdWork();
    // Rows: src 0, App.cs 32, Program.cs 64, Ui.cs 96, docs 128, intro.md 160, locked 192, README.md 224.
    carry(container, 'App.cs', 144);
    expect(moved).toEqual(['src/App.cs>docs|']);
    await until(() => row(container, 'App.cs').getAttribute('aria-disabled') === 'true');
    const read = loads.length;
    finish();
    await until(() => loads.length === read + 2);
    expect(loads.slice(read).sort()).toEqual(['docs', 'src']);
    await until(() => row(container, 'App.cs').getAttribute('aria-disabled') !== 'true');
  });

  it("lands a file as the last child of an open folder when the pointer travels an indent across (#1594)", async () => {
    const container = await mount();
    expect(tree(container).closest('[data-eq-sort]')?.getAttribute('data-eq-sort-indent') ?? tree(container).getAttribute('data-eq-sort-indent')).toBe('16');
    // Between Ui.cs (src's last child) and docs, over docs' top quarter.
    carryAcross(container, 'README.md', 130, 16);
    expect(moved).toEqual(['README.md>src|src/Ui.cs']);
  });

  it('lands it among the top level there when the pointer does not travel across', async () => {
    const container = await mount();
    carryAcross(container, 'README.md', 130, 0);
    expect(moved).toEqual(['README.md>|src']);
  });

  it("lands a folder's last child at the top level after the folder when the pointer travels back", async () => {
    const container = await mount();
    // Ui.cs's own gap, before docs, one indent toward the start.
    carryAcross(container, 'Ui.cs', 110, -16);
    expect(moved).toEqual(['src/Ui.cs>|src']);
  });

  it("never offers a place among the carried folder's own children", async () => {
    const container = await mount();
    const before = parts(row(container, 'App.cs'));
    carryAcross(container, 'src', 40, 16, false);
    await nextFrame();
    expect(parts(row(container, 'App.cs'))).toBe(before);
    pointer(document, 'pointerup', 46, 40);
    expect(moved).toEqual([]);
  });

  it("draws the line from the indentation of the depth the pointer chose, not from the row's own", async () => {
    const container = await mount();
    /** Where the line over docs starts: the inline start inset of the box that holds it, read from the atomic sheet. */
    const lineStart = (): string | null => {
      const classes = new Set(Array.from(row(container, 'docs').querySelectorAll('*')).flatMap((at) => Array.from(at.classList)));
      const sheet = document.getElementById('eq-atomic') as HTMLStyleElement | null;
      for (const rule of Array.from(sheet?.sheet?.cssRules ?? [])) {
        const match = /^\.([\w-]+)\s*\{\s*inset-inline-start:\s*([\d.]+px)/.exec(rule.cssText);
        if (match && classes.has(match[1])) return match[2];
      }
      return null;
    };
    expect(lineStart()).toBeNull();
    carryAcross(container, 'README.md', 130, 0, false);
    await until(() => lineStart() !== null);
    expect(lineStart()).toBe('8px');
    pointer(document, 'pointermove', 46, 130);
    await until(() => lineStart() === '24px');
    pointer(document, 'pointerup', 46, 130);
  });

  it('moves the active node by the keyboard, over the refused folder, into the one above, and keeps the focus', async () => {
    const container = await mount();
    tree(container).focus();
    // The first key lands the keyboard on the tree; End then walks to the last node.
    key(tree(container), 'ArrowDown');
    await nextFrame();
    key(tree(container), 'End');
    await nextFrame();
    expect(active(container)).toBe('README.md');

    key(tree(container), ' ');
    // Inside locked is refused, and passed over: before locked, then inside docs.
    key(tree(container), 'ArrowUp');
    key(tree(container), 'ArrowUp');
    expect(document.querySelector('[data-eq-sort-announcer]')?.textContent).toBe('README.md is inside docs.');
    key(tree(container), 'Enter');
    expect(moved).toEqual(['README.md>docs|']);
    // The Enter that drops is the carry's alone: it presses nothing.
    expect(pressed).toEqual([]);
    await nextFrame();
    expect(document.activeElement).toBe(tree(container));
    expect(active(container)).toBe('README.md');
  });

  it('puts the active node at the end of an open folder by the keyboard, saying where (#1594)', async () => {
    const container = await mount();
    tree(container).focus();
    key(tree(container), 'ArrowDown');
    await nextFrame();
    key(tree(container), 'End');
    await nextFrame();

    key(tree(container), ' ');
    // Inside locked is refused and passed over: before locked, inside docs, before docs.
    key(tree(container), 'ArrowUp');
    key(tree(container), 'ArrowUp');
    key(tree(container), 'ArrowUp');
    expect(document.querySelector('[data-eq-sort-announcer]')?.textContent).toBe('README.md is at position 2 of 4 in Files.');
    key(tree(container), 'ArrowRight');
    expect(document.querySelector('[data-eq-sort-announcer]')?.textContent).toBe('README.md is at position 4 of 4 in src.');
    key(tree(container), 'Enter');
    expect(moved).toEqual(['README.md>src|src/Ui.cs']);
  });
});
