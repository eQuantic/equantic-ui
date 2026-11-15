/**
 * A TreeView in the browser (#1529, #1530, REAL eqc output): one role="tree" stop whose items are div
 * treeitems stating their level and place, walked by keydown as the WAI-ARIA tree pattern, searched by
 * typing, and loading a folder's children when it first opens. The C# twins are TreeViewTests (Photon,
 * through the host) and TreeSemanticsTests (the server's lowering).
 */

import { afterEach, describe, expect, it } from 'vitest';
import { StatefulComponent } from '../core/component';
import { resetShortcuts } from '../dom/shortcuts';
import { photonTheme } from './design-system.generated';
import { setPhotonTheme } from './photon-context';
import { Column } from './vocabulary';
import { TreeNode } from './components/TreeNode';
import { TreeView } from './components/TreeView';

setPhotonTheme(photonTheme);

const nextFrame = () =>
  new Promise<void>((resolve) => requestAnimationFrame(() => requestAnimationFrame(() => resolve())));

const pressed: string[] = [];
let loads = 0;

const leaf = (key: string, label: string) => Object.assign(new TreeNode(key, label), { isLeaf: true });

class FilesHost extends StatefulComponent {
  build(): Column {
    const column = new Column(0);
    column.add(
      new TreeView(
        [
          Object.assign(new TreeNode('src', 'src'), {
            children: [leaf('src/App.cs', 'App.cs'), leaf('src/Assets', 'Assets')],
          }),
          new TreeNode('docs', 'docs'),
          leaf('README.md', 'README.md'),
        ],
        null,
        (node: TreeNode) => pressed.push(node.key),
        {
          label: 'Files',
          loadChildren: async (node: TreeNode) => {
            loads++;
            return [leaf(`${node.key}/intro.md`, 'intro.md')];
          },
        },
      ) as never,
    );
    return column as never;
  }
}

function mount(): HTMLElement {
  const container = document.createElement('div');
  document.body.appendChild(container);
  new FilesHost().mount(container);
  return container;
}

const tree = (container: HTMLElement) => container.querySelector('[role="tree"]') as HTMLElement;

const key = (target: HTMLElement, name: string, init: KeyboardEventInit = {}) =>
  target.dispatchEvent(new KeyboardEvent('keydown', { key: name, bubbles: true, cancelable: true, ...init }));

const active = (container: HTMLElement) => {
  const id = tree(container).getAttribute('aria-activedescendant');
  return id ? (container.querySelector(`#${id}`) as HTMLElement).getAttribute('aria-label') : null;
};

describe('a TreeView, driven in the browser', () => {
  afterEach(() => {
    resetShortcuts();
    document.body.innerHTML = '';
    pressed.length = 0;
    loads = 0;
  });

  it('is one tree of div items that state their level and their place, not wrapped in rows', () => {
    const container = mount();
    const host = tree(container);
    expect(host.getAttribute('tabindex')).toBe('0');
    expect(host.getAttribute('aria-label')).toBe('Files');
    expect(container.querySelector('[role="row"]')).toBeNull();
    const items = [...container.querySelectorAll('[role="treeitem"]')] as HTMLElement[];
    expect(items.map((item) => item.tagName)).toEqual(['DIV', 'DIV', 'DIV']);
    expect(items.map((item) => item.getAttribute('aria-label'))).toEqual(['src', 'docs', 'README.md']);
    expect(items.map((item) => item.getAttribute('aria-posinset'))).toEqual(['1', '2', '3']);
    expect(items.every((item) => item.getAttribute('aria-setsize') === '3')).toBe(true);
    expect(items.every((item) => item.getAttribute('aria-level') === '1')).toBe(true);
    // Open and closed are stated on the nodes that have children, and on nothing else.
    expect(items.map((item) => item.getAttribute('aria-expanded'))).toEqual(['false', 'false', null]);
  });

  it('walks by the arrows, opens with Right, enters, and presses with Enter', async () => {
    const container = mount();
    tree(container).focus();

    key(tree(container), 'ArrowDown');
    await nextFrame();
    expect(active(container)).toBe('src');

    key(tree(container), 'ArrowRight');
    await nextFrame();
    const src = container.querySelector('[aria-label="src"]') as HTMLElement;
    expect(src.getAttribute('aria-expanded')).toBe('true');
    const appCs = container.querySelector('[aria-label="App.cs"]') as HTMLElement;
    expect(appCs.getAttribute('aria-level')).toBe('2');

    key(tree(container), 'ArrowRight');
    await nextFrame();
    expect(active(container)).toBe('App.cs');
    expect(pressed).toEqual([]);

    key(tree(container), 'Enter');
    await nextFrame();
    expect(pressed).toEqual(['src/App.cs']);

    key(tree(container), 'ArrowLeft');
    await nextFrame();
    expect(active(container)).toBe('src');
    key(tree(container), 'ArrowLeft');
    await nextFrame();
    expect((container.querySelector('[aria-label="src"]') as HTMLElement).getAttribute('aria-expanded')).toBe('false');
  });

  it('jumps by typed letters, and leaves Space and a chord to the page', async () => {
    const container = mount();
    tree(container).focus();
    key(tree(container), 'ArrowDown');
    await nextFrame();

    const typed = new KeyboardEvent('keydown', { key: 'r', bubbles: true, cancelable: true });
    tree(container).dispatchEvent(typed);
    await nextFrame();
    expect(active(container)).toBe('README.md');
    expect(typed.defaultPrevented).toBe(true);

    const space = new KeyboardEvent('keydown', { key: ' ', bubbles: true, cancelable: true });
    tree(container).dispatchEvent(space);
    const chord = new KeyboardEvent('keydown', { key: 'd', ctrlKey: true, bubbles: true, cancelable: true });
    tree(container).dispatchEvent(chord);
    await nextFrame();
    expect(active(container)).toBe('README.md');
    expect(space.defaultPrevented).toBe(false);
    expect(chord.defaultPrevented).toBe(false);
  });

  it('keeps the keyboard on the tree when an item is pressed, where aria-activedescendant is read', async () => {
    const container = mount();
    const item = container.querySelector('[aria-label="README.md"]') as HTMLElement;
    const down = new MouseEvent('mousedown', { bubbles: true, cancelable: true });
    item.dispatchEvent(down);
    expect(down.defaultPrevented).toBe(true);
    expect(document.activeElement).toBe(tree(container));
    item.click();
    await nextFrame();
    expect(active(container)).toBe('README.md');
    expect(pressed).toEqual(['README.md']);
  });

  it('loads a folder once, with a loading item under it until its children arrive', async () => {
    const container = mount();
    (container.querySelector('[aria-label="docs"]') as HTMLElement).click();
    await nextFrame();
    expect(container.querySelector('[aria-label="Loading…"]')).not.toBeNull();

    await nextFrame();
    await nextFrame();
    expect(container.querySelector('[aria-label="Loading…"]')).toBeNull();
    const intro = container.querySelector('[aria-label="intro.md"]') as HTMLElement;
    expect(intro.getAttribute('aria-level')).toBe('2');
    expect(pressed).toEqual(['docs']);

    (container.querySelector('[aria-label="docs"]') as HTMLElement).click();
    await nextFrame();
    (container.querySelector('[aria-label="docs"]') as HTMLElement).click();
    await nextFrame();
    expect(container.querySelector('[aria-label="intro.md"]')).not.toBeNull();
    expect(loads).toBe(1);
  });
});
