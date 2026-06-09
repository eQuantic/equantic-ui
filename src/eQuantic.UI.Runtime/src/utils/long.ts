/**
 * .NET-compat `long` / `ulong` (Int64) — exact 64-bit integers via JavaScript BigInt.
 *
 * Plain JS numbers are IEEE-754 doubles and lose integer precision beyond 2^53, so `long` values
 * larger than that would be corrupted. This maps `long` to BigInt. The transpiler emits BigInt
 * literals (`5n`) and wraps `long` operands in `long(...)` (a pass-through for BigInt, coercion for
 * the number/string forms a value may take when it crosses JSON).
 *
 * End-to-end protocol: `long` crosses the wire (SSR state, Server Action payloads) as a STRING — the
 * server uses a string JsonConverter for Int64 — so precision survives JSON; the client coerces the
 * string back to BigInt via `long(...)`. Installing BigInt.prototype.toJSON keeps SENDING BigInt
 * JSON-safe (it would otherwise throw), serializing it as a string the server reads back.
 */

// Make BigInt JSON-serializable (as a string) — JSON.stringify throws on BigInt by default.
const bigIntProto = BigInt.prototype as unknown as { toJSON?: () => string };
if (typeof bigIntProto.toJSON !== 'function') {
  bigIntProto.toJSON = function (this: bigint): string {
    return this.toString();
  };
}

/** Coerces a value to a BigInt: pass-through for BigInt, parse for string, truncate for number. */
export function long(value: bigint | number | string): bigint {
  if (typeof value === 'bigint') return value;
  if (typeof value === 'string') return BigInt(value.trim());
  return BigInt(Math.trunc(value));
}
