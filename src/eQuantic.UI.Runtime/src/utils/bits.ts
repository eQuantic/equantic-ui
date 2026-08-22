/**
 * .NET-compat bit operations (.NET 7+ `IBinaryInteger` surface) that JavaScript lacks natively.
 *
 * The 32-bit family works on plain numbers with the int32 operators; the 64-bit family works on
 * the BigInt-backed `long`, normalizing through `BigInt.asUintN(64, …)` so negative values count
 * and rotate by their two's-complement bits — the same bits .NET sees. Counts and logs of a LONG
 * return a BigInt because the C# methods return `long` (`T.PopCount` answers in T).
 */

/** The number of set bits in a 32-bit integer (.NET int.PopCount) — the classic SWAR ladder. */
export function popCount32(x: number): number {
  x = x - ((x >> 1) & 0x55555555);
  x = (x & 0x33333333) + ((x >> 2) & 0x33333333);
  x = (x + (x >> 4)) & 0x0f0f0f0f;
  return Math.imul(x, 0x01010101) >>> 24;
}

/** The number of set bits in a long's 64 bits (.NET long.PopCount). */
export function popCount64(x: bigint): bigint {
  let value = BigInt.asUintN(64, x);
  let count = 0n;
  while (value !== 0n) {
    value &= value - 1n;
    count++;
  }
  return count;
}

/** The long rotated LEFT by n bits (.NET long.RotateLeft); the count wraps at 64. */
export function rotateLeft64(x: bigint, n: number): bigint {
  const k = BigInt(n & 63);
  const bits = BigInt.asUintN(64, x);
  return BigInt.asIntN(64, (bits << k) | (bits >> (64n - k)));
}

/** The long rotated RIGHT by n bits (.NET long.RotateRight); the count wraps at 64. */
export function rotateRight64(x: bigint, n: number): bigint {
  const k = BigInt(n & 63);
  const bits = BigInt.asUintN(64, x);
  return BigInt.asIntN(64, (bits >> k) | (bits << (64n - k)));
}

/** Leading zero bits of a long's 64 (.NET long.LeadingZeroCount): 64 for zero. */
export function leadingZeroCount64(x: bigint): bigint {
  const bits = BigInt.asUintN(64, x);
  return bits === 0n ? 64n : 64n - BigInt(bits.toString(2).length);
}

/** Trailing zero bits of a long's 64 (.NET long.TrailingZeroCount): 64 for zero. */
export function trailingZeroCount64(x: bigint): bigint {
  const bits = BigInt.asUintN(64, x);
  if (bits === 0n) return 64n;
  const lowest = bits & -bits;
  return BigInt(lowest.toString(2).length - 1);
}

/** Floor of log2 of a long (.NET long.Log2); zero answers 0, as .NET defines it. */
export function log2Of64(x: bigint): bigint {
  if (x <= 0n) return 0n;
  return BigInt(x.toString(2).length - 1);
}
