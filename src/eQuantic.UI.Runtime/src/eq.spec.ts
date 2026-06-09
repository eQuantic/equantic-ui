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

  it('time.dateTime — factory keeps its statics', () => {
    expect($eq.time.dateTime(2024, 1, 15).toString()).toBe('01/15/2024 00:00:00');
    expect($eq.time.dateTime(2024, 1, 15)).toBeInstanceOf(DateTime);
    expect($eq.time.dateTime.daysInMonth(2024, 2)).toBe(29); // static still attached
  });

  it('time.timeSpan — factory keeps its statics', () => {
    expect($eq.time.timeSpan.fromHours(25).toString()).toBe('1.01:00:00');
  });

  it('enums.parse — member name to value', () => {
    const map = { active: 'active', pending: 'pending' };
    expect($eq.enums.parse('active', map)).toBe('active');
  });
});
