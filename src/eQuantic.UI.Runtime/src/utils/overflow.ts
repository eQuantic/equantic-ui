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
    const ok = unsigned
      ? value >= 0n && value <= 2n ** 64n - 1n
      : value >= -(2n ** 63n) && value <= 2n ** 63n - 1n;
    if (!ok) throw new Error('Arithmetic operation resulted in an overflow.');
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
