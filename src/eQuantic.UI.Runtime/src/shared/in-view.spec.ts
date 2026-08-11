/**
 * The client half of the C# `InViewTests`: presence, reported on the transitions.
 *
 * jsdom has no `IntersectionObserver`, so this installs one that records what was observed and lets
 * the test deliver entries. Testing against a stub the test itself controls is the only way to
 * assert the CONTRACT — that a second identical entry says nothing, and that the callback is
 * refreshed each pass without the observer being replaced.
 */

import { beforeEach, describe, expect, it, vi } from 'vitest';
import {
  commitInViewObservers,
  declareInView,
  resetInViewForTests,
  scheduleInViewCommit,
} from './in-view';
import { lowerVisualNode } from './lowering';
import type { LoweringContext } from './lowering';
import { photonTheme } from './design-system.generated';
import { effectiveStyle } from './style-atomizer';

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

  /**
   * The marker goes on the CHILD. The wrapper is display:contents, which generates no box — an
   * observer pointed at it watches a 0×0 rectangle at the origin and reports, faithfully, about a
   * place the heading never is. Five headings on the site were observed exactly that way.
   */
  it('marks the child, not the boxless wrapper', () => {
    const context: LoweringContext = { textPrimary: photonTheme.textPrimary };
    const lowered = lowerVisualNode(
      {
        nodeKind: 'inView',
        child: { nodeKind: 'text', content: 'Heading', role: 'label' },
        onChanged: () => {},
        threshold: 0,
      } as never,
      context,
    );

    expect(effectiveStyle(lowered)).toContain('display: contents');
    expect(lowered.attributes['data-eq-inview']).toBeUndefined();
    expect(lowered.children[0].attributes['data-eq-inview']).toBeTruthy();
  });

  /**
   * …and the commit waits for the DOM the pass produced. It used to run while the tree was still a
   * value — it found nothing, cleared the declarations, and a freshly hydrated page carried every
   * marked node with no observer. Only a later re-render attached them, which on a page where
   * nothing else changes is never.
   */
  it('commits after the pass has been written, not during it', async () => {
    const seen: boolean[] = [];
    declareInView('r0/1', { threshold: 0, onChanged: (v) => seen.push(v) });
    scheduleInViewCommit();

    // The element arrives the way a rendered one does: after the pass returned.
    const element = anObservedElement('r0/1');
    expect(observers).toHaveLength(0);

    await Promise.resolve();

    expect(observers).toHaveLength(1);
    observers[0].deliver(element, true);
    expect(seen).toEqual([true]);
  });
});
