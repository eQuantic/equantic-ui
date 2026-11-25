/**
 * What the gesture core does about the page's text selection (#1598), for every gesture on it: a sortable
 * list, a draggable, a sheet dismissed by dragging. A mouse press on text starts the browser's selection,
 * and the browser extends it on its own mouse moves, which a `pointermove`'s preventDefault does not stop.
 * jsdom starts no selection, so each case makes the one the press would have made, as the browser does.
 */

import { afterEach, describe, expect, it } from 'vitest';
import { followPointer, type PointerGesture } from './pointer-gesture';

function pointer(target: EventTarget, type: string, x: number, y: number): PointerEvent {
  const event = new PointerEvent(type, {
    clientX: x,
    clientY: y,
    bubbles: true,
    cancelable: true,
    pointerType: 'mouse',
    button: 0,
  });
  target.dispatchEvent(event);
  return event;
}

/** The words under the press, selected as a mouse press on text selects them. */
function pressOnText(): { text: HTMLElement; down: PointerEvent } {
  const text = document.createElement('span');
  text.textContent = 'DATE';
  document.body.appendChild(text);
  const down = pointer(text, 'pointerdown', 10, 10);
  document.getSelection()!.selectAllChildren(text);
  return { text, down };
}

/** A gesture that arms past the slop and decides nothing. */
const travelling = (): PointerGesture => ({
  arms: (ev) => Math.abs(ev.clientX - 10) > 12,
  move: () => undefined,
  release: () => undefined,
  cancel: () => undefined,
});

const selected = (): string => document.getSelection()!.toString();

describe('a pointer gesture and the text selection', () => {
  afterEach(() => {
    document.getSelection()!.removeAllRanges();
    document.documentElement.style.userSelect = '';
    document.body.innerHTML = '';
  });

  it('clears the selection the press started once the gesture arms, and lets none start until it ends', () => {
    const { text, down } = pressOnText();
    followPointer(down, travelling());
    expect(selected()).toBe('DATE');

    pointer(document, 'pointermove', 40, 10);
    expect(selected()).toBe('');
    expect(document.documentElement.style.userSelect).toBe('none');
    const started = new Event('selectstart', { bubbles: true, cancelable: true });
    text.dispatchEvent(started);
    expect(started.defaultPrevented).toBe(true);

    pointer(document, 'pointerup', 40, 10);
    expect(document.documentElement.style.userSelect).toBe('');
    const after = new Event('selectstart', { bubbles: true, cancelable: true });
    text.dispatchEvent(after);
    expect(after.defaultPrevented).toBe(false);
  });

  it('leaves the selection to a press that never becomes a gesture', () => {
    const { down } = pressOnText();
    followPointer(down, travelling());

    pointer(document, 'pointermove', 15, 10);
    pointer(document, 'pointerup', 15, 10);
    expect(selected()).toBe('DATE');
    expect(document.documentElement.style.userSelect).toBe('');
  });

  it('holds the selection while any of two gestures is armed, and gives the page its own back after the last', () => {
    document.documentElement.style.userSelect = 'text';
    const { text, down } = pressOnText();
    const first = followPointer(down, travelling());
    const second = followPointer(down, travelling());
    pointer(document, 'pointermove', 40, 10);

    first.cancel();
    expect(document.documentElement.style.userSelect).toBe('none');
    const started = new Event('selectstart', { bubbles: true, cancelable: true });
    text.dispatchEvent(started);
    expect(started.defaultPrevented).toBe(true);

    second.cancel();
    expect(document.documentElement.style.userSelect).toBe('text');
  });

  it('gives the page back its own user-select when the gesture is cancelled', () => {
    document.documentElement.style.userSelect = 'text';
    const { down } = pressOnText();
    const followed = followPointer(down, travelling());

    pointer(document, 'pointermove', 40, 10);
    expect(document.documentElement.style.userSelect).toBe('none');
    followed.cancel();
    expect(document.documentElement.style.userSelect).toBe('text');
  });
});
