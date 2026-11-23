/**
 * `Math.Clamp` and `decimal.Clamp` over a decimal, as .NET's decimal clamps (#1767): a minimum above the
 * maximum is refused with .NET's `ArgumentException`, its two values written in the current culture as
 * `string.Format` writes them; then the minimum where the value is below it, the maximum where it is
 * above, and the value itself otherwise, its own scale kept (`Clamp(1.0m, 1.00m, 3m)` is 1.0).
 *
 * A module of its own because the refusal writes a decimal in the culture: the formatter reads the
 * Decimal, so the Decimal cannot read the formatter.
 */
import { Decimal } from './decimal';
import { exception } from './exceptions';
import { format } from './format';

export function clamp(value: Decimal, min: Decimal, max: Decimal): Decimal {
  if (min.compareTo(max) > 0) {
    throw exception('System.ArgumentException', `'${format(min, null)}' cannot be greater than ${format(max, null)}.`);
  }
  if (value.compareTo(min) < 0) return min;
  if (value.compareTo(max) > 0) return max;
  return value;
}
