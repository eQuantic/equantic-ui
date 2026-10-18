import { exception } from './exceptions';

/**
 * Lifted-operator helpers for .NET `Nullable<T>` (`T?`), and its unwrap.
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

/**
 * Lifted unary operator (`++`, `--`, `-`, `~`, `+` on a `T?`): `null` if the operand is
 * null/undefined, otherwise `fn(v)`. JavaScript's own operators read null as 0: `-null` is -0,
 * `~null` is -1 and `null + 1` is 1, where C# answers null.
 */
export function liftUnary<T, O>(v: T | null | undefined, fn: (a: T) => O): O | null {
  return v == null ? null : fn(v);
}

/** Lifted relational comparison: `false` if either operand is null/undefined, otherwise `pred(l, r)`. */
export function liftCmp<L, R>(
  l: L | null | undefined,
  r: R | null | undefined,
  pred: (a: L, b: R) => boolean,
): boolean {
  return l == null || r == null ? false : pred(l, r);
}

/** What .NET's InvalidOperationException says of the value of an empty `Nullable<T>`. */
const NO_VALUE = 'Nullable object must have a value.';

/**
 * `Nullable<T>.Value`, and every unwrap C# applies through it: a cast of a `T?` to `T` or to another
 * type, the value a struct's own conversion operator takes, a `foreach` element of a type that is not
 * nullable. An empty one throws the InvalidOperationException .NET throws, before anything converts
 * it; JavaScript carried the null on, and a struct's operator built a value holding it (#967).
 */
export function unwrap<T>(v: T | null | undefined): T {
  if (v == null) throw exception('System.InvalidOperationException', NO_VALUE);
  return v;
}
