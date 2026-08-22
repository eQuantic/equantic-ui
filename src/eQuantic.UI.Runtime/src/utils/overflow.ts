/**
 * What C# does at the edge of a fixed-width number, and JavaScript does not.
 *
 * A C# `int` is 32 bits: past 2,147,483,647 it wraps (unchecked, the default) or throws
 * (`checked`). A JavaScript number is a double: it keeps counting. The compiler truncates a
 * wrapped result inline (`| 0`, `& 0xFF`, `BigInt.asIntN(64, …)`) and asks here for the one case
 * that needs a decision at run time — the checked context, where going out of range is an
 * OverflowException.
 */

const RANGES: Record<number, [min: number, max: number]> = {
  8: [-128, 127],
  16: [-32_768, 32_767],
  32: [-2_147_483_648, 2_147_483_647],
};

/** A checked arithmetic result: the value itself, or an overflow thrown exactly where C# throws it. */
export function checked(
  value: number | bigint,
  bits: 8 | 16 | 32 | 64,
  unsigned = false,
): number | bigint {
  if (typeof value === 'bigint') {
    // A BigInt checked into ANY width — 64 for long arithmetic, narrower for a checked cast
    // (`checked((int)aLong)` throws past 2^31-1, exactly where C# does).
    const width = BigInt(bits);
    const ok = unsigned
      ? value >= 0n && value <= 2n ** width - 1n
      : value >= -(2n ** (width - 1n)) && value <= 2n ** (width - 1n) - 1n;
    if (!ok) throw new Error('Arithmetic operation resulted in an overflow.');
    return value;
  }
  if (bits === 64) {
    // A checked NUMBER against the 64-bit edge (`checked((long)aDouble)`). The edges must be the
    // doubles C# compares against: 2^63 and 2^64 are exact, 2^63-1 is not (it rounds UP to 2^63,
    // so `> max` would let the exact edge through) — compare with >= against the power itself.
    const ok = unsigned
      ? value >= 0 && value < 18446744073709551616
      : value >= -9223372036854775808 && value < 9223372036854775808;
    if (!ok || !Number.isFinite(value)) throw new Error('Arithmetic operation resulted in an overflow.');
    return value;
  }
  const [min, max] = RANGES[bits];
  const low = unsigned ? 0 : min;
  const high = unsigned ? max * 2 + 1 : max;
  if (value < low || value > high || !Number.isFinite(value)) {
    throw new Error('Arithmetic operation resulted in an overflow.');
  }
  return value;
}

/**
 * A C# `float` as text: the SHORTEST decimal that reads back as the same single-precision value —
 * `0.1f + 0.2f` prints "0.3", not the 0.30000001192092896 a double would show for the same bits.
 */
export function single(value: number): string {
  if (!Number.isFinite(value)) return String(value).replace('Infinity', '∞');
  value = Math.fround(value);
  if (Object.is(value, -0) || value === 0) return '0';
  for (let digits = 1; digits <= 9; digits++) {
    const candidate = Number(value.toPrecision(digits));
    if (Math.fround(candidate) === value) return String(candidate);
  }
  return String(value);
}

/**
 * Out of range is an ERROR in .NET and a shrug in JavaScript: `"ab".substring(9)` is "" and
 * `xs[9]` is undefined, where the CLR throws. A program that would stop loudly on the server keeps
 * running in the browser with an absent value spreading through it, and surfaces somewhere else
 * entirely — a blank render, a NaN, a page that is subtly wrong rather than plainly broken.
 */
export function substring(value: string, start: number, length?: number): string {
  // The three cases .NET tells apart, because which one it is says where the bug is.
  if (start < 0) throw new RangeError('startIndex cannot be less than zero.');
  if (start > value.length) throw new RangeError('startIndex cannot be larger than length of string.');
  if (length === undefined) return value.slice(start);
  if (length < 0) throw new RangeError('length cannot be less than zero.');
  if (start + length > value.length) {
    throw new RangeError('Index and length must refer to a location within the string.');
  }
  return value.slice(start, start + length);
}

/**
 * A dictionary read: .NET throws for a key that is not there, rather than answering "nothing".
 * The key is whatever the compiler emitted — a string, a number, a char, an enum's member name, a
 * bigint — and a plain object keys by string, so it is stringified the way an index would.
 */
export function dictGet<V>(map: Record<string, V>, key: unknown): V {
  // .NET tells an ABSENT key from a null one, and so does this: a null key is a caller mistake,
  // a missing one is a lookup that found nothing.
  if (key === null || key === undefined) throw new Error('Value cannot be null. (Parameter \'key\')');
  const property = String(key);
  if (!Object.prototype.hasOwnProperty.call(map, property)) {
    throw new Error(`The given key '${property}' was not present in the dictionary.`);
  }
  return map[property];
}
