import { describe, it, expect } from 'vitest';
import { timeSpanText } from './timespan-text';
import { format } from './format';
import { timeSpan } from './datetime';

// Every answer below was measured on .NET 10; the conformance suite runs the same formats on both
// sides, in five cultures (TimeSpanFormatConformanceTests).
const a = 37_230_040_000n; // 01:02:03.0040000
const b = -937_845_000_000n; // -1.02:03:04.5000000
const min = -9_223_372_036_854_775_808n;

function refusal(ticks: bigint, text: string): string {
  try {
    return timeSpanText(ticks, text, '.');
  } catch (error) {
    return (error as Error).message;
  }
}

describe('timeSpanText (TimeSpan.ToString(format))', () => {
  it('writes the constant form under c, t and T, whatever the decimal separator', () => {
    for (const letter of ['c', 't', 'T']) {
      expect(timeSpanText(a, letter, ',')).toBe('01:02:03.0040000');
      expect(timeSpanText(b, letter, ',')).toBe('-1.02:03:04.5000000');
      expect(timeSpanText(0n, letter, ',')).toBe('00:00:00');
    }
    expect(timeSpanText(min, 'c', '.')).toBe('-10675199.02:48:05.4775808');
  });

  it('writes the general forms with the decimal separator it is given', () => {
    expect(timeSpanText(a, 'g', '.')).toBe('1:02:03.004');
    expect(timeSpanText(b, 'g', ',')).toBe('-1:2:03:04,5');
    expect(timeSpanText(-5n, 'g', '.')).toBe('-0:00:00.0000005');
    expect(timeSpanText(864_000_000_000n * 100n, 'g', '.')).toBe('100:0:00:00');
    expect(timeSpanText(a, 'G', '.')).toBe('0:01:02:03.0040000');
    expect(timeSpanText(b, 'G', '٫')).toBe('-1:02:03:04٫5000000');
  });

  it('draws a custom format token by token, without the sign', () => {
    expect(timeSpanText(a, 'hh\\:mm', '.')).toBe('01:02');
    expect(timeSpanText(b, 'hh\\:mm', '.')).toBe('02:03');
    expect(timeSpanText(b, 'dd\\.hh\\:mm\\:ss', '.')).toBe('01.02:03:04');
    expect(timeSpanText(min, 'dddddddd', '.')).toBe('10675199');
    expect(timeSpanText(b, 'ddd', '.')).toBe('001');
    expect(timeSpanText(a, '%h', '.')).toBe('1');
    expect(timeSpanText(a, '%h%m', '.')).toBe('12');
    expect(timeSpanText(b, '\\-hh', '.')).toBe('-02');
  });

  it('cuts a fraction to its width, or drops the zeros that end it', () => {
    expect(timeSpanText(a, 'fff', '.')).toBe('004');
    expect(timeSpanText(a, 'FFF', '.')).toBe('004');
    expect(timeSpanText(b, 'FFF', '.')).toBe('5');
    expect(timeSpanText(0n, 'ss\\.FFFFFFF', '.')).toBe('00.');
    expect(timeSpanText(12_345_678n, 'ffffff', '.')).toBe('234567');
    expect(timeSpanText(400_000n, 'ff\\FF', '.')).toBe('04F');
  });

  it('writes quoted text, a backslash in it writing the character after', () => {
    expect(timeSpanText(a, "'lit'mm", '.')).toBe('lit02');
    expect(timeSpanText(a, '"lit"%m', '.')).toBe('lit2');
    expect(timeSpanText(a, "mm' min'", '.')).toBe('02 min');
    expect(timeSpanText(a, "'a\\'b'mm", '.')).toBe("a'b02");
    expect(timeSpanText(a, "'%'hh", '.')).toBe('%01');
  });

  it("refuses what .NET refuses, in .NET's words", () => {
    const bad = 'Input string was not in a correct format.';
    for (const text of [
      'C',
      'd',
      'h',
      'q',
      ' ',
      '%',
      '%%',
      '%c',
      '\\',
      'hh\\',
      'hh:mm',
      'hhh',
      'ffffffff',
    ])
      expect(refusal(a, text)).toBe(bad);
    expect(refusal(a, 'ddddddddd')).toBe(bad);
    expect(refusal(a, '%\\h')).toBe(bad);
    expect(refusal(a, "'abc")).toBe(
      "Cannot find a matching quote character for the character '''.",
    );
    expect(refusal(a, '%"')).toBe("Cannot find a matching quote character for the character '\"'.");
  });
});

describe('format of a TimeSpan', () => {
  it('reaches the TimeSpan writer, a null or an empty format being the constant form', () => {
    const span = timeSpan.fromTicks(a);
    expect(format(span, 'hh\\:mm')).toBe('01:02');
    expect(format(span, 'g', undefined, true)).toBe('1:02:03.004');
    expect(format(span, null)).toBe('01:02:03.0040000');
    expect(format(span, '')).toBe('01:02:03.0040000');
    expect(span.toString()).toBe('01:02:03.0040000');
  });
});
