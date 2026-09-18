import { describe, expect, it } from 'vitest';
import { lowerVisualNode } from './lowering';
import type { LoweringContext } from './lowering';
import { photonTheme } from './design-system.generated';
import type { AdjustableNode } from './nodes';

const ctx: LoweringContext = { textPrimary: photonTheme.textPrimary };

/** The web half of arrow-key adjustment (C# twin: the Photon host's dispatch). */
describe('adjustable lowering (C# cross-pin)', () => {
  function lower(
    onAdjust?: (d: number) => void,
    role?: AdjustableNode['role'],
    value?: AdjustableNode['value'],
  ) {
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
      ...(value ? { value } : {}),
    };
    return lowerVisualNode(node, ctx);
  }

  it('is one focusable slider-role wrapper', () => {
    const html = lower(undefined, undefined, { now: 0.4, min: 0, max: 1 });
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

  // The VALUE half (C# twin: AdjustableValueTests). This path had no runtime coverage at all: the
  // helper above never passed a value, so every assertion here would have gone on passing with the
  // emission deleted — the client-side lowering is its OWN realizer, and the C# WebRealizer tests
  // do not reach it.
  it('states the value and the range it moves over', () => {
    const html = lower(undefined, undefined, { now: 0.4, min: 0, max: 1 });

    expect(html.attributes['aria-valuenow']).toBe('0.4');
    expect(html.attributes['aria-valuemin']).toBe('0');
    expect(html.attributes['aria-valuemax']).toBe('1');
    expect(html.attributes['aria-valuetext']).toBeUndefined();
  });

  it('rounds to four decimals, as C# "0.####" does', () => {
    const html = lower(undefined, undefined, { now: 1 / 3, min: 0, max: 1 });

    expect(html.attributes['aria-valuenow']).toBe('0.3333');
  });

  it('the words REPLACE the number, never join it', () => {
    // aria-valuetext substitutes for aria-valuenow in what a reader says, so echoing the number
    // into it would spend the one chance to say "40%" saying "0.4" twice.
    const html = lower(undefined, undefined, { now: 0.4, min: 0, max: 1, text: '40%' });

    expect(html.attributes['aria-valuetext']).toBe('40%');
    expect(html.attributes['aria-valuenow']).toBe('0.4');
  });

  it('a group role carries no value, however it is built', () => {
    // A tablist and a radiogroup announce a SELECTION their children already state; ARIA has no
    // valuenow for either role, so one here would be a second answer to their question.
    for (const role of ['tablist', 'radiogroup'] as const) {
      const html = lower(undefined, role, { now: 2, min: 0, max: 5 });
      expect(html.attributes['role']).toBe(role);
      expect(html.attributes['aria-valuenow']).toBeUndefined();
    }
  });

  it('a slider with no value is a GROUP — never a slider without its number', () => {
    // The rule, at the one place that speaks ARIA: role="slider" REQUIRES aria-valuenow, so the
    // combination is not emittable. The C# twin derives the same way (WebLoweringVisitor.AriaRole),
    // which is what keeps `new Adjustable(child, onAdjust)` off the invalid pair.
    const html = lower();

    expect(html.attributes['role']).toBe('group');
    expect(html.attributes['tabindex']).toBe('0');
    expect(html.attributes['aria-label']).toBe('Budget');
    expect(html.attributes['aria-valuenow']).toBeUndefined();
  });
});
