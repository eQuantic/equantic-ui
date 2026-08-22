import { dec, Decimal } from './decimal';
import { long } from './long';
import { dateTime, timeSpan, dateOnly, timeOnly, dateTimeOffset } from './datetime';

/**
 * TYPED hydration — the boundary where a value from the server (SSR state, a Server Action result)
 * becomes the runtime type the transpiled code computes with.
 *
 * The wire protocol (`EqJson`) sends what JavaScript cannot represent natively as STRINGS: `long`
 * as "9007199254740993", `decimal` as "0.1", dates as ISO-8601. The compiler KNOWS the C# type of
 * every state field and every Server Action's return, so it emits a small spec — this module's
 * input — and the value is coerced ONCE, here, instead of defensively at every use site.
 *
 * A spec says what a value IS:
 *  - a tag (`'long'`, `'decimal'`, `'dateTime'`, …) — a compat scalar, restored by its factory;
 *  - `[spec]` — a list whose every element hydrates by the inner spec;
 *  - `{ dict: spec }` — a dictionary (plain-object twin): keys stay strings, values hydrate;
 *  - a class reference — a record/struct twin: the plain JSON object is rebuilt on the class's
 *    prototype (so `instanceof`, `equals`, `with` survive the wire) and each member hydrates by
 *    the class's own static `$hydration` map.
 *
 * Every branch is idempotent: a value that already has its runtime type passes through, so
 * hydrating twice (or hydrating a value that never crossed the wire) is harmless.
 */

/** The scalar compat types whose wire form is a string. */
export type HydrationTag =
  | 'decimal'
  | 'long'
  | 'dateTime'
  | 'timeSpan'
  | 'dateOnly'
  | 'timeOnly'
  | 'dateTimeOffset';

/** A record/struct twin: a prototype to rebuild on, and its own member specs. */
export interface HydratableConstructor {
  readonly prototype: object;
  readonly $hydration?: Readonly<Record<string, HydrationSpec>>;
}

export type HydrationSpec =
  | HydrationTag
  | readonly [HydrationSpec]
  | { readonly dict: HydrationSpec }
  | HydratableConstructor;

/** The value coerced to what the spec says it is. Null and undefined pass through untouched. */
export function hydrate(incoming: unknown, spec: HydrationSpec): unknown {
  if (incoming == null) return incoming;
  if (typeof spec === 'string') return scalar(incoming, spec);
  if (typeof spec === 'function') return instance(incoming, spec as HydratableConstructor);
  if (Array.isArray(spec)) {
    const inner = (spec as readonly [HydrationSpec])[0];
    return Array.isArray(incoming) ? incoming.map((element) => hydrate(element, inner)) : incoming;
  }
  if ('dict' in (spec as { dict?: HydrationSpec })) {
    const inner = (spec as { dict: HydrationSpec }).dict;
    if (typeof incoming !== 'object' || Array.isArray(incoming)) return incoming;
    const source = incoming as Record<string, unknown>;
    const values: Record<string, unknown> = {};
    for (const key of Object.keys(source)) values[key] = hydrate(source[key], inner);
    return values;
  }
  return incoming;
}

/** A compat scalar from its wire form — pass-through when it already has the runtime type. */
function scalar(incoming: unknown, tag: HydrationTag): unknown {
  switch (tag) {
    case 'decimal':
      return incoming instanceof Decimal
        ? incoming
        : typeof incoming === 'string' || typeof incoming === 'number'
          ? dec(incoming)
          : incoming;
    case 'long':
      return typeof incoming === 'bigint'
        ? incoming
        : typeof incoming === 'string' || typeof incoming === 'number'
          ? long(incoming)
          : incoming;
    case 'dateTime':
      return typeof incoming === 'string' ? dateTime.parse(incoming) : incoming;
    case 'timeSpan':
      return typeof incoming === 'string' ? timeSpan.parse(incoming) : incoming;
    case 'dateOnly':
      return typeof incoming === 'string' ? dateOnly.parse(incoming) : incoming;
    case 'timeOnly':
      return typeof incoming === 'string' ? timeOnly.parse(incoming) : incoming;
    case 'dateTimeOffset':
      return typeof incoming === 'string' ? dateTimeOffset.parse(incoming) : incoming;
  }
}

/**
 * A record/struct twin rebuilt from its plain JSON form: the prototype restored (methods,
 * `instanceof`), every member the class's `$hydration` names coerced, everything else copied
 * verbatim. A value that is already an instance passes through.
 */
function instance(incoming: unknown, ctor: HydratableConstructor): unknown {
  if (incoming instanceof (ctor as unknown as new (...args: never[]) => object)) return incoming;
  if (typeof incoming !== 'object' || Array.isArray(incoming)) return incoming;
  const source = incoming as Record<string, unknown>;
  const rebuilt = Object.create(ctor.prototype) as Record<string, unknown>;
  const members = ctor.$hydration;
  for (const key of Object.keys(source)) {
    const spec = members?.[key];
    rebuilt[key] = spec !== undefined ? hydrate(source[key], spec) : source[key];
  }
  return rebuilt;
}
