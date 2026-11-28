/**
 * Gradient + shimmer client half (animation slice 2) — byte-for-byte cross-pins with the C#
 * GradientShimmerRealizerTests: BoxStyle.gradient lowers to background-image linear-gradient with
 * light-dark() stops, and the transpiled Skeleton (REAL eqc output) carries the rest-hidden glint —
 * clipped track, mirrored gradient halves, the 1.4s loop.
 */

import { declarationFor, effectiveStyle, hashDeclaration, stateStyle } from './style-atomizer';
import { describe, expect, it } from 'vitest';
import { photonTheme } from './design-system.generated';
import { lowerVisualNode, tokenValue } from './lowering';
import { setPhotonTheme } from './photon-context';
import {
  Box,
  BoxStyle,
  Color,
  ColorToken,
  DrawingPattern,
  GridPattern,
  ImagePattern,
  LinearGradient,
  Point,
  RadialGradient,
  StyleDiff,
  Text,
  TileLength,
  VectorDrawing,
  VectorPaint,
  VectorShape,
} from '../index';
import type { DrawingPattern as Drawn, GridPattern as Grid, ImagePattern as Imaged } from './vocabulary';
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
    // No colour of its own since #614: the fallback is the colour it inherits (the C# cross-pin).
    expect(style).not.toMatch(/(^|[^-\w])color:/);
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
          patterns: [new GridPattern(56, photonTheme.border)],
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

  // Mirrors C# Glow_LowersToAnEllipticalRadialGradient_WithAPercentageCenter.
  it('the glow lowers to an elliptical radial-gradient with a percentage center', () => {
    const node = lower(
      new Box(
        new BoxStyle({
          width: 300,
          height: 400,
          glow: new RadialGradient(
            photonTheme.colors('info').base,
            new ColorToken(Color.transparent),
            0.2,
            0,
            800,
            500,
          ),
        }),
      ),
    );

    expect(effectiveStyle(node)).toContain(
      `background-image: radial-gradient(800px 500px at 20% 0%, ` +
        `${tokenValue(photonTheme.colors('info').base)}, #00000000)`,
    );
  });

  // Mirrors C# AllThreeBackgroundLayers_StackInPaintOrder_WithAlignedSizes.
  it('all three background layers stack in paint order with aligned sizes', () => {
    const node = lower(
      new Box(
        new BoxStyle({
          width: 300,
          height: 300,
          gradient: new LinearGradient(photonTheme.scrim, new ColorToken(Color.transparent)),
          glow: new RadialGradient(
            photonTheme.colors('info').base,
            new ColorToken(Color.transparent),
            0.5,
            0.5,
            200,
            120,
          ),
          patterns: [new GridPattern(56, photonTheme.border)],
        }),
      ),
    );

    const style = effectiveStyle(node);
    expect(style.indexOf('linear-gradient(to right')).toBeLessThan(
      style.indexOf('radial-gradient('),
    );
    expect(style.indexOf('radial-gradient(')).toBeLessThan(style.indexOf('transparent 1px'));
    expect(style).toContain('background-size: auto, auto, 56px 56px, 56px 56px');
  });

  // Mirrors C# GridPattern_UnderAGradient_KeepsLayerOrderAndSizeAlignment.
  it('a gradient over the grid keeps layer order and size alignment', () => {
    const node = lower(
      new Box(
        new BoxStyle({
          width: 200,
          height: 200,
          gradient: new LinearGradient(photonTheme.scrim, new ColorToken(Color.transparent)),
          patterns: [new GridPattern(56, photonTheme.border)],
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

  // -- The patterns of #1937, the twin of every C# GradientShimmerRealizerTests case ----------------

  const square = new VectorDrawing(0, 0, 10, 10, [
    new VectorShape('M0 0H10V10H0Z', VectorPaint.solid(Color.fromRgb(0xc8, 0x79, 0x6b))),
  ]);
  const squareUrl =
    `url("data:image/svg+xml,%3Csvg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 10 10' ` +
    `width='10' height='10' preserveAspectRatio='none' fill='none'%3E%3Cpath d='M0 0H10V10H0Z' fill='%23c8796b'/%3E%3C/svg%3E")`;
  const styleOf = (...patterns: (Grid | Drawn | Imaged)[]): string =>
    effectiveStyle(lower(new Box(new BoxStyle({ width: 200, height: 200, patterns }))));
  // The atomic class of a background-image declaration: effectiveStyle resolves a `var()` to its
  // fallback, which would hide the chain itself.
  const classesOf = (...patterns: (Grid | Drawn | Imaged)[]): string[] =>
    (lower(new Box(new BoxStyle({ width: 200, height: 200, patterns }))).attributes['class'] ?? '').split(' ');
  const imageClass = (value: string): string => `eq-${hashDeclaration(declarationFor('background-image', value))}`;

  it('a default grid writes no list but the images and the sizes', () => {
    const style = styleOf(new GridPattern(56, photonTheme.border));
    expect(style).toContain('background-size: 56px 56px, 56px 56px');
    for (const list of ['background-repeat', 'background-position', 'background-origin', 'background-clip', 'background-attachment'])
      expect(style).not.toContain(list);
  });

  it('a drawing tile is a data URL of its artwork, with an auto side', () => {
    const style = styleOf(new DrawingPattern(square, 330));
    expect(style).toContain(`background-image: ${squareUrl}`);
    expect(style).toContain('background-size: 330px auto');
  });

  it('two patterns under a gradient and a glow keep the order and pad every list', () => {
    const gradient = new LinearGradient(photonTheme.scrim, new ColorToken(Color.transparent));
    const glow = new RadialGradient(photonTheme.colors('info').base, new ColorToken(Color.transparent), 0.5, 0.5, 200, 120);
    const style = effectiveStyle(
      lower(
        new Box(
          new BoxStyle({
            width: 300,
            height: 300,
            gradient,
            glow,
            patterns: [
              new GridPattern(56, photonTheme.border),
              new DrawingPattern(square, 40, undefined, null, { horizontalRepeat: 'once' }),
            ],
          }),
        ),
      ),
    );
    const line = tokenValue(photonTheme.border);
    expect(style).toContain(
      `, ${squareUrl}, linear-gradient(to right, ${line} 1px, transparent 1px), linear-gradient(to bottom, ${line} 1px, transparent 1px)`,
    );
    expect(style.indexOf('radial-gradient(')).toBeLessThan(style.indexOf(squareUrl));
    expect(style).toContain('background-size: auto, auto, 40px auto, 56px 56px, 56px 56px');
    expect(style).toContain('background-repeat: repeat, repeat, no-repeat repeat, repeat, repeat');
  });

  it('once across and spaced down writes both keywords', () => {
    expect(styleOf(new DrawingPattern(square, 40, undefined, null, { horizontalRepeat: 'once', verticalRepeat: 'spaced' })))
      .toContain('background-repeat: no-repeat space');
  });

  it('fitted both ways is round', () => {
    expect(styleOf(new DrawingPattern(square, 40, undefined, null, { horizontalRepeat: 'fitted', verticalRepeat: 'fitted' })))
      .toContain('background-repeat: round;');
  });

  it('an anchor with an offset is a calc on each axis', () => {
    expect(
      styleOf(new DrawingPattern(square, 40, undefined, null, { anchor: new Point(0.5, 1), offset: new Point(12, -8) })),
    ).toContain('background-position: calc(50% + 12px) calc(100% - 8px)');
  });

  it('a fraction is a percentage, and a fit its keyword', () => {
    expect(styleOf(new DrawingPattern(square, TileLength.fraction(0.25)))).toContain('background-size: 25% auto');
    expect(styleOf(new DrawingPattern(square, undefined, undefined, null, { fit: 'cover' }))).toContain('background-size: cover');
    expect(styleOf(new DrawingPattern(square, undefined, undefined, null, { fit: 'contain' }))).toContain('background-size: contain');
    expect(styleOf(new DrawingPattern(square, undefined, undefined, null, { fit: 'stretch' }))).toContain('background-size: 100% 100%');
  });

  it('the areas are the origin and the clip', () => {
    const style = styleOf(new DrawingPattern(square, 40, undefined, null, { area: 'content', clipTo: 'padding' }));
    expect(style).toContain('background-origin: content-box');
    expect(style).toContain('background-clip: padding-box');
    expect(styleOf(new GridPattern(50, photonTheme.border, 1, { area: 'border' }))).toContain(
      'background-origin: border-box, border-box',
    );
  });

  it('a window attachment is fixed, and every other layer scrolls', () => {
    const style = effectiveStyle(
      lower(
        new Box(
          new BoxStyle({
            gradient: new LinearGradient(photonTheme.scrim, new ColorToken(Color.transparent)),
            patterns: [new DrawingPattern(square, 40, undefined, null, { attachment: 'window' })],
          }),
        ),
      ),
    );
    expect(style).toContain('background-attachment: scroll, fixed');
  });

  it('an image tile is its source as a CSS string', () => {
    const style = styleOf(new ImagePattern('img/linen.png', 120));
    expect(style).toContain('background-image: url("img/linen.png")');
    expect(style).toContain('background-size: 120px auto');
  });

  it('an opacity is one group around the shapes', () => {
    expect(styleOf(new DrawingPattern(square, 40, undefined, null, { opacity: 0.5 }))).toContain(
      "fill='none'%3E%3Cg opacity='0.5'%3E%3Cpath d='M0 0H10V10H0Z' fill='%23c8796b'/%3E%3C/g%3E%3C/svg%3E",
    );
  });

  it('a tint that differs by mode is a variant per mode, read through the toggles', () => {
    const inked = new VectorDrawing(0, 0, 10, 10, [new VectorShape('M0 0H10V10H0Z', VectorPaint.inherit)]);
    const style = styleOf(new DrawingPattern(inked, 40));
    expect(classesOf(new DrawingPattern(inked, 40))).toContain(imageClass('var(--eq-p0-l, var(--eq-p0-d))'));
    const leg = (color: { r: number; g: number; b: number }): string =>
      `%23${[color.r, color.g, color.b].map((c) => c.toString(16).padStart(2, '0')).join('')}`;
    expect(style).toContain('--eq-p0-l: var(--eq-on-light) url("data:image/svg+xml,');
    expect(style).toContain(`fill='none' color='${leg(photonTheme.textPrimary.light)}'%3E`);
    expect(style).toContain('--eq-p0-d: var(--eq-on-dark) url("data:image/svg+xml,');
    expect(style).toContain(`fill='none' color='${leg(photonTheme.textPrimary.dark)}'%3E`);
  });

  it('a tint that differs by contrast is a variant per leg', () => {
    const inked = new VectorDrawing(0, 0, 10, 10, [new VectorShape('M0 0H10V10H0Z', VectorPaint.inherit)]);
    const tint = new ColorToken(Color.fromRgb(1, 2, 3), Color.fromRgb(4, 5, 6)).withMore(Color.fromRgb(7, 8, 9), Color.fromRgb(10, 11, 12));
    const style = styleOf(new GridPattern(8, photonTheme.border), new DrawingPattern(inked, 40, undefined, tint));
    expect(style).toContain('background-image: var(--eq-p1-dm), linear-gradient(to right, ');
    expect(classesOf(new DrawingPattern(inked, 40, undefined, tint))).toContain(
      imageClass('var(--eq-p0-ls, var(--eq-p0-lm, var(--eq-p0-ds, var(--eq-p0-dm))))'),
    );
    expect(style).toContain('--eq-p1-lm: var(--eq-on-light) var(--eq-on-more) url(');
    expect(style).toContain("color='%23070809'");
  });

  it('a dark source is a variant per mode', () => {
    const tile = new ImagePattern('img/linen.png', 120, undefined, { darkSource: 'img/linen-dark.png' });
    const style = styleOf(tile);
    expect(classesOf(tile)).toContain(imageClass('var(--eq-p0-l, var(--eq-p0-d))'));
    expect(style).toContain('--eq-p0-l: var(--eq-on-light) url("img/linen.png")');
    expect(style).toContain('--eq-p0-d: var(--eq-on-dark) url("img/linen-dark.png")');
  });

  it('a hover gradient pads every list the base wrote', () => {
    const node = lower(
      new Box(
        new BoxStyle({
          patterns: [new DrawingPattern(square, 40, undefined, null, { horizontalRepeat: 'once' })],
          hover: new StyleDiff({ gradient: new LinearGradient(photonTheme.scrim, new ColorToken(Color.transparent)) }),
        }),
      ),
    );
    expect(stateStyle(node, ':hover')).toContain('background-repeat: repeat, no-repeat repeat');
  });

  it('refuses what the C# vocabulary refuses, naming the member', () => {
    expect(() => new DrawingPattern(square, 330, undefined, null, { fit: 'cover' })).toThrow("(Parameter 'Fit')");
    expect(() => new DrawingPattern(square, 330, undefined, null, { anchor: new Point(1.5, 0) })).toThrow("(Parameter 'Anchor')");
    expect(() => new DrawingPattern(square, 330, undefined, null, { offset: new Point(Number.NaN, 0) })).toThrow("(Parameter 'Offset')");
    expect(() => new DrawingPattern(square, 330, undefined, null, { offset: new Point(0, Infinity) })).toThrow("(Parameter 'Offset')");
    expect(() => new DrawingPattern(square, 330, undefined, null, { opacity: 1.5 })).toThrow("(Parameter 'Opacity')");
    expect(() => new ImagePattern('', 120)).toThrow("(Parameter 'source')");
    expect(() => new ImagePattern('a.png', 120, undefined, { darkSource: '' })).toThrow("(Parameter 'DarkSource')");
    expect(() => TileLength.fraction(0)).toThrow("(Parameter 'fraction')");
    expect(() => new DrawingPattern(square, -1)).toThrow("(Parameter 'dp')");
    expect(() => new BoxStyle({ patterns: [new DrawingPattern(square)] })).toThrow("(Parameter 'Fit')");
    expect(() => new BoxStyle({ patterns: [new DrawingPattern(square, undefined, undefined, null, { fit: 'contain' })] })).not.toThrow();
    // A grid's cell and rule are finite and above 0, or Photon's loop over its rules has no end
    // (security review P1).
    for (const bad of [0, -1, Number.NaN, Number.POSITIVE_INFINITY]) {
      expect(() => new GridPattern(bad, photonTheme.border)).toThrow("(Parameter 'Cell')");
      expect(() => new GridPattern(24, photonTheme.border, bad)).toThrow("(Parameter 'LineWidth')");
      expect(() => new GridPattern(24, photonTheme.border, 1, { lineWidth: bad })).toThrow("(Parameter 'LineWidth')");
    }
    expect(() => new GridPattern(24, photonTheme.border, 1e9)).not.toThrow();
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
