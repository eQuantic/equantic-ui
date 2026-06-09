/**
 * Structural (value) equality for .NET value-shaped data: records, `record struct`/`struct`, value
 * tuples, and anonymous types — all represented as plain JS objects / arrays by the transpiler.
 *
 * .NET semantics modelled:
 *  - records & structs compare by VALUE (each member equal), not by reference;
 *  - value tuples `(a, b)` compare element-wise;
 *  - the compat types (Decimal, DateTime, TimeSpan, …) carry their own `.equals`, so we delegate to
 *    it rather than walking their internals.
 *
 * This backs `==`/`!=` on those types and the value-based `Contains`/`Distinct`. C# only allows `==`
 * between the SAME record/struct type (mixing types doesn't compile), and LINQ collections are
 * homogeneous, so a field-by-field walk is faithful without a runtime type tag.
 */
export function equals(a: unknown, b: unknown): boolean {
  // Identical reference, or equal primitives (number/string/boolean/bigint/symbol).
  if (a === b) return true;

  // Nullish: equal only if both are null/undefined (a === b already caught both-undefined etc.).
  if (a == null || b == null) return a == null && b == null;

  const ta = typeof a;
  if (ta !== typeof b) return false;

  // Non-objects that aren't `===` are not equal (e.g. 1 vs 2, "a" vs "b", 1n vs 2n).
  if (ta !== 'object') return false;

  // Compat value types expose their own structural equality (Decimal, DateTime, TimeSpan, DateOnly,
  // TimeOnly, DateTimeOffset). Delegate to it.
  const aEq = (a as { equals?: unknown }).equals;
  if (typeof aEq === 'function') return (aEq as (o: unknown) => boolean).call(a, b);

  // Value tuples are arrays — compare element-wise.
  const aArr = Array.isArray(a);
  const bArr = Array.isArray(b);
  if (aArr || bArr) {
    if (!aArr || !bArr) return false;
    if (a.length !== b.length) return false;
    for (let i = 0; i < a.length; i++) {
      if (!equals(a[i], b[i])) return false;
    }
    return true;
  }

  // Plain objects (records / structs / anonymous types): same own keys, each member equal.
  const ao = a as Record<string, unknown>;
  const bo = b as Record<string, unknown>;
  const ak = Object.keys(ao);
  const bk = Object.keys(bo);
  if (ak.length !== bk.length) return false;
  for (const k of ak) {
    if (!Object.prototype.hasOwnProperty.call(bo, k)) return false;
    if (!equals(ao[k], bo[k])) return false;
  }
  return true;
}
