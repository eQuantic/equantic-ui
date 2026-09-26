import { describe, it, expect } from 'vitest';
import { codePointStarts, nextTextElementLength, textElementStarts } from './text-elements';

/**
 * `StringInfo`'s text elements on this side. The conformance suite runs the same cases against
 * .NET; the last case pins what a browser WITHOUT `Intl.Segmenter` gets: code points.
 */
describe('text elements (StringInfo)', () => {
  const cases: [string, number[]][] = [
    ['abc', [0, 1, 2]],
    ['', []],
    ['e\u0301a', [0, 2]],
    ['\u{1F600}b', [0, 2]],
    ['\u{1F468}\u200D\u{1F469}\u200D\u{1F467}x', [0, 8]],
    ['\u{1F1E7}\u{1F1F7}\u{1F1F5}\u{1F1F9}', [0, 4]],
    ['\u{1F44D}\u{1F3FD}!', [0, 4]],
    ['a\r\nb', [0, 1, 3]],
  ];

  it('begins an element where the platform says one begins', () => {
    for (const [text, starts] of cases) expect(textElementStarts(text)).toEqual(starts);
  });

  it('measures the element at an index, and throws past the end as .NET does', () => {
    expect(nextTextElementLength('e\u0301a', 0)).toBe(2);
    expect(nextTextElementLength('e\u0301a', 2)).toBe(1);
    expect(nextTextElementLength('ab', 2)).toBe(0);
    expect(() => nextTextElementLength('ab', 3)).toThrow(RangeError);
  });

  it('falls back to code points where the browser has no segmenter, and joins nothing', () => {
    expect(codePointStarts('abc')).toEqual([0, 1, 2]);
    expect(codePointStarts('e\u0301a')).toEqual([0, 1, 2]);
    expect(codePointStarts('\u{1F600}b')).toEqual([0, 2]);
    expect(codePointStarts('\uD83D\u0301')).toEqual([0, 1]);
    expect(codePointStarts('')).toEqual([]);
  });
});
