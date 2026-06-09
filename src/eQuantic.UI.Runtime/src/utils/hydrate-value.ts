import { Decimal, dec } from './decimal';
import { long } from './long';
import { DateTime, dateTime, TimeSpan, timeSpan } from './datetime';

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

  return incoming;
}
