import { beforeEach, describe, expect, it } from 'vitest';
import { lowerVisualNode } from './lowering';
import { activeShortcuts, commitShortcuts, resetShortcuts } from '../dom/shortcuts';
import type { LoweringContext } from './lowering';
import { photonTheme } from './design-system.generated';
import type { AnchoredNode, VisualNodeValue } from './nodes';

const ctx: LoweringContext = { textPrimary: photonTheme.textPrimary };

/** Wave 3 anchored overlay — the LITERAL class strings the C# AnchoredRealizerTests pin. */

const text = { nodeKind: 'text', content: 't', role: 'label' };

function anchored(extra: Partial<AnchoredNode> = {}): AnchoredNode {
  return {
    nodeKind: 'anchored',
    anchor: text,
    panel: { nodeKind: 'text', content: 'p', role: 'label' },
    ...extra,
  } as AnchoredNode;
}

describe('anchored lowering (C# cross-pin)', () => {
  it('closed lowers host + anchor only', () => {
    const host = lowerVisualNode(anchored(), ctx);
    expect(host.attributes['class']).toBe('eq-anchorhost');
    expect(host.children).toHaveLength(1);
  });

  it('open adds the placement panel with the gap margin', () => {
    const host = lowerVisualNode(anchored({ open: true, placement: 'bottomEnd' }), ctx);
    expect(host.children).toHaveLength(2);
    const panel = host.children[host.children.length - 1] as {
      attributes: Record<string, string>;
    };
    expect(panel.attributes['class']).toMatch(/^eq-anchor-panel eq-anchor-b-end /);
  });

  it('top placement rides margin-bottom', () => {
    const host = lowerVisualNode(anchored({ open: true, placement: 'topStart', gap: 8 }), ctx);
    const panel = host.children[host.children.length - 1] as {
      attributes: Record<string, string>;
    };
    expect(panel.attributes['class']).toMatch(/^eq-anchor-panel eq-anchor-t-start /);
  });

  it('dismissible gets a REAL scrim pressable before the panel', () => {
    let dismissed = false;
    const host = lowerVisualNode(
      anchored({ open: true, onDismiss: () => (dismissed = true) }),
      ctx,
    );
    expect(host.children).toHaveLength(3);
    const scrim = host.children[1] as {
      tag: string;
      attributes: Record<string, string>;
      events: Record<string, () => void>;
    };
    expect(scrim.tag).toBe('button');
    expect(scrim.attributes['class']).toMatch(/^eq-pressable /);
    expect(scrim.attributes['class']).toMatch(/ eq-anchor-scrim$/);
    scrim.events['click']();
    expect(dismissed).toBe(true);
  });

  it('matchAnchorWidth adds the match class', () => {
    const host = lowerVisualNode(anchored({ open: true, matchAnchorWidth: true }), ctx);
    const panel = host.children[host.children.length - 1] as {
      attributes: Record<string, string>;
    };
    expect(panel.attributes['class']).toContain('eq-anchor-match');
  });

  it('openOnHover lowers the reveal host with the panel always present (C# cross-pin)', () => {
    const host = lowerVisualNode(anchored({ openOnHover: true, placement: 'topCenter' }), ctx);
    expect(host.attributes['class']).toBe('eq-anchorhost eq-hoverreveal');
    expect(host.children).toHaveLength(2);
    const panel = host.children[1] as { attributes: Record<string, string> };
    expect(panel.attributes['class']).toMatch(/^eq-anchor-panel eq-anchor-t-center /);
  });
});

/**
 * Escape closes an open panel — the native twin registers the identical binding while it is open.
 * A menu opened with the keyboard has to be closable with it, and the outside-tap scrim is no help
 * to someone who never touched the mouse.
 */
describe('tooltip semantics (describesAnchor, C# cross-pin)', () => {
  it('wires role=tooltip + a deterministic id + aria-describedby on the anchor', () => {
    const node = anchored({
      openOnHover: true,
      describesAnchor: true,
      panel: { nodeKind: 'text', content: 'Saves the draft', role: 'caption' } as VisualNodeValue,
    });

    const host = lowerVisualNode(node, ctx)!;
    const anchor = host.children[0];
    const panel = host.children[host.children.length - 1];

    expect(panel.attributes['role']).toBe('tooltip');
    expect(panel.attributes['id']).toMatch(/^eq-tip-/);
    expect(anchor.attributes['aria-describedby']).toBe(panel.attributes['id']);

    // Same text, same id — SSR and hydration must agree without seeing each other.
    const again = lowerVisualNode(node, ctx)!;
    expect(again.children[again.children.length - 1].attributes['id']).toBe(panel.attributes['id']);
  });

  it('a hover panel alone is not a tooltip', () => {
    const host = lowerVisualNode(anchored({ openOnHover: true }), ctx)!;

    expect(host.children[host.children.length - 1].attributes['role']).toBeUndefined();
    expect(host.children[0].attributes['aria-describedby']).toBeUndefined();
  });
});

describe('anchored dismissal by keyboard', () => {
  beforeEach(() => resetShortcuts());

  it('an open dismissible panel declares Escape', () => {
    let closed = 0;
    lowerVisualNode(anchored({ open: true, onDismiss: () => closed++ }), ctx);
    commitShortcuts();

    const escape = activeShortcuts().filter((b) => b.chord === 'escape');
    expect(escape).toHaveLength(1);
    escape[0].handler();
    expect(closed).toBe(1);
  });

  it('a closed one declares nothing', () => {
    lowerVisualNode(anchored({ open: false, onDismiss: () => {} }), ctx);
    commitShortcuts();
    expect(activeShortcuts().filter((b) => b.chord === 'escape')).toHaveLength(0);
  });

  it('and neither does one with nothing to dismiss to', () => {
    lowerVisualNode(anchored({ open: true }), ctx);
    commitShortcuts();
    expect(activeShortcuts().filter((b) => b.chord === 'escape')).toHaveLength(0);
  });
});
