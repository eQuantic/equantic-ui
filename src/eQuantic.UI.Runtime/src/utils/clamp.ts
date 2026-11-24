/**
 * `Math.Clamp` and its siblings over a number the runtime holds as a JavaScript number or a BigInt, as
 * .NET clamps them (#1824): a minimum above the maximum is refused with .NET's `ArgumentException`, its
 * two values written in the current culture as their type writes them (`'3,5' cannot be greater than
 * 1,25.` in pt-BR, and a float's `0.1`, not its double's digits); then the minimum where the value is
 * below it, the maximum where it is above, and the value itself otherwise. That order is .NET's, and it
 * is not `Math.min(Math.max(value, min), max)`: a NaN bound leaves the answer a number
 * (`Clamp(2, NaN, 1)` is 1), and a negative zero keeps its sign (`Clamp(-0.0, 0.0, 1.0)` is -0).
 *
 * A decimal clamps in `decimal-math.ts`. Both refusals write their values through the formatter, which
 * is why neither lives beside the arithmetic the formatter reads.
 */
import { exception } from './exceptions';
import { format, type NumberKind } from './format';

/** A number's clamp; `kind` is its .NET type, which the refusal writes it as. */
export function clamp(value: number, min: number, max: number, kind: NumberKind): number {
  if (min > max) throw refusal(min, max, kind);
  if (value < min) return min;
  if (value > max) return max;
  return value;
}

/** A long's, a ulong's or a native integer's clamp: a BigInt writes itself. */
export function clampLong(value: bigint, min: bigint, max: bigint): bigint {
  if (min > max) throw refusal(min, max);
  if (value < min) return min;
  if (value > max) return max;
  return value;
}

function refusal(min: unknown, max: unknown, kind?: NumberKind): Error {
  const text = (bound: unknown) => format(bound, null, undefined, undefined, kind);
  return exception(
    'System.ArgumentException',
    `'${text(min)}' cannot be greater than ${text(max)}.`,
  );
}
