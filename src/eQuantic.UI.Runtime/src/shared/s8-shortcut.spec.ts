import { describe, it, expect, beforeEach } from 'vitest';
import { lowerVisualNode } from './lowering';
import { commitShortcuts, activeShortcuts, chordOf, resetShortcuts } from '../dom/shortcuts';
import { photonTheme } from './design-system.generated';
import type { LoweringContext } from './lowering';
import type { VisualNodeValue } from './nodes';

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
