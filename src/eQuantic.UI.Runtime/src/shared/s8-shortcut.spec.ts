import { describe, it, expect, beforeEach, afterEach } from 'vitest';
import { lowerVisualNode } from './lowering';
import { commitShortcuts, activeShortcuts, chordOf, resetShortcuts } from '../dom/shortcuts';
import { Reconciler } from '../dom/reconciler';
import { photonTheme } from './design-system.generated';
import type { LoweringContext } from './lowering';
import type { AdaptiveNodeValue, AnchoredNode, VisualNodeValue } from './nodes';

const ctx: LoweringContext = { textPrimary: photonTheme.textPrimary };
const marker = (): VisualNodeValue =>
  ({
    nodeKind: 'box',
    style: { width: { kind: 'fixed', value: 10 }, height: { kind: 'fixed', value: 10 } },
  }) as unknown as VisualNodeValue;

function shortcut(
  chord: { key: string; modifiers?: number },
  onPressed: () => void,
): VisualNodeValue {
  return {
    nodeKind: 'shortcut',
    child: marker(),
    chord,
    onPressed,
  } as unknown as VisualNodeValue;
}

/**
 * Spec S8 client lowering — the chord id is the C# WebRealizer.ChordId twin, and MOUNTING is the
 * subscription (the pass's declarations replace the live set when it commits).
 */
describe('S8 shortcuts (C# cross-pin)', () => {
  beforeEach(() => resetShortcuts());

  it('marks the child with the chord and leaves the tree shape alone', () => {
    const node = lowerVisualNode(
      shortcut({ key: 'k', modifiers: 4 }, () => {}),
      ctx,
    );
    // Layout-transparent: the CHILD is what lowered — no wrapper element.
    expect(node.tag).toBe('div');
    expect(node.attributes['data-eq-shortcut']).toBe('command+k');
  });

  it('modifiers serialize in the fixed C# order', () => {
    const node = lowerVisualNode(
      shortcut({ key: 'K', modifiers: 4 | 1 }, () => {}),
      ctx,
    );
    expect(node.attributes['data-eq-shortcut']).toBe('command+shift+k');
  });

  it('a bare key needs no modifier prefix', () => {
    const node = lowerVisualNode(
      shortcut({ key: 'Escape' }, () => {}),
      ctx,
    );
    expect(node.attributes['data-eq-shortcut']).toBe('escape');
  });

  it('declarations go live when the pass commits — and unmounting drops them', () => {
    let fired = 0;
    lowerVisualNode(
      shortcut({ key: 'Escape' }, () => fired++),
      ctx,
    );
    commitShortcuts();
    expect(activeShortcuts()).toHaveLength(1);
    activeShortcuts()[0].handler();
    expect(fired).toBe(1);

    // A pass that does NOT re-lower the subtree (the dialog closed) leaves nothing behind.
    commitShortcuts();
    expect(activeShortcuts()).toHaveLength(0);
  });

  it('command matches ⌘ AND Ctrl — one authored chord, every platform', () => {
    expect(chordOf({ key: 'k', metaKey: true } as KeyboardEvent)).toBe('command+k');
    expect(chordOf({ key: 'k', ctrlKey: true } as KeyboardEvent)).toBe('command+control+k');
    expect(chordOf({ key: 'Escape' } as KeyboardEvent)).toBe('escape');
  });
});

/**
 * The web MOUNTS every arm of an AdaptiveNode and the width shows one; Photon lays out only that
 * one. A binding declared in a hidden arm is mounted and invisible, and before the gate was asked,
 * the LAST declared binding answered at every width — the expanded arm's, because it lowers after
 * the compact one. Measured in Chromium: ⌘F at 700px opened the find bar of the hidden editor.
 *
 * These drive the real window listener with real key events, and ask happy-dom's own matchMedia,
 * so the media condition each gate writes is parsed the way a browser parses it.
 */
describe('S8 shortcuts inside adaptive arms', () => {
  const happyDOM = (
    window as unknown as {
      happyDOM: { setViewport(viewport: { width: number; height: number }): void };
    }
  ).happyDOM;
  const at = (width: number) => happyDOM.setViewport({ width, height: 900 });

  beforeEach(() => resetShortcuts());
  afterEach(() => at(1024));

  const find = (onPressed: () => void) => shortcut({ key: 'f', modifiers: 4 }, onPressed);

  function adaptive(arms: Omit<AdaptiveNodeValue, 'nodeKind'>): VisualNodeValue {
    return { nodeKind: 'adaptive', ...arms } as unknown as VisualNodeValue;
  }

  /** ⌘F through the window, as a person presses it; the event says whether anyone claimed it. */
  function pressFind(): KeyboardEvent {
    const event = new KeyboardEvent('keydown', { key: 'f', metaKey: true, cancelable: true });
    window.dispatchEvent(event);
    return event;
  }

  function mount(node: VisualNodeValue): void {
    lowerVisualNode(node, ctx);
    commitShortcuts();
  }

  it('the arm the width shows answers the chord, at either width', () => {
    const fired: string[] = [];
    mount(
      adaptive({
        compact: find(() => fired.push('compact')),
        expanded: find(() => fired.push('expanded')),
      }),
    );

    at(700);
    pressFind();
    at(1024);
    pressFind();

    expect(fired).toEqual(['compact', 'expanded']);
  });

  it('a chord only a hidden arm binds is left to the browser', () => {
    const fired: string[] = [];
    mount(adaptive({ compact: marker(), expanded: find(() => fired.push('expanded')) }));

    at(700);
    const event = pressFind();

    expect(fired).toEqual([]);
    expect(event.defaultPrevented).toBe(false);
  });

  it('a middle arm answers only across its own range', () => {
    const fired: string[] = [];
    mount(
      adaptive({
        compact: find(() => fired.push('compact')),
        medium: find(() => fired.push('medium')),
        expanded: find(() => fired.push('expanded')),
      }),
    );

    for (const width of [400, 700, 1200]) {
      at(width);
      pressFind();
    }

    expect(fired).toEqual(['compact', 'medium', 'expanded']);
  });

  it("a design's own threshold moves where the chord changes hands", () => {
    const fired: string[] = [];
    mount(
      adaptive({
        compact: find(() => fired.push('compact')),
        expanded: find(() => fired.push('expanded')),
        expandedFrom: 1024,
      }),
    );

    at(900);
    pressFind();
    at(1024);
    pressFind();

    expect(fired).toEqual(['compact', 'expanded']);
  });

  it('an arm inside an arm answers only while both are shown', () => {
    const fired: string[] = [];
    mount(
      adaptive({
        compact: marker(),
        expanded: adaptive({
          compact: find(() => fired.push('inner compact')),
          expanded: marker(),
          expandedFrom: 1200,
        }),
      }),
    );

    at(700); // outer compact: the inner node is not on screen at all
    pressFind();
    at(1000); // outer expanded, inner compact
    pressFind();
    at(1300); // both expanded
    pressFind();

    expect(fired).toEqual(['inner compact']);
  });

  it("a panel's Escape in a hidden arm does not close it", () => {
    let closed = 0;
    const panel = (): VisualNodeValue =>
      ({
        nodeKind: 'anchored',
        anchor: marker(),
        panel: marker(),
        open: true,
        onDismiss: () => closed++,
      }) as AnchoredNode as unknown as VisualNodeValue;
    mount(adaptive({ compact: panel(), expanded: marker() }));

    at(1024);
    window.dispatchEvent(new KeyboardEvent('keydown', { key: 'Escape', cancelable: true }));
    at(700);
    window.dispatchEvent(new KeyboardEvent('keydown', { key: 'Escape', cancelable: true }));

    expect(closed).toBe(1);
  });

  it('a binding outside every arm answers at any width', () => {
    let fired = 0;
    mount(find(() => fired++));

    at(400);
    pressFind();
    at(1400);
    pressFind();

    expect(fired).toBe(2);
  });
});

/**
 * A FOCUS-SCOPED shortcut is its subtree's own (C# `Shortcut.FocusScoped`, twin of
 * PhotonHost.FocusIsWithin): of two that claim one chord, the one holding the keyboard answers, and
 * with the keyboard in neither the browser keeps the key. Page-wide, the last one mounted answered
 * wherever the keyboard was: F7 typed in the first code diff of a page stepped the second.
 */
describe('S8 focus-scoped shortcuts', () => {
  beforeEach(() => resetShortcuts());

  function scoped(label: string, onPressed: () => void): VisualNodeValue {
    return {
      nodeKind: 'shortcut',
      child: { nodeKind: 'pressable', child: marker(), onPressed: () => {}, label },
      chord: { key: 'F7' },
      onPressed,
      focusScoped: true,
    } as unknown as VisualNodeValue;
  }

  function pressF7(): KeyboardEvent {
    const event = new KeyboardEvent('keydown', { key: 'F7', cancelable: true });
    window.dispatchEvent(event);
    return event;
  }

  it('answers for the subtree the keyboard is in, and leaves the key alone outside every one', () => {
    const fired: string[] = [];
    const parent = document.createElement('div');
    document.body.appendChild(parent);
    try {
      const column = {
        nodeKind: 'column',
        children: [scoped('first', () => fired.push('first')), scoped('second', () => fired.push('second'))],
      } as unknown as VisualNodeValue;
      new Reconciler().reconcile(parent, null, lowerVisualNode(column, ctx));
      commitShortcuts();
      const scopes = [...parent.querySelectorAll<HTMLElement>('[data-eq-focus-scope]')];
      expect(scopes).toHaveLength(2);

      expect(pressF7().defaultPrevented).toBe(false);
      scopes[0].focus();
      expect(pressF7().defaultPrevented).toBe(true);
      scopes[1].focus();
      pressF7();

      expect(fired).toEqual(['first', 'second']);
    } finally {
      parent.remove();
    }
  });
});
