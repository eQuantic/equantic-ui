import { describe, expect, it } from 'vitest';
import { lowerVisualNode } from './lowering';
import type { LoweringContext } from './lowering';
import { photonTheme } from './design-system.generated';
import type { AnchoredNode } from './nodes';

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
    const host = lowerVisualNode(anchored({ open: true, onDismiss: () => (dismissed = true) }), ctx);
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
