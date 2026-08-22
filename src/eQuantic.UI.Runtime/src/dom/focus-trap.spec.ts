/**
 * §10 focus management for modal layers: Tab cycles inside, focus lands inside on open, and every
 * close path returns focus to the invoker. The C# realizer pins the SSR half of the same contract
 * (OverlayFocusRealizerTests) — the marker attribute is identical on both producers, which is what
 * lets the controller find traps without any path agreement between them.
 */

import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { activeFocusTraps, commitFocusTraps, resetFocusTraps } from './focus-trap';

/** A modal layer as the lowerings emit it: the marker plus the dialog semantics. */
function layer(...focusables: HTMLElement[]): HTMLElement {
  const el = document.createElement('div');
  el.setAttribute('role', 'dialog');
  el.setAttribute('aria-modal', 'true');
  el.setAttribute('tabindex', '-1');
  el.setAttribute('data-eq-trap', '');
  for (const child of focusables) el.appendChild(child);
  return el;
}

function button(label: string): HTMLButtonElement {
  const el = document.createElement('button');
  el.textContent = label;
  return el;
}

const tab = (shift = false) => {
  // cancelable: a Tab the controller does not let through must be PREVENTABLE, and an event
  // created without the flag reports defaultPrevented false however often it is prevented.
  const event = new KeyboardEvent('keydown', {
    key: 'Tab',
    shiftKey: shift,
    bubbles: true,
    cancelable: true,
  });
  window.dispatchEvent(event);
  return event;
};

describe('modal focus trap (§10)', () => {
  beforeEach(() => {
    document.body.innerHTML = '';
    resetFocusTraps();
  });

  it('takes focus on open and gives it back to the invoker on close', () => {
    const invoker = button('Delete account');
    document.body.appendChild(invoker);
    invoker.focus();
    expect(document.activeElement).toBe(invoker);

    const confirm = button('Confirm');
    const dialog = layer(button('Cancel'), confirm);
    document.body.appendChild(dialog);
    commitFocusTraps();

    expect(activeFocusTraps()).toHaveLength(1);
    // The rAF hop the controller takes (a layer fading in is not focusable until the style lands)
    // is what the manual focus below stands in for in jsdom; what matters here is the RESTORE.
    confirm.focus();

    dialog.remove();
    commitFocusTraps();

    expect(activeFocusTraps()).toHaveLength(0);
    expect(document.activeElement, 'closing returns focus to the invoker').toBe(invoker);
  });

  it('restores even when the layer stops being a trap without being removed', () => {
    // The keep-mounted close (Overlay.Motion): the layer stays in the DOM and animates out, so the
    // MARKER is what goes away — the trap must end on that, not on removal.
    const invoker = button('Open sheet');
    document.body.appendChild(invoker);
    invoker.focus();

    const sheet = layer(button('Share'));
    document.body.appendChild(sheet);
    commitFocusTraps();
    (sheet.querySelector('button') as HTMLElement).focus();

    sheet.removeAttribute('data-eq-trap');
    commitFocusTraps();

    expect(activeFocusTraps()).toHaveLength(0);
    expect(document.activeElement).toBe(invoker);
  });

  it('cycles Tab inside and never reaches the page behind', () => {
    const behind = button('Behind');
    document.body.appendChild(behind);

    const first = button('Cancel');
    const last = button('Confirm');
    const dialog = layer(first, last);
    document.body.appendChild(dialog);
    commitFocusTraps();

    last.focus();
    const forward = tab();
    expect(forward.defaultPrevented, 'the page behind must not be reachable').toBe(true);
    expect(document.activeElement, 'Tab wraps to the first focusable').toBe(first);

    const backward = tab(true);
    expect(backward.defaultPrevented, 'Shift+Tab wraps to the last').toBe(true);
    expect(document.activeElement).toBe(last);

    // Focus escaping by any other route (a click behind, a return from browser chrome) is pulled
    // back by the focusin guard.
    behind.focus();
    expect(dialog.contains(document.activeElement)).toBe(true);
  });

  it('the topmost layer owns the keyboard when dialogs stack', () => {
    const outerButton = button('Outer');
    const outer = layer(outerButton);
    document.body.appendChild(outer);
    commitFocusTraps();

    const innerButton = button('Inner');
    const inner = layer(innerButton);
    document.body.appendChild(inner);
    commitFocusTraps();

    expect(activeFocusTraps()).toHaveLength(2);
    innerButton.focus();
    tab();
    expect(document.activeElement, 'a single focusable cycles to itself').toBe(innerButton);

    // Closing the inner one hands the keyboard back to the outer, and focus with it.
    inner.remove();
    commitFocusTraps();
    expect(activeFocusTraps()).toHaveLength(1);
    tab();
    expect(outer.contains(document.activeElement)).toBe(true);
  });

  it('a layer with nothing focusable holds focus on itself', () => {
    const behind = button('Behind');
    document.body.appendChild(behind);
    behind.focus();

    const empty = layer();
    document.body.appendChild(empty);
    commitFocusTraps();

    const event = tab();
    expect(event.defaultPrevented, 'tabindex=-1 makes the container the fallback').toBe(true);
    expect(document.activeElement).toBe(empty);
  });
});
describe('§10 preferred initial focus', () => {
  beforeEach(() => {
    document.body.innerHTML = '';
    resetFocusTraps();
    // The controller hops one rAF before focusing (a fading-in layer is not focusable until the
    // style lands); a synchronous stub lets these tests observe the LANDING, which is their point.
    vi.stubGlobal('requestAnimationFrame', (cb: FrameRequestCallback) => {
      cb(0);
      return 0;
    });
  });

  afterEach(() => vi.unstubAllGlobals());

  it('the marked SAFE action takes initial focus over the first focusable', () => {
    const trap = document.createElement('div');
    trap.setAttribute('data-eq-trap', '');
    trap.tabIndex = -1;
    const destroy = document.createElement('button');
    destroy.textContent = 'Delete';
    const cancel = document.createElement('button');
    cancel.textContent = 'Cancel';
    cancel.setAttribute('data-eq-initial-focus', '');
    // DOM order puts the destructive action FIRST — the marker must still win.
    trap.appendChild(destroy);
    trap.appendChild(cancel);
    document.body.appendChild(trap);

    commitFocusTraps();

    expect(document.activeElement).toBe(cancel);
  });

  it('without a marker the first focusable keeps the job', () => {
    const trap = document.createElement('div');
    trap.setAttribute('data-eq-trap', '');
    trap.tabIndex = -1;
    const first = document.createElement('button');
    first.textContent = 'OK';
    trap.appendChild(first);
    document.body.appendChild(trap);

    commitFocusTraps();

    expect(document.activeElement).toBe(first);
  });
});
