/**
 * The animation system's client half (spec §06, slice 1) — byte-for-byte cross-pins with the C#
 * LoopMotionRealizerTests: LoopMotion lowers to the eq-loop div driving the GENERATED eq-slide-x
 * keyframes (endpoints as custom properties at the style tail, duration on the animation shorthand),
 * a clipping Box lowers to overflow:hidden after border-radius, and the transpiled indeterminate
 * ProgressBar (REAL eqc output) sweeps its flex segment inside the clipped track.
 */

import { effectiveStyle } from './style-atomizer';
import { describe, expect, it } from 'vitest';
import { photonTheme } from './design-system.generated';
import { lowerVisualNode, tokenValue } from './lowering';
import { setPhotonTheme } from './photon-context';
import { Box, BoxStyle, CornerRadii, LoopMotion } from '../index';
import { ProgressBar } from './components/ProgressBar';

setPhotonTheme(photonTheme);

const lower = (node: unknown) =>
  lowerVisualNode(node as never, {
    textPrimary: photonTheme.textPrimary,
    componentContext: { theme: photonTheme, typeScale: 1 },
  });

describe('loop motion lowering (C# LoopMotionRealizerTests cross-pin)', () => {
  it('LoopMotion lowers to the generated keyframe animation', () => {
    const node = lower(
      new LoopMotion(new Box(new BoxStyle({ width: 60, height: 4 })), 'slideX', -0.35, 1.05, 1200),
    );

    expect(node.tag).toBe('div');
    expect(node.attributes['class']).toMatch(/^eq-loop(?: |$)/);
    expect(effectiveStyle(node)).toBe(
      'animation: eq-slide-x 1200ms linear infinite; --eq-loop-from: -35%; --eq-loop-to: 105%',
    );
    expect(node.children).toHaveLength(1);
  });

  it('a clipping Box lowers to overflow hidden after border-radius', () => {
    const node = lower(
      new Box(
        new BoxStyle({
          width: 200,
          height: 4,
          background: photonTheme.surfaceSubtle,
          cornerRadius: new CornerRadii(999),
          clip: true,
        }),
      ),
    );

    expect(effectiveStyle(node)).toContain('border-radius: 999px');
    expect(effectiveStyle(node)).toContain('overflow: hidden');
  });

  it('the transpiled indeterminate ProgressBar sweeps a flex segment inside the clipped track', () => {
    const track = lower(new ProgressBar());

    expect(effectiveStyle(track)).toContain(
      `background-color: ${tokenValue(photonTheme.surfaceSubtle)}`,
    );
    expect(effectiveStyle(track)).toContain('overflow: hidden');

    const layer = track.children[0];
    expect(layer.attributes['class']).toMatch(/^eq-loop(?: |$)/);
    expect(effectiveStyle(layer)).toContain('animation: eq-slide-x 1200ms linear infinite');
    expect(effectiveStyle(layer)).toMatch(/--eq-loop-from: -35%; --eq-loop-to: 105%$/);

    const row = layer.children[0];
    expect(effectiveStyle(row.children[0])).toContain('flex: 300 1 0%');
    expect(effectiveStyle(row.children[1])).toContain('flex: 700 1 0%');
  });

  it('the transpiled determinate ProgressBar keeps the flex-weight split', () => {
    const node = lower(new ProgressBar(0.64));

    expect(effectiveStyle(node.children[0])).toContain('flex: 640 1 0%');
    expect(effectiveStyle(node.children[1])).toContain('flex: 360 1 0%');
    expect(effectiveStyle(node)).not.toContain('overflow');
  });
});
