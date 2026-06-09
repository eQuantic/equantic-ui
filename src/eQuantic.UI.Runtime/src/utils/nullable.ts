/**
 * Lifted-operator helpers for .NET `Nullable<T>` (`T?`).
 *
 * A lifted binary operator in C# evaluates BOTH operands, then:
 *  - arithmetic (`+ - * / %`)  → `null` if either operand is null;
 *  - relational (`< > <= >=`)  → **false** if either operand is null. This is NOT a value
 *    comparison: naive JS coerces `null` to `0` (so `null < 5` is `true`), which diverges from .NET
 *    (`(int?)null < 5` is `false`). These helpers mirror .NET exactly.
 *
 * Operands are passed by value, so each is evaluated exactly once (matching C#, which evaluates both
 * sides of a lifted operator before testing for null) — no double-evaluation of the source expression.
 */

/** Lifted arithmetic: `null` if either operand is null/undefined, otherwise `fn(l, r)`. */
export function liftArith<L, R, O>(
  l: L | null | undefined,
  r: R | null | undefined,
  fn: (a: L, b: R) => O,
): O | null {
  return l == null || r == null ? null : fn(l, r);
}

/** Lifted relational comparison: `false` if either operand is null/undefined, otherwise `pred(l, r)`. */
export function liftCmp<L, R>(
  l: L | null | undefined,
  r: R | null | undefined,
  pred: (a: L, b: R) => boolean,
): boolean {
  return l == null || r == null ? false : pred(l, r);
}
