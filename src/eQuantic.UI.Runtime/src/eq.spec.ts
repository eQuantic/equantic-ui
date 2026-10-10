import { describe, it, expect } from 'vitest';
import { $eq } from './eq';
import { Decimal } from './utils/decimal';
import { DateTime } from './utils/datetime';
import { StringBuilder } from './utils/string-builder';

describe('$eq namespace', () => {
  it('num.dec — exact decimal arithmetic', () => {
    expect($eq.num.dec('0.1').add($eq.num.dec('0.2')).toString()).toBe('0.3');
    expect($eq.num.dec('1')).toBeInstanceOf(Decimal);
  });

  it('num.long — BigInt-backed', () => {
    expect($eq.num.long('9007199254740993')).toBe(9007199254740993n);
  });

  it('math.round — banker’s rounding', () => {
    expect($eq.math.round(2.5)).toBe(2);
    expect($eq.math.round(3.5)).toBe(4);
  });

  it('text.format — number formatting', () => {
    expect($eq.text.format(3.14159, 'F2')).toBe('3.14');
  });

  it('text.stringBuilder — fluent build', () => {
    expect($eq.text.stringBuilder().append('a').append('b').toString()).toBe('ab');
    expect($eq.text.stringBuilder()).toBeInstanceOf(StringBuilder);
  });

  it('time.dateTime — one factory per constructor shape, beside the statics', () => {
    expect($eq.time.dateTime.of(2024, 1, 15).toString()).toBe('01/15/2024 00:00:00');
    expect($eq.time.dateTime.of(2024, 1, 15)).toBeInstanceOf(DateTime);
    expect($eq.time.dateTime.daysInMonth(2024, 2)).toBe(29);
  });

  it('time.timeSpan — factory keeps its statics', () => {
    expect($eq.time.timeSpan.fromHours(25).toString()).toBe('1.01:00:00');
  });

  it('enums — a name, its text and its default, from the shape the compiler writes', () => {
    const shape = { names: ['Active', 'Pending'], keys: ['active', 'pending'], values: [0, 1], flags: false, digits: 8 };
    expect($eq.enums.parse('Pending', shape)).toBe('pending');
    expect($eq.enums.text('pending', shape)).toBe('Pending');
    expect($eq.enums.zero(shape)).toBe('active');
  });

  it('closingLike — a copy is of the closed type its source was built as (#751)', () => {
    class Pair {
      a: number;
      constructor(a: number) {
        this.a = a;
      }
    }
    const ints = $eq.closing(new Pair(1), 'int');
    const doubles = $eq.closing(new Pair(1), 'double');
    const copy = $eq.closingLike(Object.assign(Object.create(Pair.prototype), ints), ints);
    expect($eq.sameClosure(copy, doubles)).toBe(false);
    expect($eq.sameClosure(copy, ints)).toBe(true);
    expect($eq.sameClosure($eq.withPatch(ints, { a: 2 }), doubles)).toBe(false);
    // An unmarked source makes an unmarked copy, which is not taken for another type.
    expect($eq.sameClosure($eq.closingLike(new Pair(1), new Pair(1)), doubles)).toBe(true);
  });

  it('withPatch — the copy is of its closed type before its patch is written (#751)', () => {
    let seen: boolean | undefined;
    class Step {
      _at = 0;
      get at() {
        return this._at;
      }
      set at(value: number) {
        this._at = value;
        seen = $eq.sameClosure(this, doubles);
      }
    }
    const doubles = $eq.closing(new Step(), 'double');
    const ints = $eq.closing(new Step(), 'int');
    expect($eq.withPatch(ints, { at: 2 }).at).toBe(2);
    expect(seen).toBe(false);
  });
});
