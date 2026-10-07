import { StatefulComponent, StatelessComponent } from '../core/component';
import { Component } from '../core/types';
import { adoptMember } from '../utils/adopt-member';
import { DateOnly, DateTime, DateTimeOffset, TimeOnly, TimeSpan } from '../utils/datetime';
import { Decimal } from '../utils/decimal';
import { Dictionary } from '../utils/dictionary';
import { hydrateValue } from '../utils/hydrate-value';
import { SortedMap } from '../utils/sorted';

/**
 * What a hot reload carries across for a page: the fields its C# declares. A write-once page keeps
 * them on itself, as the class fields its twin declares (`_count = 0;`), and the reload read a
 * `_state` bag such a page does not have, so it carried nothing and every counter went back to its
 * initializer (#664).
 *
 * The runtime's own fields live on the same object, set by the base classes' constructors before the
 * page's: the nearest base class of the runtime, built bare, says which they are, with no list to keep
 * in step with it.
 */
const runtimeBases: ReadonlyArray<abstract new (...args: never[]) => object> = [
  StatefulComponent,
  StatelessComponent,
  Component,
];

function runtimeKeys(page: object): Set<string> {
  for (let prototype = Object.getPrototypeOf(page); prototype; prototype = Object.getPrototypeOf(prototype)) {
    const base = runtimeBases.find((candidate) => candidate.prototype === prototype);
    if (base) return new Set(Object.keys(Reflect.construct(base, [])));
  }
  return new Set();
}

/** The value types the runtime holds as classes and `hydrateValue` rebuilds from their text. */
const runtimeValues: ReadonlyArray<abstract new (...args: never[]) => object> = [
  Decimal,
  DateTime,
  DateTimeOffset,
  DateOnly,
  TimeOnly,
  TimeSpan,
  Dictionary,
  SortedMap,
];

/**
 * Whether a value is DATA, which JSON carries and the reloaded page's initializer rebuilds as it was: a
 * primitive, an array or a plain object of data, a record or a struct (whose twin has `with` and
 * `equals`) of data, or one of the runtime's value types. Anything else is an object with a life of
 * its own, a controller or a service, whose JSON is not it: rebuilt from that, its maps came back as
 * objects no map method accepts and its callbacks as nothing, so it keeps its initializer instead.
 */
function isData(value: unknown, depth = 0): boolean {
  if (depth > 64) return false;
  if (value === null || value === undefined) return true;
  switch (typeof value) {
    case 'string':
    case 'number':
    case 'boolean':
    case 'bigint':
      return true;
    case 'object':
      break;
    default:
      return false;
  }
  if (Array.isArray(value)) return value.every((item) => isData(item, depth + 1));
  if (runtimeValues.some((type) => value instanceof type)) return true;
  const prototype = Object.getPrototypeOf(value) as { with?: unknown; equals?: unknown } | null;
  const plain = prototype === Object.prototype || prototype === null;
  const record = !plain && typeof prototype?.with === 'function' && typeof prototype.equals === 'function';
  return (plain || record) && Object.values(value as object).every((member) => isData(member, depth + 1));
}

/** A `long` is a BigInt here, which JSON refuses: it crosses as its digits, which `hydrateValue` reads back. */
const wire = (_: string, value: unknown): unknown => (typeof value === 'bigint' ? value.toString() : value);

/**
 * The page's fields that are data, each already in the form JSON carries across the reload. One that
 * cannot cross (a cycle) is left behind alone, and the others still cross.
 */
export function capturePageState(page: object): Record<string, unknown> {
  const skip = runtimeKeys(page);
  const state: Record<string, unknown> = {};
  for (const key of Object.keys(page)) {
    if (skip.has(key)) continue;
    const value = (page as Record<string, unknown>)[key];
    if (value === undefined || !isData(value)) continue;
    try {
      state[key] = JSON.parse(JSON.stringify(value, wire));
    } catch {
      continue;
    }
  }
  return state;
}

/**
 * Hands the reloaded page the fields it held, each rebuilt in the shape its own initializer gives it
 * (`hydrateValue`), before it builds. Only a field the new page declares too: one the edit removed or
 * renamed is left behind, and one it added keeps its initializer.
 */
export function restorePageState(page: object, saved: Record<string, unknown>): void {
  const skip = runtimeKeys(page);
  for (const key of Object.keys(saved)) {
    if (skip.has(key) || !Object.prototype.hasOwnProperty.call(page, key)) continue;
    adoptMember(page, key, hydrateValue((page as Record<string, unknown>)[key], saved[key]));
  }
}
