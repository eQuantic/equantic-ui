import { describe, expect, it } from 'vitest';
import { lowerVisualNode } from './lowering';
import type { LoweringContext } from './lowering';
import { photonTheme } from './design-system.generated';
import { effectiveStyle } from './style-atomizer';
import type { HtmlNode } from '../core/types';
import type { LiveRegionNode, LiveRegionUrgencyValue } from './nodes';

const ctx: LoweringContext = { textPrimary: photonTheme.textPrimary };

/**
 * The web half of live-region semantics (C# twin: LiveRegionSemanticsTests).
 *
 * This lowering is its OWN realizer — the C# WebRealizer tests never reach it — so a node whose
 * whole purpose is an attribute pair could ship here emitting nothing while the C# side was green.
 * Written at the same time as the C# side rather than after a reviewer asks.
 */
describe('live region lowering (C# cross-pin)', () => {
  function lower(urgency?: LiveRegionUrgencyValue, label?: string): HtmlNode {
    const node: LiveRegionNode = {
      nodeKind: 'liveRegion',
      child: {
        nodeKind: 'text',
        content: 'saved',
        role: 'bodyM',
      } as unknown as LiveRegionNode['child'],
      ...(urgency ? { urgency } : {}),
      ...(label ? { label } : {}),
    };
    return lowerVisualNode(node, ctx);
  }

  // The role and the live value are BOTH stated: the role is what a reader reports the region as,
  // the live value is what makes it watched. `role="alert"` implies assertive in the spec, and
  // implementations have long disagreed about whether an alert inserted after load is announced.
  it('polite is a status that waits for a pause', () => {
    const html = lower('polite');

    expect(html.attributes['role']).toBe('status');
    expect(html.attributes['aria-live']).toBe('polite');
  });

  it('assertive is an alert that cuts in', () => {
    const html = lower('assertive');

    expect(html.attributes['role']).toBe('alert');
    expect(html.attributes['aria-live']).toBe('assertive');
  });

  it('a region with no urgency is polite, like the C# default', () => {
    const html = lower();

    expect(html.attributes['role']).toBe('status');
    expect(html.attributes['aria-live']).toBe('polite');
  });

  // Without this a reader announces only the node that changed, so a banner whose lead-in and body
  // both move is announced as a fragment of itself.
  it('the region is announced WHOLE', () => {
    expect(lower().attributes['aria-atomic']).toBe('true');
  });

  it('a label names the region, and an empty one is not emitted', () => {
    expect(lower('polite', 'Upload status').attributes['aria-label']).toBe('Upload status');
    expect(lower().attributes['aria-label']).toBeUndefined();
  });

  // Read, never operated — the same rule the progress bar has, and for the same reason: promising
  // a gesture that does nothing is worse than promising none.
  it('the region is not a control', () => {
    const html = lower('polite', 'Status');

    expect(html.attributes['tabindex']).toBeUndefined();
    expect(Object.keys(html.events ?? {})).toHaveLength(0);
  });

  it('the child survives the host', () => {
    expect(lower().children).toHaveLength(1);
  });

  it('a wrapper above it sees the child through, fill and cap', () => {
    // The `case 'liveRegion'` arms in `fills` and `capsAt` only matter when a region is NESTED:
    // lowerLiveRegion calls both on its own child directly, so a region at the top exercises
    // neither. Measured on the C# side first — removing either arm left that sweep green.
    const capped = {
      nodeKind: 'box',
      style: { width: { kind: 'fill' }, maxWidth: 320 },
      child: { nodeKind: 'text', content: 'saved', role: 'label', maxLines: 0 },
    };
    const pressed = {
      nodeKind: 'pressable',
      child: { nodeKind: 'liveRegion', child: capped, label: 'Upload status' },
      onPressed: () => {},
    };

    const html = lowerVisualNode(pressed as never, ctx);
    const style = effectiveStyle(html as { attributes: Record<string, string | undefined> });

    expect(style).toContain('width: 100%');
    expect(style).toContain('max-width: 320px');
  });
});
