/**
 * .NET-compat math helpers.
 *
 * Part of the eQuantic.UI .NET-compatibility runtime: faithful implementations of .NET numeric
 * semantics that JavaScript lacks natively. The transpiler emits calls to these where C# behavior
 * differs from the JS built-ins.
 */

/**
 * Rounds like .NET's `Math.Round` — banker's rounding (MidpointRounding.ToEven) — instead of
 * JavaScript's `Math.round` (which rounds halves toward +Infinity).
 *
 * Examples (matching .NET): round(2.5) === 2, round(3.5) === 4, round(-2.5) === -2,
 * round(2.345, 2) === 2.34.
 *
 * Note: this operates on IEEE-754 doubles, so midpoints that aren't exactly representable
 * (e.g. 2.675) follow the double's actual value, which can differ from .NET's decimal-aware
 * rounding. Exact decimal rounding belongs to the Decimal compat type.
 */
export function round(value: number, digits = 0): number {
  if (!Number.isFinite(value)) return value;

  const factor = 10 ** digits;
  const scaled = value * factor;
  return roundHalfToEven(scaled) / factor;
}

function roundHalfToEven(x: number): number {
  const floor = Math.floor(x);
  const fraction = x - floor;
  const EPSILON = 1e-9;

  // Exact midpoint → round to the even neighbour.
  if (Math.abs(fraction - 0.5) < EPSILON) {
    return floor % 2 === 0 ? floor : floor + 1;
  }

  // Otherwise normal rounding.
  return Math.round(x);
}
