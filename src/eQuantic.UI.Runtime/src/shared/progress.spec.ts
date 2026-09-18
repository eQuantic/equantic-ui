import { describe, expect, it } from 'vitest';
import { lowerVisualNode } from './lowering';
import type { LoweringContext } from './lowering';
import { photonTheme } from './design-system.generated';
import type { ProgressNode } from './nodes';

const ctx: LoweringContext = { textPrimary: photonTheme.textPrimary };

/**
 * The web half of progress semantics (C# twin: ProgressSemanticsTests).
 *
 * This lowering is its OWN realizer — the C# WebRealizer tests never reach it — and the last time a
 * node's value emission was added, the runtime side shipped with no coverage at all and deleting it
 * outright failed zero tests. So it is written here at the same time as the C# side rather than
 * after a reviewer asks.
 */
describe('progress lowering (C# cross-pin)', () => {
  function lower(value?: ProgressNode['value'], label = 'Uploading') {
    const node: ProgressNode = {
      nodeKind: 'progress',
      child: {
        nodeKind: 'text',
        content: 'bar',
        role: 'label',
      } as unknown as ProgressNode['child'],
      label,
      ...(value ? { value } : {}),
    };
    return lowerVisualNode(node, ctx);
  }

  it('states the progress and the range it covers', () => {
    const html = lower({ now: 0.45, min: 0, max: 1 });

    expect(html.attributes['role']).toBe('progressbar');
    expect(html.attributes['aria-label']).toBe('Uploading');
    expect(html.attributes['aria-valuenow']).toBe('0.45');
    expect(html.attributes['aria-valuemin']).toBe('0');
    expect(html.attributes['aria-valuemax']).toBe('1');
    expect(html.attributes['aria-valuetext']).toBeUndefined();
  });

  it('an indeterminate bar keeps the ROLE and drops only the number', () => {
    // ARIA's own rule, and the INVERSE of the slider's: there a missing value means the node is not
    // a slider and the role is withheld; here the missing value is what the role reports.
    const html = lower();

    expect(html.attributes['role']).toBe('progressbar');
    expect(html.attributes['aria-label']).toBe('Uploading');
    expect(html.attributes['aria-valuenow']).toBeUndefined();
    expect(html.attributes['aria-valuemin']).toBeUndefined();
    expect(html.attributes['aria-valuemax']).toBeUndefined();
  });

  it('the words REPLACE the ratio, never join it', () => {
    const html = lower({ now: 0.43, min: 0, max: 1, text: '3 of 7 files' });

    expect(html.attributes['aria-valuetext']).toBe('3 of 7 files');
    expect(html.attributes['aria-valuenow']).toBe('0.43');
  });

  it('rounds through the SHARED formatter, as every other number does', () => {
    // One spelling of C# TokenCss.Number for the whole lowering — two would let the ARIA values
    // drift from the CSS beside them, which is a finding this repository has already had once.
    const html = lower({ now: 1 / 3, min: 0, max: 1 });

    expect(html.attributes['aria-valuenow']).toBe('0.3333');
  });

  it('is not a control', () => {
    // A progress bar is read, never moved: a tab stop here would put it in the Tab order and a key
    // handler would promise a gesture that does nothing.
    const html = lower({ now: 0.5, min: 0, max: 1 });

    expect(html.attributes['tabindex']).toBeUndefined();
    expect(Object.keys(html.events ?? {})).toHaveLength(0);
  });
});
