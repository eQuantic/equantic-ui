/**
 * Spinner (spec B15) + the B14 value transition on the client — byte-for-byte cross-pins with the
 * C# SpinnerRealizerTests, plus the transpiled stateful ProgressBar end to end: forward changes
 * carry the flex-grow transition, a regression build snaps (adoptConfig direction check), and the
 * next forward change animates again.
 */

import { describe, expect, it } from 'vitest';
import { photonTheme } from './design-system.generated';
import { lowerVisualNode, tokenValue } from './lowering';
import { setPhotonTheme } from './photon-context';
import { Box, BoxStyle, Flexible, Row, Spacer, Spinner } from '../index';
import { ProgressBar } from './components/ProgressBar';

setPhotonTheme(photonTheme);

const lower = (node: unknown) =>
  lowerVisualNode(node as never, {
    textPrimary: photonTheme.textPrimary,
    componentContext: { theme: photonTheme, typeScale: 1 },
  });

describe('spinner lowering (C# cross-pin)', () => {
  it('lowers to the eight-bar SVG with stagger delays', () => {
    const node = lower(new Spinner(24, photonTheme.colors('primary').base));

    expect(node.tag).toBe('svg');
    expect(node.attributes['class']).toBe('eq-spinner');
    expect(node.attributes['viewBox']).toBe('0 0 16 16');
    expect(node.attributes['fill']).toBe('currentColor');
    expect(node.attributes['aria-hidden']).toBe('true');
    expect(node.attributes['style']).toBe(
      `width: 24px; height: 24px; color: ${tokenValue(photonTheme.colors('primary').base)}`,
    );

    expect(node.children).toHaveLength(8);
    node.children.forEach((bar, i) => {
      expect(bar.tag).toBe('rect');
      expect(bar.attributes['transform']).toBe(`rotate(${i * 45} 8 8)`);
      expect(bar.attributes['style']).toBe(`animation-delay: -${i * 100}ms`);
    });
  });
});

describe('B14 value transition (transpiled stateful ProgressBar)', () => {
  it('an animated Flexible carries the token transition', () => {
    const row = new Row(0);
    row.add(new Flexible(new Box(new BoxStyle({ height: 4 })), 640, { animateChanges: true }));
    row.add(new Spacer(360));

    const node = lower(row);
    expect(node.children[0].attributes['style']).toContain(
      'transition: flex-grow var(--eq-motion-base) var(--eq-curve-standard)',
    );
  });

  it('forward changes animate; regressions snap for exactly one build', () => {
    const bar = new ProgressBar(0.3);
    const fillStyle = () => lower(bar).children[0].attributes['style']!;

    expect(fillStyle()).toContain('transition: flex-grow');

    bar.adoptConfig(new ProgressBar(0.6));
    expect(fillStyle()).toContain('transition: flex-grow');

    bar.adoptConfig(new ProgressBar(0.2));
    expect(fillStyle()).not.toContain('transition:');

    bar.adoptConfig(new ProgressBar(0.8));
    expect(fillStyle()).toContain('transition: flex-grow');
  });
});
