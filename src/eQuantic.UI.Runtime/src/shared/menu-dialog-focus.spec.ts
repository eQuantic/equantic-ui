/**
 * A dialog opened from a menu's item gives the focus back to the menu's trigger (#1812). The item left
 * with its menu in the same pass that opened the dialog, so when the dialog recorded its opener the focus
 * was on the body, and Cancel gave it back to nothing. A menu's item now leaves the keyboard on the
 * trigger, as the menu pattern keeps it there while the arrows walk the items. Photon's twin is
 * `ModalFocusReturnTests`, which runs the same case.
 */
import { afterEach, beforeEach, describe, expect, it } from 'vitest';
import { StatefulComponent } from '../core/component';
import { resetFocusTraps } from '../dom/focus-trap';
import { resetShortcuts } from '../dom/shortcuts';
import { photonTheme } from './design-system.generated';
import { setPhotonTheme } from './photon-context';
import { Box, BoxStyle, Column, Overlay, Pressable, Stack, Text } from './vocabulary';
import { Menu } from './components/Menu';
import { MenuItem } from './components/MenuItem';

setPhotonTheme(photonTheme);

/** A row with a menu whose Delete opens the dialog that confirms it. */
class Page extends StatefulComponent {
  asking = false;

  build(): never {
    const page = new Stack();
    page.add(
      new Menu(
        new Text('Actions one', 'bodyM', photonTheme.textPrimary),
        [new MenuItem('Edit'), Object.assign(new MenuItem('Delete'), { destructive: true })],
        (index: number) => {
          if (index === 1) this.setState(() => (this.asking = true));
        },
      ) as never,
    );
    if (this.asking) {
      const actions = new Column(4);
      actions.add(
        new Pressable(
          new Box(new BoxStyle({ width: 80, height: 24 }), new Text('Cancel', 'bodyM', photonTheme.textPrimary)),
          () => this.setState(() => (this.asking = false)),
          { label: 'Cancel', initialFocus: true },
        ) as never,
      );
      page.add(new Overlay(actions, { label: 'Delete the row?' }) as never);
    }
    return page as never;
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

/** A press as a browser makes it: the press's mousedown moves the focus unless the control cancels it,
 *  and the click follows. jsdom moves no focus on a click by itself. */
function pointerPress(target: HTMLElement): void {
  const down = new MouseEvent('mousedown', { bubbles: true, cancelable: true, button: 0 });
  target.dispatchEvent(down);
  if (!down.defaultPrevented) target.focus();
  target.dispatchEvent(new MouseEvent('mouseup', { bubbles: true, cancelable: true, button: 0 }));
  target.click();
}

const trigger = (host: HTMLElement) => host.querySelector('[aria-haspopup="menu"]') as HTMLElement;
const menu = () => document.querySelector('[role="menu"]');
const dialog = () => document.querySelector('[data-eq-trap]');
const cancel = () => document.querySelector('[aria-label="Cancel"]') as HTMLElement;

describe('a dialog opened from a menu item', () => {
  beforeEach(() => {
    document.body.innerHTML = '';
    resetFocusTraps();
  });

  afterEach(() => {
    resetShortcuts();
    resetFocusTraps();
  });

  it('gives the focus back to the menu’s trigger after a pointer chose the item', async () => {
    const host = mount();
    await nextFrame();
    pointerPress(trigger(host));
    await until(() => !!menu());
    expect(document.activeElement).toBe(trigger(host));

    pointerPress(document.querySelectorAll<HTMLElement>('[role="menuitem"]')[1]);
    await until(() => !!dialog() && !menu());

    pointerPress(cancel());
    await until(() => !dialog());

    expect(document.activeElement, 'Cancel gave the focus back to the menu’s trigger').toBe(trigger(host));
  });

  it('gives the focus back to the menu’s trigger after the keyboard chose the item', async () => {
    const host = mount();
    await nextFrame();
    trigger(host).focus();
    trigger(host).click();
    await until(() => !!menu());

    window.dispatchEvent(new KeyboardEvent('keydown', { key: 'ArrowDown', bubbles: true, cancelable: true }));
    await nextFrame();
    window.dispatchEvent(new KeyboardEvent('keydown', { key: 'Enter', bubbles: true, cancelable: true }));
    await until(() => !!dialog() && !menu());

    cancel().focus();
    cancel().click();
    await until(() => !dialog());

    expect(document.activeElement, 'Cancel gave the focus back to the menu’s trigger').toBe(trigger(host));
  });
});
