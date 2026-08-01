/**
 * Gradient + shimmer client half (animation slice 2) — byte-for-byte cross-pins with the C#
 * GradientShimmerRealizerTests: BoxStyle.gradient lowers to background-image linear-gradient with
 * light-dark() stops, and the transpiled Skeleton (REAL eqc output) carries the rest-hidden glint —
 * clipped track, mirrored gradient halves, the 1.4s loop.
 */

import { effectiveStyle } from './style-atomizer';
import { describe, expect, it } from 'vitest';
import { photonTheme } from './design-system.generated';
import { lowerVisualNode, tokenValue } from './lowering';
import { setPhotonTheme } from './photon-context';
import { Box, BoxStyle, Color, ColorToken, GridPattern, LinearGradient, Text } from '../index';
import { Skeleton } from './components/Skeleton';

setPhotonTheme(photonTheme);

const lower = (node: unknown) =>
  lowerVisualNode(node as never, {
    textPrimary: photonTheme.textPrimary,
    componentContext: { theme: photonTheme, typeScale: 1 },
  });

describe('gradient + shimmer lowering (C# GradientShimmerRealizerTests cross-pin)', () => {
  it('a gradient Box lowers to background-image after background-color', () => {
    const node = lower(
      new Box(
        new BoxStyle({
          width: 120,
          height: 40,
          background: photonTheme.surfaceSubtle,
          gradient: new LinearGradient(
            new ColorToken(Color.transparent),
            photonTheme.surfaceHighlight,
          ),
        }),
      ),
    );

    expect(effectiveStyle(node)).toContain(
      `background-color: ${tokenValue(photonTheme.surfaceSubtle)}; ` +
        `background-image: linear-gradient(to right, #00000000, ${tokenValue(photonTheme.surfaceHighlight)})`,
    );
  });

  it('toBottom emits the CSS keyword', () => {
    const node = lower(
      new Box(
        new BoxStyle({
          width: 120,
          height: 40,
          gradient: new LinearGradient(
            photonTheme.scrim,
            new ColorToken(Color.transparent),
            'toBottom',
          ),
        }),
      ),
    );

    expect(effectiveStyle(node)).toContain('background-image: linear-gradient(to bottom, ');
  });

  // The DIAGONAL axes — mirrors C# GradientDirection_Diagonals_EmitTheKeyword.
  it.each([
    ['toBottomRight', 'to bottom right'],
    ['toBottomLeft', 'to bottom left'],
  ])('%s emits the CSS keyword', (direction, keyword) => {
    const node = lower(
      new Box(
        new BoxStyle({
          width: 32,
          height: 32,
          gradient: new LinearGradient(
            photonTheme.colors('primary').base,
            photonTheme.colors('tertiary').base,
            direction,
          ),
        }),
      ),
    );

    expect(effectiveStyle(node)).toContain(`background-image: linear-gradient(${keyword}, `);
  });

  // GRADIENT TEXT — mirrors C# GradientText_ClipsTheBackgroundToTheGlyphs_KeepingAColorFallback.
  it('gradient text clips the background to the glyphs, keeping a color fallback', () => {
    const gradient = new LinearGradient(
      photonTheme.textPrimary,
      photonTheme.colors('primary').base,
    );
    const node = lower(new Text('Headline', 'display', null, 0, { gradient }));

    // effectiveStyle presents the atomic rules SORTED, so assert each declaration on its own
    // rather than assuming the emission adjacency the C# inline-style string has.
    const style = effectiveStyle(node);
    expect(style).toContain(`color: ${tokenValue(photonTheme.textPrimary)}`);
    expect(style).toContain('-webkit-background-clip: text');
    expect(style).toContain('background-clip: text');
    expect(style).toContain('-webkit-text-fill-color: transparent');
    expect(style).toContain(
      `background-image: linear-gradient(to right, ${tokenValue(photonTheme.textPrimary)}, ` +
        `${tokenValue(photonTheme.colors('primary').base)})`,
    );
  });

  // The GRID pattern — mirrors C# GridPattern_EmitsBothHairlineLayers_AndTheCellSize.
  it('the grid pattern emits both hairline layers and the cell size', () => {
    const node = lower(
      new Box(
        new BoxStyle({
          width: 200,
          height: 200,
          pattern: new GridPattern(56, photonTheme.border),
        }),
      ),
    );

    const line = tokenValue(photonTheme.border);
    expect(effectiveStyle(node)).toContain(
      `background-image: linear-gradient(to right, ${line} 1px, transparent 1px), ` +
        `linear-gradient(to bottom, ${line} 1px, transparent 1px)`,
    );
    expect(effectiveStyle(node)).toContain('background-size: 56px 56px, 56px 56px');
  });

  // Mirrors C# GridPattern_UnderAGradient_KeepsLayerOrderAndSizeAlignment.
  it('a gradient over the grid keeps layer order and size alignment', () => {
    const node = lower(
      new Box(
        new BoxStyle({
          width: 200,
          height: 200,
          gradient: new LinearGradient(photonTheme.scrim, new ColorToken(Color.transparent)),
          pattern: new GridPattern(56, photonTheme.border),
        }),
      ),
    );

    const style = effectiveStyle(node);
    expect(style).toContain('background-image: linear-gradient(to right, ');
    expect(style).toContain('background-size: auto, 56px 56px, 56px 56px');
    expect(style.indexOf(tokenValue(photonTheme.scrim))).toBeLessThan(
      style.indexOf('transparent 1px'),
    );
  });

  it('the transpiled Skeleton sweeps the rest-hidden mirrored glint inside the clipped track', () => {
    const track = lower(new Skeleton('line', 160));

    expect(effectiveStyle(track)).toContain(
      `background-color: ${tokenValue(photonTheme.surfaceSubtle)}`,
    );
    expect(effectiveStyle(track)).toContain('border-radius: 999px');
    expect(effectiveStyle(track)).toContain('overflow: hidden');

    const layer = track.children[0];
    expect(layer.attributes['class']).toMatch(/^eq-loop eq-loop-rest-hidden(?: |$)/);
    expect(effectiveStyle(layer)).toContain('animation: eq-slide-x 1400ms linear infinite');
    expect(effectiveStyle(layer)).toMatch(/--eq-loop-from: -100%; --eq-loop-to: 100%$/);

    const glint = layer.children[0];
    const highlight = tokenValue(photonTheme.surfaceHighlight);
    expect(effectiveStyle(glint.children[0].children[0])).toContain(
      `background-image: linear-gradient(to right, #00000000, ${highlight})`,
    );
    expect(effectiveStyle(glint.children[1].children[0])).toContain(
      `background-image: linear-gradient(to right, ${highlight}, #00000000)`,
    );
  });
});
