import { describe, expect, it } from 'vitest';
import {
  popCount32,
  popCount64,
  rotateLeft64,
  rotateRight64,
  leadingZeroCount64,
  trailingZeroCount64,
  log2Of64,
} from './bits';

describe('32-bit population count', () => {
  it('counts set bits, all-bits for -1', () => {
    expect(popCount32(0)).toBe(0);
    expect(popCount32(255)).toBe(8);
    expect(popCount32(-1)).toBe(32);
    expect(popCount32(0x80000000 | 0)).toBe(1);
  });
});

describe('64-bit bit surface (BigInt long)', () => {
  it('counts population over the two’s-complement bits', () => {
    expect(popCount64(0n)).toBe(0n);
    expect(popCount64(255n)).toBe(8n);
    expect(popCount64(-1n)).toBe(64n);
    expect(popCount64(-9223372036854775808n)).toBe(1n); // long.MinValue = the sign bit alone
  });

  it('rotates through the sign bit and wraps the count at 64', () => {
    expect(rotateLeft64(1n, 63)).toBe(-9223372036854775808n);
    expect(rotateLeft64(1n, 64)).toBe(1n);
    expect(rotateLeft64(1n, 0)).toBe(1n);
    expect(rotateRight64(1n, 1)).toBe(-9223372036854775808n);
    expect(rotateRight64(-9223372036854775808n, 63)).toBe(1n);
  });

  it('counts leading and trailing zeros, 64 for zero', () => {
    expect(leadingZeroCount64(1n)).toBe(63n);
    expect(leadingZeroCount64(0n)).toBe(64n);
    expect(leadingZeroCount64(-1n)).toBe(0n);
    expect(trailingZeroCount64(4294967296n)).toBe(32n);
    expect(trailingZeroCount64(0n)).toBe(64n);
    expect(trailingZeroCount64(-9223372036854775808n)).toBe(63n);
  });

  it('takes the floor log2, 0 for zero as .NET defines it', () => {
    expect(log2Of64(4294967296n)).toBe(32n);
    expect(log2Of64(1n)).toBe(0n);
    expect(log2Of64(0n)).toBe(0n);
  });
});
