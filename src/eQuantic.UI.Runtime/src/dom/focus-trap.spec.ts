/**
 * §10 focus management for modal layers: Tab cycles inside, focus lands inside on open, and every
 * close path returns focus to the invoker. The C# realizer pins the SSR half of the same contract
 * (OverlayFocusRealizerTests) — the marker attribute is identical on both producers, which is what
 * lets the controller find traps without any path agreement between them.
 */

import { beforeEach, describe, expect, it } from 'vitest';
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
    expect(document.activeElement).toBe(invoker, 'closing returns focus to the invoker');
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
    expect(forward.defaultPrevented).toBe(true, 'the page behind must not be reachable');
    expect(document.activeElement).toBe(first, 'Tab wraps to the first focusable');

    const backward = tab(true);
    expect(backward.defaultPrevented).toBe(true);
    expect(document.activeElement).toBe(last, 'Shift+Tab wraps to the last');

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
    expect(document.activeElement).toBe(innerButton, 'a single focusable cycles to itself');

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
    expect(event.defaultPrevented).toBe(true);
    expect(document.activeElement).toBe(empty, 'tabindex=-1 makes the container the fallback');
  });
});
