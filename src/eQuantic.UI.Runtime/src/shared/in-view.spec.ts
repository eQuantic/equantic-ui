/**
 * The client half of the C# `InViewTests`: presence, reported on the transitions.
 *
 * jsdom has no `IntersectionObserver`, so this installs one that records what was observed and lets
 * the test deliver entries. Testing against a stub the test itself controls is the only way to
 * assert the CONTRACT — that a second identical entry says nothing, and that the callback is
 * refreshed each pass without the observer being replaced.
 */

import { beforeEach, describe, expect, it, vi } from 'vitest';
import { commitInViewObservers, declareInView, resetInViewForTests } from './in-view';

interface StubObserver {
  targets: Element[];
  deliver(target: Element, isIntersecting: boolean): void;
}

let observers: StubObserver[] = [];

function installObserver(): void {
  vi.stubGlobal(
    'IntersectionObserver',
    class {
      readonly targets: Element[] = [];

      constructor(private readonly callback: (entries: unknown[]) => void) {
        observers.push(this as unknown as StubObserver);
      }

      observe(target: Element): void {
        this.targets.push(target);
      }

      disconnect(): void {}

      deliver(target: Element, isIntersecting: boolean): void {
        this.callback([{ target, isIntersecting }]);
      }
    },
  );
}

function anObservedElement(path: string): HTMLElement {
  const element = document.createElement('div');
  element.setAttribute('data-eq-inview', path);
  document.body.appendChild(element);
  return element;
}

describe('in-view observers', () => {
  beforeEach(() => {
    document.body.innerHTML = '';
    observers = [];
    resetInViewForTests();
    installObserver();
  });

  it('reports a child coming into view', () => {
    const seen: boolean[] = [];
    const element = anObservedElement('r0/1');
    declareInView('r0/1', { threshold: 0, onChanged: (v) => seen.push(v) });
    commitInViewObservers();

    observers[0].deliver(element, true);

    expect(seen).toEqual([true]);
  });

  // A callback that fires on every crossing report is one every caller has to debounce.
  it('says nothing when the answer has not changed', () => {
    const seen: boolean[] = [];
    const element = anObservedElement('r0/1');
    declareInView('r0/1', { threshold: 0, onChanged: (v) => seen.push(v) });
    commitInViewObservers();

    observers[0].deliver(element, true);
    observers[0].deliver(element, true);
    observers[0].deliver(element, false);

    expect(seen).toEqual([true, false]);
  });

  /**
   * A rebuilt tree hands over a NEW closure over new state every pass, so the callback has to be
   * refreshed — but re-observing would fire an initial report each render, and a table of contents
   * would jump back to whatever is on screen every time any state changed.
   */
  it('refreshes the callback across passes without replacing the observer', () => {
    const first: boolean[] = [];
    const second: boolean[] = [];
    const element = anObservedElement('r0/1');

    declareInView('r0/1', { threshold: 0, onChanged: (v) => first.push(v) });
    commitInViewObservers();

    declareInView('r0/1', { threshold: 0, onChanged: (v) => second.push(v) });
    commitInViewObservers();

    expect(observers).toHaveLength(1);

    observers[0].deliver(element, true);
    expect(first).toEqual([]);
    expect(second).toEqual([true]);
  });

  it('leaves an element nobody declared alone', () => {
    anObservedElement('r0/9');
    commitInViewObservers();

    expect(observers).toHaveLength(0);
  });
});
