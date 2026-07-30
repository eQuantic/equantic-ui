import { describe, expect, it } from 'vitest';
import { Box, BoxStyle, Presence, Text } from './vocabulary';
import { lowerVisualNode } from './lowering';
import { ambientLoweringContext } from './photon-context';

/**
 * Spec §06 enter motion — the client half of the Presence contract: NO wrapper element, the
 * mount-playing animation class rides the lowered child's own root (identical DOM to the C# SSR
 * realizer, so hydration adopts it untouched and the CSS animation plays exactly once per real
 * insertion).
 */
describe('presence lowering (enter motion)', () => {
  const lower = (node: Presence) => lowerVisualNode(node, ambientLoweringContext());

  it('rides the fade class on the child root — no wrapper', () => {
    const child = new Box(new BoxStyle({ width: 40, height: 40 }));
    const html = lower(new Presence(child))!;

    expect(html.tag).toBe('div'); // the Box's own element, not a wrapper
    expect(html.attributes['class']).toContain('eq-presence-fade');
  });

  it("lowers 'slideUp' (the eqc enum shape) to the slide class", () => {
    const html = lower(new Presence(new Box(new BoxStyle({})), 'slideUp'))!;
    expect(html.attributes['class']).toContain('eq-presence-slideup');
  });

  it('prepends — the child keeps its own classes', () => {
    const html = lower(new Presence(new Text('hi', 'caption')))!;
    expect(html.attributes['class']).toMatch(/^eq-presence-fade .*eq-type-caption/);
  });
});
