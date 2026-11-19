/**
 * The context controller (#1531), driven through the real lowering of `ContextTarget`: a right-click, the menu
 * key, Shift+F10 and a finger's hold each reach the innermost enabled target as a `ContextRequest`, and the
 * browser's own menu stays out only where a target answers.
 */
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { StatefulComponent } from '../core/component';
import { photonTheme } from '../shared/design-system.generated';
import { setPhotonTheme } from '../shared/photon-context';
import { Box, BoxStyle, ContextTarget, Pressable, Text } from '../shared/vocabulary';
import type { ContextRequest } from '../shared/value-types';
import { HOLD_MS, resetContextController } from './context-request';

setPhotonTheme(photonTheme);

/** A page that draws one node, through the real lowering and renderer. */
class Page extends StatefulComponent {
  constructor(private readonly node: unknown) {
    super();
  }

  build(): never {
    return this.node as never;
  }
}

function mount(node: unknown): HTMLElement {
  const host = document.createElement('div');
  document.body.appendChild(host);
  new Page(node).mount(host);
  return host;
}

/** A target 200 by 40 at (50, 100), whatever the test runner's layout says. */
function placeAt(element: Element, left = 50, top = 100): void {
  element.getBoundingClientRect = () =>
    ({ left, top, right: left + 200, bottom: top + 40, width: 200, height: 40, x: left, y: top }) as DOMRect;
}

function contextMenu(target: Element, init: MouseEventInit & { pointerType?: string }): MouseEvent {
  const event = new MouseEvent('contextmenu', { bubbles: true, cancelable: true, ...init });
  if (init.pointerType) Object.defineProperty(event, 'pointerType', { value: init.pointerType });
  target.dispatchEvent(event);
  return event;
}

function pointer(type: string, target: Element, x: number, y: number): void {
  const event = new MouseEvent(type, { bubbles: true, cancelable: true, clientX: x, clientY: y });
  Object.defineProperty(event, 'pointerType', { value: 'touch' });
  Object.defineProperty(event, 'pointerId', { value: 7 });
  target.dispatchEvent(event);
}

describe('a ContextTarget, asked for its menu in the browser', () => {
  let heard: (ContextRequest | string)[];

  beforeEach(() => {
    heard = [];
    document.body.innerHTML = '';
  });

  afterEach(() => {
    resetContextController();
    vi.useRealTimers();
  });

  const row = (name: string, enabled = true) =>
    new ContextTarget(new Box(new BoxStyle({ height: 40 }), new Text(name, 'bodyM', photonTheme.textPrimary)), (r) => heard.push(r), {
      enabled,
    });

  it('hears a right-click at its point, and keeps the browser menu out', () => {
    const host = mount(row('src'));
    const target = host.querySelector('[data-eq-context]')!;
    placeAt(target);

    const event = contextMenu(target.querySelector('span') ?? target, { button: 2, clientX: 60, clientY: 106 });

    expect(event.defaultPrevented).toBe(true);
    expect(heard).toHaveLength(1);
    const request = heard[0] as ContextRequest;
    expect(request.at?.x).toBe(10);
    expect(request.at?.y).toBe(6);
  });

  it('leaves a field inside a target its own menu', () => {
    const host = mount(row('src'));
    const target = host.querySelector('[data-eq-context]')!;
    const field = document.createElement('input');
    target.appendChild(field);

    const event = contextMenu(field, { button: 2 });

    expect(event.defaultPrevented).toBe(false);
    expect(heard).toEqual([]);
  });

  it('leaves the browser menu alone where no target answers', () => {
    mount(row('src'));
    const outside = document.createElement('p');
    document.body.appendChild(outside);

    const event = contextMenu(outside, { button: 2 });

    expect(event.defaultPrevented).toBe(false);
    expect(heard).toEqual([]);
  });

  it('answers nothing from a disabled target, and lets the browser menu open', () => {
    const host = mount(row('locked', false));
    const event = contextMenu(host.querySelector('[data-eq-context]')!, { button: 2 });

    expect(event.defaultPrevented).toBe(false);
    expect(heard).toEqual([]);
  });

  it('is answered by the innermost target alone', () => {
    const outer = new ContextTarget(
      new Box(new BoxStyle({ height: 80 }), new ContextTarget(new Text('file', 'bodyM', photonTheme.textPrimary), () => heard.push('inner'))),
      () => heard.push('outer'),
    );
    const host = mount(outer);
    const inner = host.querySelectorAll('[data-eq-context]')[1];

    contextMenu(inner, { button: 2 });

    expect(heard).toEqual(['inner']);
  });

  it('hears the menu key and Shift+F10 with no point, around the focus, and the contextmenu after them is no second ask', () => {
    const host = mount(
      new ContextTarget(new Pressable(new Text('open', 'bodyM', photonTheme.textPrimary), () => undefined), (r) => heard.push(r)),
    );
    const button = host.querySelector('button') as HTMLElement;
    button.focus();

    const menuKey = new KeyboardEvent('keydown', { key: 'ContextMenu', bubbles: true, cancelable: true });
    button.dispatchEvent(menuKey);
    contextMenu(button, { button: 0 });
    button.dispatchEvent(new KeyboardEvent('keydown', { key: 'F10', shiftKey: true, bubbles: true, cancelable: true }));

    expect(menuKey.defaultPrevented).toBe(true);
    expect(heard).toHaveLength(2);
    expect((heard[0] as ContextRequest).at).toBeNull();
    expect((heard[1] as ContextRequest).at).toBeNull();
  });

  it('hears a finger held still, swallows the tap it ends in, and ignores the phone browser’s own contextmenu', () => {
    vi.useFakeTimers();
    const host = mount(row('docs'));
    const target = host.querySelector('[data-eq-context]')!;
    placeAt(target);

    pointer('pointerdown', target, 70, 120);
    const fromThePhone = contextMenu(target, { pointerType: 'touch', clientX: 70, clientY: 120 });
    vi.advanceTimersByTime(HOLD_MS - 1);
    expect(heard).toEqual([]);
    vi.advanceTimersByTime(1);
    pointer('pointerup', target, 70, 120);
    const click = new MouseEvent('click', { bubbles: true, cancelable: true });
    target.dispatchEvent(click);

    expect(fromThePhone.defaultPrevented).toBe(true);
    expect(heard).toHaveLength(1);
    expect((heard[0] as ContextRequest).at?.x).toBe(20);
    expect(click.defaultPrevented).toBe(true);
  });

  it('takes a finger that travels for a scroll, and asks nothing', () => {
    vi.useFakeTimers();
    const host = mount(row('docs'));
    const target = host.querySelector('[data-eq-context]')!;

    pointer('pointerdown', target, 70, 120);
    pointer('pointermove', target, 70, 150);
    vi.advanceTimersByTime(HOLD_MS + 50);

    expect(heard).toEqual([]);
  });

  it('leaves a hold that lifted a sortable item to the lift', () => {
    vi.useFakeTimers();
    const host = mount(row('card'));
    const target = host.querySelector('[data-eq-context]')!;
    const inner = target.firstElementChild as HTMLElement;
    inner.setAttribute('data-eq-sort-lifted', '');

    pointer('pointerdown', inner, 70, 120);
    vi.advanceTimersByTime(HOLD_MS + 10);

    expect(heard).toEqual([]);
  });
});
