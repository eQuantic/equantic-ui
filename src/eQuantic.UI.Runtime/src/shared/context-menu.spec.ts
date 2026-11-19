/**
 * The ready-made context menu in the browser (#1531): a right-click opens the Menu's panel where the pointer
 * asked, the menu key opens it under the child, and the arrows and Enter choose as a Menu's do.
 */
import { afterEach, beforeEach, describe, expect, it } from 'vitest';
import { StatefulComponent } from '../core/component';
import { resetContextController } from '../dom/context-request';
import { resetShortcuts } from '../dom/shortcuts';
import { photonTheme } from './design-system.generated';
import { setPhotonTheme } from './photon-context';
import { Box, BoxStyle, Pressable, Text } from './vocabulary';
import { ContextMenu } from './components/ContextMenu';
import { MenuItem } from './components/MenuItem';

setPhotonTheme(photonTheme);

const chosen: number[] = [];

class Page extends StatefulComponent {
  build(): never {
    return new ContextMenu(
      new Pressable(new Box(new BoxStyle({ width: 200, height: 40 }), new Text('file', 'bodyM', photonTheme.textPrimary)), () => {}),
      [new MenuItem('Rename'), Object.assign(new MenuItem('Duplicate'), { disabled: true }), new MenuItem('Delete')],
      (index: number) => chosen.push(index),
    ) as never;
  }
}

const nextFrame = () =>
  new Promise<void>((resolve) => requestAnimationFrame(() => requestAnimationFrame(() => resolve())));

async function until(seen: () => boolean, frames = 30): Promise<void> {
  for (let frame = 0; frame < frames && !seen(); frame++) await nextFrame();
  expect(seen()).toBe(true);
}

function mount(): HTMLElement {
  const host = document.createElement('div');
  document.body.appendChild(host);
  new Page().mount(host);
  return host;
}

const panel = (host: HTMLElement) => host.querySelector('[role="menu"]') as HTMLElement | null;

describe('a ContextMenu, asked for in the browser', () => {
  beforeEach(() => {
    chosen.length = 0;
    document.body.innerHTML = '';
  });

  afterEach(() => {
    resetContextController();
    resetShortcuts();
  });

  it('opens where a right-click asked', async () => {
    const host = mount();
    const target = host.querySelector('[data-eq-context]') as HTMLElement;
    target.getBoundingClientRect = () => ({ left: 10, top: 20, width: 200, height: 40 }) as DOMRect;

    target.dispatchEvent(new MouseEvent('contextmenu', { bubbles: true, cancelable: true, button: 2, clientX: 130, clientY: 50 }));
    await until(() => panel(host)?.closest('.eq-anchor-panel') !== null && !!panel(host));

    const placed = panel(host)!.closest('.eq-anchor-panel') as HTMLElement;
    expect(placed.className).not.toContain('eq-anchor-b-start');
    expect(host.querySelectorAll('[role="menuitem"]')).toHaveLength(3);
  });

  it('opens under the child for the menu key, and the keys choose as a Menu’s do', async () => {
    const host = mount();
    const button = host.querySelector('button') as HTMLElement;
    button.focus();

    button.dispatchEvent(new KeyboardEvent('keydown', { key: 'ContextMenu', bubbles: true, cancelable: true }));
    await until(() => !!panel(host));
    expect((panel(host)!.closest('.eq-anchor-panel') as HTMLElement).className).toContain('eq-anchor-b-start');

    window.dispatchEvent(new KeyboardEvent('keydown', { key: 'ArrowDown', bubbles: true, cancelable: true }));
    await nextFrame();
    window.dispatchEvent(new KeyboardEvent('keydown', { key: 'Enter', bubbles: true, cancelable: true }));
    await until(() => chosen.length > 0);

    expect(chosen).toEqual([2]);
  });
});
