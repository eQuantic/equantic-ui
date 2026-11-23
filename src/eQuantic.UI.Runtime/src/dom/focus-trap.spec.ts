/**
 * §10 focus management for modal layers: Tab cycles inside, focus lands inside on open, and every
 * close path returns focus to the invoker. The C# realizer pins the SSR half of the same contract
 * (OverlayFocusRealizerTests) — the marker attribute is identical on both producers, which is what
 * lets the controller find traps without any path agreement between them.
 */

import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { activeFocusTraps, commitFocusTraps, resetFocusTraps } from './focus-trap';
import { gateCompactUntil, gateExpandedFrom } from '../shared/style-atomizer';

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

  // The focused control hears Tab before the trap does (#598): a code editor at either end of a
  // dialog indents on Tab, and the trap cycles only a Tab nothing consumed. Heard in the capture
  // phase, the trap moved the focus and prevented the key before the editor ever saw it.
  it('cycles only a Tab the focused control did not consume', () => {
    const first = button('Cancel');
    const editor = document.createElement('textarea');
    let keeps = true;
    editor.addEventListener('keydown', (event) => {
      if (event.key === 'Tab' && keeps) event.preventDefault();
    });
    document.body.appendChild(layer(first, editor));
    commitFocusTraps();
    const tabOn = (target: HTMLElement) =>
      target.dispatchEvent(new KeyboardEvent('keydown', { key: 'Tab', bubbles: true, cancelable: true }));

    editor.focus();
    tabOn(editor);
    expect(document.activeElement, 'the editor kept the Tab it consumed').toBe(editor);

    keeps = false;
    tabOn(editor);
    expect(document.activeElement, 'a Tab it let go wraps to the first focusable').toBe(first);
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

/**
 * #1811: the invoker gone is no reason to drop the focus. The control now at its place among the
 * controls Tab reaches takes it, which is the same control of the next row when a dialog deleted the
 * row that opened it. Photon's twin is ModalFocusReturnTests, over the same rows.
 */
describe('§10 two layers that stop being traps together, kept mounted for their exit', () => {
  beforeEach(() => {
    document.body.innerHTML = '';
    resetFocusTraps();
  });

  it('give the focus to the outermost one opener, from inside the inner one', () => {
    const settingsButton = button('Settings');
    document.body.appendChild(settingsButton);
    settingsButton.focus();
    const reset = button('Reset');
    const settings = layer(reset, button('Close'));
    document.body.appendChild(settings);
    commitFocusTraps();
    reset.focus();
    const keep = button('Keep');
    const confirm = layer(keep, button('Discard'));
    document.body.appendChild(confirm);
    commitFocusTraps();
    keep.focus();

    // Both close with motion: they stay in the document, sibling layers, and only lose their markers.
    confirm.removeAttribute('data-eq-trap');
    settings.removeAttribute('data-eq-trap');
    commitFocusTraps();

    expect(document.activeElement, 'the focus left the closing confirm for the settings dialog opener').toBe(settingsButton);
  });
});

describe('§10 the focus goes to the control in the invoker place when the invoker is gone', () => {
  beforeEach(() => {
    document.body.innerHTML = '';
    resetFocusTraps();
  });

  /** Rows with an Edit and a Delete each, in a list on the page. */
  function rows(...names: string[]): Map<string, HTMLElement> {
    const list = document.createElement('div');
    const byName = new Map<string, HTMLElement>();
    for (const name of names) {
      const row = document.createElement('div');
      row.appendChild(button(`Edit ${name}`));
      row.appendChild(button(`Delete ${name}`));
      list.appendChild(row);
      byName.set(name, row);
    }
    document.body.appendChild(list);
    return byName;
  }

  const deleteOf = (row: HTMLElement) => row.querySelectorAll('button')[1] as HTMLElement;

  /** The confirm dialog opened from `opener`, with the focus inside it as the rAF hop leaves it. */
  function confirmFrom(opener: HTMLElement): HTMLElement {
    opener.focus();
    const confirm = button('Delete');
    const dialog = layer(confirm, button('Cancel'));
    document.body.appendChild(dialog);
    commitFocusTraps();
    confirm.focus();
    return dialog;
  }

  it('a row the dialog deleted hands the focus to the same control of the next row', () => {
    const list = rows('one', 'two', 'three');
    const dialog = confirmFrom(deleteOf(list.get('two')!));

    list.get('two')!.remove();
    dialog.remove();
    commitFocusTraps();

    expect(document.activeElement?.textContent).toBe('Delete three');
  });

  it('the last row deleted hands the focus to the row before it', () => {
    const list = rows('one', 'two', 'three');
    const dialog = confirmFrom(deleteOf(list.get('three')!));

    list.get('three')!.remove();
    dialog.remove();
    commitFocusTraps();

    expect(document.activeElement?.textContent).toBe('Delete two');
  });

  it('a control inside a lower dialog is the place, when the dialog over it closes', () => {
    const lower = layer(button('Keep'), button('Remove'), button('Close'));
    document.body.appendChild(lower);
    commitFocusTraps();
    const remove = lower.querySelectorAll('button')[1] as HTMLElement;
    const upper = confirmFrom(remove);

    remove.remove();
    upper.remove();
    commitFocusTraps();

    expect(document.activeElement?.textContent, 'inside the dialog still open, never the page').toBe('Close');
  });

  it('a row removed after the dialog closed hands the focus to the next row', () => {
    const list = rows('one', 'two', 'three');
    const dialog = confirmFrom(deleteOf(list.get('two')!));
    dialog.remove();
    commitFocusTraps();
    expect(document.activeElement?.textContent, 'the row is still there').toBe('Delete two');

    // The server answered: the row goes in a later pass.
    list.get('two')!.remove();
    commitFocusTraps();

    expect(document.activeElement?.textContent).toBe('Delete three');
  });

  it('a row removed later leaves a focus the user moved where it is', () => {
    const list = rows('one', 'two', 'three');
    const dialog = confirmFrom(deleteOf(list.get('two')!));
    dialog.remove();
    commitFocusTraps();
    const moved = list.get('three')!.querySelector('button') as HTMLElement;
    moved.focus();
    commitFocusTraps();

    list.get('two')!.remove();
    commitFocusTraps();

    expect(document.activeElement).toBe(moved);
  });

  it('two dialogs closing together give the focus to the outermost one opener', () => {
    // A control before the opener: the confirm's opener sat first in its dialog, and the page's first
    // control is not the settings dialog's opener, so a place taken across scopes shows.
    document.body.appendChild(button('Before'));
    const settingsButton = button('Settings');
    document.body.appendChild(settingsButton);
    settingsButton.focus();
    const reset = button('Reset');
    const settings = layer(reset, button('Close'));
    document.body.appendChild(settings);
    commitFocusTraps();
    const confirm = confirmFrom(reset);

    confirm.remove();
    settings.remove();
    commitFocusTraps();

    expect(document.activeElement).toBe(settingsButton);
  });
});

/**
 * A layer in an adaptive arm the window hides lets go of the keyboard (#1185). The web mounts every arm and
 * its CSS shows one, so a drawer left open in the compact arm kept its marker when the window widened, and
 * held the keyboard in a layer nobody could see. The controller asks the arms' gates when the Tab or the
 * focus arrives, from the same media conditions the gates' CSS applies.
 */
describe('§10 a layer in a hidden adaptive arm', () => {
  let width = 700;
  const matches = (media: string): boolean => {
    const min = /min-width:\s*([\d.]+)px/.exec(media);
    const max = /max-width:\s*([\d.]+)px/.exec(media);
    return (!min || width >= Number(min[1])) && (!max || width <= Number(max[1]));
  };

  beforeEach(() => {
    document.body.innerHTML = '';
    resetFocusTraps();
    width = 700;
    vi.stubGlobal('matchMedia', (media: string) => ({
      matches: matches(media),
      media,
      addEventListener: () => {},
      removeEventListener: () => {},
    }));
  });

  afterEach(() => {
    vi.unstubAllGlobals();
    resetFocusTraps();
  });

  /** An arm behind `gate`, holding `children`, as the lowerings wrap one. */
  function arm(gate: string, ...children: HTMLElement[]): HTMLElement {
    const el = document.createElement('div');
    el.className = gate;
    for (const child of children) el.appendChild(child);
    document.body.appendChild(el);
    return el;
  }

  it('a drawer in the compact arm cycles Tab at 700, lets go of it at 1024, and holds it again back at 700', () => {
    const page = button('Page');
    document.body.appendChild(page);
    const home = button('Home');
    const settings = button('Settings');
    arm(gateCompactUntil(840), layer(home, settings));
    const sidebar = button('Sidebar');
    arm(gateExpandedFrom(840), sidebar);
    page.focus();
    commitFocusTraps();
    settings.focus();

    expect(tab().defaultPrevented, 'at 700 the drawer is on screen and cycles Tab').toBe(true);
    expect(document.activeElement).toBe(home);

    width = 1024;
    expect(tab().defaultPrevented, 'at 1024 the drawer is hidden, and Tab walks the page').toBe(false);
    sidebar.focus();
    expect(document.activeElement, 'a focus outside the hidden drawer stays where it went').toBe(sidebar);

    width = 700;
    expect(tab().defaultPrevented, 'back at 700 the drawer holds Tab again').toBe(true);
    expect(document.activeElement).toBe(home);
  });

  it('a layer outside every arm holds the keyboard at every width', () => {
    const cancel = button('Cancel');
    const dialog = layer(cancel, button('Delete'));
    document.body.appendChild(dialog);
    commitFocusTraps();
    cancel.focus();

    for (const at of [500, 700, 1024]) {
      width = at;
      const outside = button(`Behind ${at}`);
      document.body.appendChild(outside);
      outside.focus();
      expect(dialog.contains(document.activeElement), `at ${at} the focus is pulled back into the dialog`).toBe(true);
    }
  });

  it('a layer that opens in an arm already hidden takes no focus and traps nothing', () => {
    // A served page carries every arm, and a dialog open from the start in the compact arm reaches a wide
    // window behind its gate.
    width = 1024;
    const page = button('Page');
    document.body.appendChild(page);
    page.focus();
    arm(gateCompactUntil(840), layer(button('Accept'), button('Decline')));
    // The initial focus waits a frame; run it now, so a focus scheduled into the hidden layer shows.
    vi.stubGlobal('requestAnimationFrame', (callback: FrameRequestCallback) => {
      callback(0);
      return 0;
    });
    commitFocusTraps();

    expect(document.activeElement, 'a hidden layer takes no initial focus').toBe(page);
    expect(tab().defaultPrevented, 'Tab walks the page').toBe(false);
  });

  it('a hidden drawer that closes leaves the focus where the user moved it', () => {
    const menu = button('Menu');
    arm(gateCompactUntil(840), menu);
    menu.focus();
    const drawer = layer(button('Home'), button('Settings'));
    const compact = arm(gateCompactUntil(840), drawer);
    commitFocusTraps();
    // Two controls in the wide arm, and the user picks the second: the one a place taken for the menu
    // would not pick, so a restore over the user's choice shows.
    const sidebar = button('Sidebar');
    const search = button('Search');
    arm(gateExpandedFrom(840), sidebar, search);

    width = 1024;
    search.focus();
    compact.remove();
    commitFocusTraps();

    expect(document.activeElement, 'the closed drawer gave nothing back over the user’s choice').toBe(search);
  });

  it('a hidden layer and one on screen closing together give the focus to the opener on screen', () => {
    // A drawer opens from the compact arm's menu, and the window then widens: its arm hides with it open.
    const menu = button('Menu');
    const compact = arm(gateCompactUntil(840), menu);
    menu.focus();
    const drawer = layer(button('Home'), button('Settings'));
    compact.appendChild(drawer);
    commitFocusTraps();
    width = 1024;
    // On screen, a dialog opens from the second control of the wide arm: the first is where the drawer's
    // place, taken for the menu, would put the focus.
    const sidebar = button('Sidebar');
    const share = button('Share');
    const wide = arm(gateExpandedFrom(840), sidebar, share);
    share.focus();
    const copy = button('Copy link');
    const dialog = layer(copy, button('Close'));
    wide.appendChild(dialog);
    commitFocusTraps();
    copy.focus();

    // One pass closes both, the drawer first in the document.
    drawer.remove();
    dialog.remove();
    commitFocusTraps();

    expect(document.activeElement, 'the opener on screen takes the focus back, not the hidden menu’s place').toBe(share);
  });

  it('under a hidden layer, the layer on screen owns the keyboard', () => {
    const keep = button('Keep');
    const discard = button('Discard');
    const dialog = layer(keep, discard);
    document.body.appendChild(dialog);
    commitFocusTraps();
    const home = button('Home');
    arm(gateCompactUntil(840), layer(home, button('Settings')));
    commitFocusTraps();

    width = 1024;
    discard.focus();
    expect(tab().defaultPrevented, 'the dialog on screen cycles Tab').toBe(true);
    expect(document.activeElement).toBe(keep);
  });
});
