import { Decimal, dec } from './decimal';
import { long } from './long';
import { DateTime, dateTime, TimeSpan, timeSpan, DateOnly, dateOnly, TimeOnly, timeOnly, DateTimeOffset, dateTimeOffset } from './datetime';

/**
 * Coerce a value arriving from SSR state (or any server payload) so it matches the runtime type of
 * the field it is being assigned to.
 *
 * Values that JavaScript cannot represent natively cross the wire as strings (the `EqJson` protocol:
 * `decimal` -> string, `long`/`ulong` -> string). The transpiled state class initializes such fields
 * with the compat type as their default (`dec("0")`, `0n`), so the *existing* value tells us what the
 * incoming value should become. This keeps `decimal`/`long` fields exact after hydration without the
 * runtime needing any compile-time type metadata — and never touches genuine string/number fields,
 * whose defaults are plain strings/numbers.
 *
 * @param current  the field's current value (its default, set by the state constructor)
 * @param incoming the value parsed from the SSR/server JSON
 */
export function hydrateValue(current: unknown, incoming: unknown): unknown {
  if (incoming == null) return incoming;

  // decimal field: restore the exact base-10 Decimal from its string (or number) representation.
  if (current instanceof Decimal && (typeof incoming === 'string' || typeof incoming === 'number')) {
    return dec(incoming);
  }

  // A NUMBER field fed a numeric string: EqJson writes Int64 as a string (so values beyond 2^53
  // survive the wire), but a `long` the transpiler typed as `number` must not end up holding a
  // string — later arithmetic would silently concatenate.
  if (typeof current === 'number' && typeof incoming === 'string') {
    const parsed = Number(incoming);
    return Number.isFinite(parsed) ? parsed : current;
  }

  // long/ulong field: restore the BigInt from its string (or number) representation.
  if (typeof current === 'bigint' && (typeof incoming === 'string' || typeof incoming === 'number')) {
    return long(incoming);
  }

  // DateTime field: parse the ISO-8601 string the server emitted back into the compat type.
  if (current instanceof DateTime && typeof incoming === 'string') {
    return dateTime.parse(incoming);
  }

  // TimeSpan field: parse the .NET "c" string ([-][d.]hh:mm:ss[.fffffff]) back into the compat type.
  if (current instanceof TimeSpan && typeof incoming === 'string') {
    return timeSpan.parse(incoming);
  }

  // DateOnly / TimeOnly: parse the ISO string the server emitted back into the compat type.
  if (current instanceof DateOnly && typeof incoming === 'string') {
    return dateOnly.parse(incoming);
  }
  if (current instanceof TimeOnly && typeof incoming === 'string') {
    return timeOnly.parse(incoming);
  }
  if (current instanceof DateTimeOffset && typeof incoming === 'string') {
    return dateTimeOffset.parse(incoming);
  }

  // Record/struct field: SSR sends the value as a plain JSON object, which loses the class prototype
  // (so its instance methods and `instanceof` would be gone). Rebuild it on the right prototype, taken
  // from the field's default (a record instance), and hydrate each member recursively using the
  // default's corresponding member as the witness — so nested records and compat-typed members (a
  // Decimal/DateTime inside the record) are restored too. Plain objects (Dictionary/anonymous) keep
  // the `Object.prototype` and are left untouched; arrays are handled by their element witnesses.
  if (
    typeof current === 'object' &&
    current !== null &&
    !Array.isArray(current) &&
    typeof incoming === 'object' &&
    !Array.isArray(incoming)
  ) {
    const proto = Object.getPrototypeOf(current);
    if (proto && proto !== Object.prototype) {
      const rebuilt = Object.create(proto) as Record<string, unknown>;
      const witness = current as Record<string, unknown>;
      for (const key of Object.keys(incoming as Record<string, unknown>)) {
        rebuilt[key] = hydrateValue(witness[key], (incoming as Record<string, unknown>)[key]);
      }
      return rebuilt;
    }
  }

  return incoming;
}
