import { beforeEach, describe, expect, it, vi } from 'vitest';
import { cssFontWeight } from './value-types';

/**
 * What the measurer TELLS the canvas.
 *
 * `FontWeight` lowers to its camelCase member name, and a name is not a weight anywhere CSS parses
 * one: assigning `"regular 11.5px ui-monospace"` to `ctx.font` is an invalid shorthand, which a
 * canvas answers by silently keeping `10px sans-serif`. Everything measured through it was then the
 * advance of a PROPORTIONAL face at the wrong size — the code editor's column width came back 5.55
 * instead of 6.92, and its caret sat four characters from the text it was supposed to be inside.
 * Silent, and wrong by 20%.
 */
describe('the measuring canvas is given a font it can parse', () => {
  const fonts: string[] = [];

  beforeEach(() => {
    fonts.length = 0;
    vi.spyOn(HTMLCanvasElement.prototype, 'getContext').mockReturnValue({
      set font(value: string) {
        fonts.push(value);
      },
      get font() {
        return fonts[fonts.length - 1] ?? '';
      },
      measureText: (text: string) => ({ width: text.length * 7 }),
    } as unknown as CanvasRenderingContext2D);
  });

  it('writes a NUMERIC weight, never the enum member name', async () => {
    const { measurePhotonText } = await import('./photon-context');

    measurePhotonText('0000000000', {
      size: 11.5,
      lineHeight: 14.5,
      weight: 'regular',
      tracking: 0,
      mono: true,
    });

    expect(fonts).toHaveLength(1);
    expect(fonts[0]).toMatch(/^400 11\.5px ui-monospace/);
    expect(fonts[0]).not.toContain('regular');
  });

  it('adds the tracking the lowering draws as letter-spacing, once a character (#983)', async () => {
    const { measurePhotonText } = await import('./photon-context');
    const bodyM = { size: 14, lineHeight: 20, weight: 'regular', tracking: 0.25 };

    // The canvas answers 7 a UTF-16 unit; the spacing stands after each of the 3 characters.
    expect(measurePhotonText('abc', bodyM)).toBeCloseTo(3 * 7 + 3 * 0.25);
    // An emoji with its tone is one character to the browser, however many units it takes.
    expect(measurePhotonText('👍🏽', bodyM)).toBeCloseTo(4 * 7 + 0.25);
    // A fluid size draws its tracking as an em ratio, which follows the scale.
    expect(measurePhotonText('ab', { ...bodyM, fluid: { min: 12, percentOfWindow: 2, max: 18 } }, 2))
      .toBeCloseTo(2 * 7 + 2 * 0.25 * 2);
    expect(measurePhotonText('ab', { ...bodyM, tracking: 0 })).toBe(14);
  });

  // The nine weights OpenType defines, by the names the transpiler emits: the four appended to the
  // vocabulary fell to 400 through the switch this mapping used to be (#1082).
  it('maps each of the nine weights to its number', () => {
    expect(cssFontWeight('thin')).toBe(100);
    expect(cssFontWeight('extraLight')).toBe(200);
    expect(cssFontWeight('light')).toBe(300);
    expect(cssFontWeight('regular')).toBe(400);
    expect(cssFontWeight('medium')).toBe(500);
    expect(cssFontWeight('semiBold')).toBe(600);
    expect(cssFontWeight('bold')).toBe(700);
    expect(cssFontWeight('extraBold')).toBe(800);
    expect(cssFontWeight('black')).toBe(900);
    // A twin that already lowered it stays lowered.
    expect(cssFontWeight(600)).toBe(600);
    expect(cssFontWeight(undefined)).toBe(400);
    // A name that is no weight is the regular weight, a key of the object's prototype included.
    expect(cssFontWeight('toString')).toBe(400);
  });
});
