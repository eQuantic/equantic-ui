import { describe, it, expect } from 'vitest';
import { SourceMapConsumer, decodeVlq, RawSourceMap } from './source-map';

describe('decodeVlq', () => {
  it('decodes single values', () => {
    expect(decodeVlq('A')).toEqual([0]);
    expect(decodeVlq('K')).toEqual([5]);
    expect(decodeVlq('Q')).toEqual([8]);
    expect(decodeVlq('J')).toEqual([-4]);
  });

  it('decodes a multi-field segment', () => {
    expect(decodeVlq('AAKQA')).toEqual([0, 0, 5, 8, 0]);
    expect(decodeVlq('UACJA')).toEqual([10, 0, 1, -4, 0]);
  });
});

describe('SourceMapConsumer', () => {
  // One generated line with two segments:
  //   col 0  -> Counter.cs (0-based line 5, col 8), name "Increment"
  //   col 10 -> Counter.cs (0-based line 6, col 4)
  const map: RawSourceMap = {
    version: 3,
    sources: ['Counter.cs'],
    sourcesContent: ['l1\nl2\nl3\nl4\nl5\nl6\nl7'],
    names: ['Increment'],
    mappings: 'AAKQA,UACJA',
  };
  const consumer = new SourceMapConsumer(map);

  it('maps the first segment back to its C# position (1-based line)', () => {
    const pos = consumer.originalPositionFor(1, 0);
    expect(pos).not.toBeNull();
    expect(pos!.source).toBe('Counter.cs');
    expect(pos!.line).toBe(6); // 0-based 5 -> 1-based 6
    expect(pos!.column).toBe(8);
    expect(pos!.name).toBe('Increment');
    expect(pos!.sourceContent).toContain('l6');
  });

  it('maps the second segment', () => {
    const pos = consumer.originalPositionFor(1, 10);
    expect(pos!.line).toBe(7);
    expect(pos!.column).toBe(4);
  });

  it('uses the nearest preceding segment for an in-between column', () => {
    const pos = consumer.originalPositionFor(1, 5);
    expect(pos!.line).toBe(6); // still the first segment
  });

  it('returns null for a line with no mappings', () => {
    expect(consumer.originalPositionFor(2, 0)).toBeNull();
  });
});
