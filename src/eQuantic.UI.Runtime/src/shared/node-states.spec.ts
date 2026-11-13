/**
 * What the browser keeps for a node between renders belongs to the node's POSITION (#1454,
 * docs/ELEMENT-TREE-PLAN.md 3.7). It used to live in module maps keyed by the lowering's path and as
 * properties on DOM elements, so a node that took another's path, or the DOM element the reconciler
 * handed it, inherited what the one before it had kept: a scroll offset, a request already
 * honoured, the code editor's last reveal.
 *
 * Each case builds its tree through the transpiled engine and reads positions from it, as the
 * lowering does, and drives the after-pass the way a commit does.
 */

import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import type { RenderContext } from '../core/types';
import { BuildOwner } from './components/BuildOwner';
import { ElementIds } from './components/ElementIds';
import { CodeEditorController } from './components/CodeEditorController';
import { CodeGrid } from './components/CodeGrid';
import { CodeLanguages } from './components/CodeLanguages';
import { CodePosition } from './components/CodePosition';
import { CodeRange } from './components/CodeRange';
import { photonTheme } from './design-system.generated';
import { commitInViewObservers, declareInView, InViewState } from './in-view';
import { commitCanvasSurfaces, declareCanvas } from './canvas-surface';
import { ElementRoots, enterPass, exitPass } from './element-roots';
import { lowerVisualNode, type LoweringContext } from './lowering';
import { findNodeState, nodeStateFor } from './node-states';
import { photonComponentContext, setPhotonTheme } from './photon-context';
import { commitScrollViewports, declareScrollViewport, ScrollViewportState } from './scroll-viewports';
import { frameOf, positionsOf, specOwner } from './test-positions';
import { Point, Size } from './value-types';
import { CodeSurface, Column, ScrollView, Text, type VisualNode } from './vocabulary';
import type { Element as Position } from './runtime-exports';

setPhotonTheme(photonTheme);

const column = (...children: VisualNode[]): Column => {
  const node = new Column();
  node.children.push(...children);
  return node;
};

/** A DOM element as a scroll view's, whose offset a test can read and the commit can set. */
function aScrollElement(): HTMLElement {
  const view = document.createElement('div');
  let top = 0;
  Object.defineProperty(view, 'scrollTop', {
    get: () => top,
    set: (value: number) => {
      top = value;
    },
    configurable: true,
  });
  Object.defineProperty(view, 'clientHeight', { value: 200, configurable: true });
  document.body.append(view);
  return view;
}

describe('a scroll view keeps its state on its position', () => {
  const disconnected: Element[] = [];

  beforeEach(() => {
    document.body.innerHTML = '';
    disconnected.length = 0;
    vi.stubGlobal(
      'ResizeObserver',
      class {
        private target: Element | null = null;
        observe(target: Element): void {
          this.target = target;
        }
        disconnect(): void {
          if (this.target) disconnected.push(this.target);
        }
      },
    );
  });

  afterEach(() => vi.unstubAllGlobals());

  it('starts at its own offset on an element another position scrolled, and honours its own request', () => {
    const [before, after] = positionsOf(
      new ScrollView(new Text('before', 'bodyM')),
      new ScrollView(new Text('after', 'bodyM')),
    );
    const view = aScrollElement();

    view.setAttribute(
      'data-eq-scroll',
      declareScrollViewport(before, { horizontal: false, request: { version: 1, offset: 120 } }),
    );
    commitScrollViewports();
    expect(view.scrollTop).toBe(120);
    // The reader scrolls on.
    view.scrollTop = 300;

    // The reconciler hands the same element to the node that took the first one's place, whose own
    // request has the same version: it is a request this position never honoured.
    view.setAttribute(
      'data-eq-scroll',
      declareScrollViewport(after, { horizontal: false, request: { version: 1, offset: 40 } }),
    );
    commitScrollViewports();

    expect(view.scrollTop).toBe(40);
  });

  it('shows an element handed to it at the top when it declares no offset, never where it was left', () => {
    const [before, after] = positionsOf(
      new ScrollView(new Text('before', 'bodyM')),
      new ScrollView(new Text('after', 'bodyM')),
    );
    const view = aScrollElement();
    view.setAttribute('data-eq-scroll', declareScrollViewport(before, { horizontal: false, offset: 80 }));
    commitScrollViewports();
    view.scrollTop = 300;

    view.setAttribute(
      'data-eq-scroll',
      declareScrollViewport(after, { horizontal: false, onViewportChanged: () => {} }),
    );
    commitScrollViewports();

    expect(view.scrollTop).toBe(0);
  });

  it('keeps the offset the reader scrolled to while the same position draws it', () => {
    const [position] = positionsOf(new ScrollView(new Text('kept', 'bodyM')));
    const view = aScrollElement();
    view.setAttribute('data-eq-scroll', declareScrollViewport(position, { horizontal: false, offset: 80 }));
    commitScrollViewports();
    view.scrollTop = 300;

    declareScrollViewport(position, { horizontal: false, offset: 80 });
    commitScrollViewports();

    expect(view.scrollTop).toBe(300);
  });

  it('lets go of its observer when its position leaves the tree', () => {
    const owner = specOwner();
    const scroll = new ScrollView(new Text('leaves', 'bodyM'));
    const frame = frameOf(column(scroll), owner);
    const view = aScrollElement();
    view.setAttribute(
      'data-eq-scroll',
      declareScrollViewport(frame.elementAt(scroll)!, { horizontal: false, onViewportChanged: () => {} }),
    );
    commitScrollViewports();
    expect(disconnected).toEqual([]);

    // The next frame no longer holds the scroll view: its position leaves when the frame ends, and
    // its state with it.
    frameOf(column(new Text('alone', 'bodyM')), owner);
    owner.endFrame();

    expect(disconnected).toEqual([view]);
  });
});

describe('an InView and a filling canvas keep their observers on their position', () => {
  /** Observers a test drives by hand: each knows what it watches and whether it was let go. */
  class FakeObserver {
    static made: FakeObserver[] = [];
    target: Element | null = null;
    connected = true;
    constructor(readonly callback: (entries: { target: Element; isIntersecting: boolean }[]) => void) {
      FakeObserver.made.push(this);
    }
    observe(target: Element): void {
      this.target = target;
    }
    disconnect(): void {
      this.connected = false;
    }
  }

  beforeEach(() => {
    document.body.innerHTML = '';
    FakeObserver.made = [];
    vi.stubGlobal('IntersectionObserver', FakeObserver);
    vi.stubGlobal('ResizeObserver', FakeObserver);
  });

  afterEach(() => vi.unstubAllGlobals());

  it('tells an InView that takes another\'s element that it is on screen, and lets the other go', () => {
    const [before, after] = positionsOf(new Text('before', 'bodyM'), new Text('after', 'bodyM'));
    const element = document.createElement('div');
    document.body.append(element);
    const seenBefore: boolean[] = [];
    element.setAttribute('data-eq-inview', declareInView(before, { threshold: 0, onChanged: (v) => seenBefore.push(v) }));
    commitInViewObservers();
    FakeObserver.made[0].callback([{ target: element, isIntersecting: true }]);
    expect(seenBefore).toEqual([true]);

    // The reconciler hands the same element to the InView that took the first one's place.
    const seenAfter: boolean[] = [];
    element.setAttribute('data-eq-inview', declareInView(after, { threshold: 0, onChanged: (v) => seenAfter.push(v) }));
    commitInViewObservers();

    expect(FakeObserver.made[0].connected, "the first position's observer is let go").toBe(false);
    expect(FakeObserver.made).toHaveLength(2);
    FakeObserver.made[1].callback([{ target: element, isIntersecting: true }]);
    expect(seenAfter, 'its first sight is reported, though the element was already on screen').toEqual([true]);
    expect(seenBefore).toEqual([true]);
  });

  it("measures a filling canvas's element again when another position takes it, and lets the other go", () => {
    const [before, after] = positionsOf(new Text('before', 'bodyM'), new Text('after', 'bodyM'));
    const svg = document.createElementNS('http://www.w3.org/2000/svg', 'svg');
    svg.getBoundingClientRect = () => ({ width: 100, height: 50, top: 0, left: 0, right: 100, bottom: 50, x: 0, y: 0, toJSON: () => ({}) });
    document.body.append(svg);
    let drawnBy = '';
    svg.setAttribute('data-eq-canvas-fill', declareCanvas(before, { draw: () => { drawnBy = 'before'; } }));
    commitCanvasSurfaces();

    svg.setAttribute('data-eq-canvas-fill', declareCanvas(after, { draw: () => { drawnBy = 'after'; } }));
    commitCanvasSurfaces();

    expect(drawnBy).toBe('after');
    expect(FakeObserver.made.map((observer) => observer.connected)).toEqual([false, true]);
  });
});

describe('a code surface keeps its identity and its reveal on its position', () => {
  const surface = (key: string, editor: CodeEditorController): CodeSurface => {
    const node = new CodeSurface(new Text('', 'labelSmall'), editor);
    node.key = key;
    return node;
  };
  const anEditor = (): CodeEditorController => {
    const editor = new CodeEditorController('one\ntwo\nthree', CodeLanguages.for('csharp'));
    editor.grid = new CodeGrid(new Point(0, 0), new Size(8, 18));
    return editor;
  };
  /** Lowers `node` in a pass of `roots`, as a page renders: the root's owner keeps its positions from
   * one render to the next. */
  const lower = (node: CodeSurface, roots: ElementRoots) => {
    enterPass(roots, null, true);
    try {
      return lowerVisualNode(node as never, {
        textPrimary: photonTheme.textPrimary,
        componentContext: { theme: photonTheme, typeScale: 1 },
      } as LoweringContext);
    } finally {
      exitPass();
    }
  };
  /** The caret the reveal scrolls into view, found as the reveal finds it: by the surface's id. */
  const aCaretFor = (id: string) => {
    const host = document.createElement('div');
    host.setAttribute('data-eq-code', id);
    const caret = document.createElement('div');
    caret.className = 'eq-code-caret';
    const scrolled = vi.fn();
    caret.scrollIntoView = scrolled;
    host.append(caret);
    document.body.append(host);
    return scrolled;
  };

  beforeEach(() => {
    document.body.innerHTML = '';
    vi.useFakeTimers();
  });

  afterEach(() => vi.useRealTimers());

  it("carries its position's id, the same while the position keeps it", () => {
    const roots = new ElementRoots(null);
    const editor = anEditor();
    const id = lower(surface('a', editor), roots).attributes['data-eq-code'];
    expect(Number(id)).toBeGreaterThan(0);

    expect(lower(surface('a', editor), roots).attributes['data-eq-code']).toBe(id);
    expect(lower(surface('b', editor), roots).attributes['data-eq-code']).not.toBe(id);
  });

  it('reveals a caret the model moved on the position that drew it, and none on one that took its place', () => {
    const roots = new ElementRoots(null);
    const editor = anEditor();
    const id = lower(surface('a', editor), roots).attributes['data-eq-code']!;

    // Kept: the model moves its caret, and the next render brings it into view.
    editor.select(new CodeRange(new CodePosition(2, 1)), false);
    lower(surface('a', editor), roots);
    const keptCaret = aCaretFor(id);
    vi.advanceTimersByTime(100);
    expect(keptCaret).toHaveBeenCalled();

    // Replaced: a surface takes the first one's place over another model whose caret has moved as
    // often. Its first render is where it opened: nothing of the first surface's reveal is its.
    const other = anEditor();
    other.select(new CodeRange(new CodePosition(1, 1)), false);
    other.select(new CodeRange(new CodePosition(2, 2)), false);
    const replacedId = lower(surface('b', other), roots).attributes['data-eq-code']!;
    const replacedCaret = aCaretFor(replacedId);
    vi.advanceTimersByTime(100);

    expect(replacedId).not.toBe(id);
    expect(replacedCaret).not.toHaveBeenCalled();
  });
});

describe('a position keeps one kind of state', () => {
  it('refuses to be asked for another kind than the one it keeps, naming both', () => {
    const [position] = positionsOf(new ScrollView(new Text('one kind', 'bodyM')));
    nodeStateFor(position, ScrollViewportState);

    expect(findNodeState(position, ScrollViewportState)).toBeInstanceOf(ScrollViewportState);
    expect(() => findNodeState(position, InViewState)).toThrow(/ScrollViewportState.*InViewState/);
  });
});

describe('the trees of a document count their ids together', () => {
  it('never gives two positions the same id when their owners share one count', () => {
    const ids = new ElementIds();
    const owner = () =>
      BuildOwner.structural((component, built) => component.build(built) as unknown as VisualNode, false, ids);
    const context = photonComponentContext() as unknown as RenderContext;
    const all: number[] = [];
    const collect = (position: Position) => {
      all.push(position.id);
      position.visitChildren(collect);
    };
    collect(owner().build(column(new Text('first', 'bodyM')), context, 'compact', true));
    collect(owner().build(column(new Text('second', 'bodyM')), context, 'compact', true));

    expect(all).toHaveLength(4);
    expect(new Set(all).size).toBe(all.length);
  });
});
