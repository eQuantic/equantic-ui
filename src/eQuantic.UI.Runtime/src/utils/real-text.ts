/**
 * A floating-point number's text as .NET writes it with no format string, in the invariant culture
 * the twin always writes in (EQ2110 names what a culture changes).
 *
 * .NET writes the SHORTEST digits that read back as the value, and picks the notation from the
 * decimal exponent of the first of them: fixed from -4 up to 16 for a double and up to 8 for a
 * float, scientific outside that, as `d.dddE+XX` with the exponent signed and at least two digits.
 * JavaScript's `String()` keeps fixed notation up to 1e21 and down to 1e-7, spells the exponent
 * `e+21`, and drops the sign of a negative zero. Measured on .NET 10 at each boundary: 9.9e16
 * prints `99000000000000000` and 1e17 `1E+17`; 990000000f prints in full and 1e9f `1E+09`; 0.0001
 * prints as it is and 1e-5 as `1E-05`; -0.0 prints `-0`; the infinities print `Infinity` and
 * `-Infinity`.
 */
const DOUBLE_FIXED_UP_TO = 16;
const SINGLE_FIXED_UP_TO = 8;
const FIXED_DOWN_TO = -4;

/** A double's `ToString()`. */
export function double(value: number): string {
  return text(value, value, DOUBLE_FIXED_UP_TO);
}

/**
 * A float's `ToString()`: the shortest decimal that reads back as the same SINGLE. `0.1f + 0.2f`
 * is "0.3", where the double underneath would spell 0.30000001192092896.
 */
export function single(value: number): string {
  const stored = Math.fround(value);
  let shortest = stored;
  if (Number.isFinite(stored) && stored !== 0) {
    for (let digits = 1; digits <= 9; digits++) {
      const candidate = Number(stored.toPrecision(digits));
      if (Math.fround(candidate) === stored) {
        shortest = candidate;
        break;
      }
    }
  }
  return text(stored, shortest, SINGLE_FIXED_UP_TO);
}

/** The text of `value`, whose digits are the shortest of `digitsOf`, in fixed notation up to `fixedUpTo`. */
function text(value: number, digitsOf: number, fixedUpTo: number): string {
  if (Number.isNaN(value)) return 'NaN';
  if (value === Infinity) return 'Infinity';
  if (value === -Infinity) return '-Infinity';
  const sign = value < 0 || Object.is(value, -0) ? '-' : '';
  if (value === 0) return sign + '0';
  const { digits, exponent } = shortestDigits(Math.abs(digitsOf));
  if (exponent >= FIXED_DOWN_TO && exponent <= fixedUpTo) return sign + fixed(digits, exponent);
  const mantissa = digits.length === 1 ? digits : `${digits[0]}.${digits.slice(1)}`;
  const magnitude = String(Math.abs(exponent)).padStart(2, '0');
  return `${sign}${mantissa}E${exponent < 0 ? '-' : '+'}${magnitude}`;
}

/**
 * The significant digits of a positive number and the decimal exponent of the first, read from
 * JavaScript's own text of it, which is the shortest that round-trips, and of those the closest,
 * as .NET's is. Only the notation differs, so the digits are taken and the notation is .NET's.
 */
function shortestDigits(positive: number): { digits: string; exponent: number } {
  const [mantissa, power] = String(positive).split('e');
  const dot = mantissa.indexOf('.');
  const all = dot < 0 ? mantissa : mantissa.slice(0, dot) + mantissa.slice(dot + 1);
  const leading = all.length - all.replace(/^0+/, '').length;
  const point = (dot < 0 ? mantissa.length : dot) - leading;
  return {
    digits: all.slice(leading).replace(/0+$/, ''),
    exponent: point - 1 + (power === undefined ? 0 : Number(power)),
  };
}

/** `digits` in fixed notation, with the first at decimal `exponent`. */
function fixed(digits: string, exponent: number): string {
  if (exponent < 0) return `0.${'0'.repeat(-exponent - 1)}${digits}`;
  const whole = exponent + 1;
  if (digits.length <= whole) return digits + '0'.repeat(whole - digits.length);
  return `${digits.slice(0, whole)}.${digits.slice(whole)}`;
}
