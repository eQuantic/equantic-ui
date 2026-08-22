import { describe, expect, it } from 'vitest';
import { lowerVisualNode } from './lowering';
import type { LoweringContext } from './lowering';
import { photonTheme } from './design-system.generated';
import type { AdjustableNode } from './nodes';

const ctx: LoweringContext = { textPrimary: photonTheme.textPrimary };

/** The web half of arrow-key adjustment (C# twin: the Photon host's dispatch). */
describe('adjustable lowering (C# cross-pin)', () => {
  function lower(onAdjust?: (d: number) => void, role?: AdjustableNode['role']) {
    const node: AdjustableNode = {
      nodeKind: 'adjustable',
      child: {
        nodeKind: 'text',
        content: 'knob',
        role: 'label',
      } as unknown as AdjustableNode['child'],
      label: 'Budget',
      onAdjust,
      ...(role ? { role } : {}),
    };
    return lowerVisualNode(node, ctx);
  }

  it('is one focusable slider-role wrapper', () => {
    const html = lower();
    expect(html.attributes['role']).toBe('slider');
    expect(html.attributes['tabindex']).toBe('0');
    expect(html.attributes['aria-label']).toBe('Budget');
  });

  it('arrows nudge and stay off the page scroll', () => {
    const seen: number[] = [];
    const html = lower((d) => seen.push(d));
    const keydown = html.events['keydown'] as unknown as (e: KeyboardEvent) => void;

    let prevented = 0;
    const press = (key: string) =>
      keydown({ key, preventDefault: () => prevented++ } as unknown as KeyboardEvent);

    press('ArrowRight');
    press('ArrowUp');
    press('ArrowLeft');
    press('Enter'); // not the adjustable's key: untouched, unprevented

    expect(seen).toEqual([1, 1, -1]);
    expect(prevented).toBe(3);
  });

  it('a radiogroup reads DOWN as next — reading order, not value', () => {
    // A slider's vertical axis is VALUE (up increases); a radiogroup's is READING ORDER
    // (down is the next option) — WAI-ARIA's own split, and what a vertical RadioGroup needs.
    const seen: number[] = [];
    const html = lower((d) => seen.push(d), 'radiogroup');
    expect(html.attributes['role']).toBe('radiogroup');

    const keydown = html.events['keydown'] as unknown as (e: KeyboardEvent) => void;
    const press = (key: string) =>
      keydown({ key, preventDefault: () => {} } as unknown as KeyboardEvent);

    press('ArrowDown');
    press('ArrowUp');
    press('ArrowRight');
    press('ArrowLeft');

    expect(seen).toEqual([1, -1, 1, -1]);
  });
});
