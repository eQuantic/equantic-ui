import { StatefulComponent, StatelessComponent } from '../core/component';
import { Component } from '../core/types';
import { adoptMember, declaresMember } from '../utils/adopt-member';
import { Dictionary } from '../utils/dictionary';
import { valueTwinNamed, valueTwinOf } from '../utils/hash';
import {
  hydrate,
  scalarTagOf,
  type HydratableConstructor,
  type HydrationKey,
  type HydrationSpec,
  type HydrationTag,
} from '../utils/hydrate';

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
  for (
    let prototype = Object.getPrototypeOf(page);
    prototype;
    prototype = Object.getPrototypeOf(prototype)
  ) {
    const base = runtimeBases.find((candidate) => candidate.prototype === prototype);
    if (base) return new Set(Object.keys(Reflect.construct(base, [])));
  }
  return new Set();
}

/**
 * How a carried value is rebuilt: the hydration spec of its runtime type, in the language `hydrate`
 * reads and the compiler writes for the server's payload (`utils/hydrate.ts`), with one difference. No
 * class crosses a document reload, so a class is written by its NAME: `twin` names a vocabulary value
 * type, which the runtime registers and which is the same class after the reload (`valueTwinNamed`),
 * and `record` names a record or a struct of the app's, whose class only the reloaded page's own
 * initializer can hand back.
 */
export type CarriedSpec =
  | HydrationTag
  | readonly [CarriedSpec]
  | { readonly tuple: readonly (CarriedSpec | null)[] }
  | {
      readonly dict: CarriedSpec | null;
      readonly key?: HydrationKey;
      readonly byValue?: true | 'own';
    }
  | {
      readonly members: Readonly<Record<string, CarriedSpec>>;
      readonly twin?: string;
      readonly record?: string;
    };

/**
 * What a hot reload carries for one page: each field as JSON writes it, the form the server's payload
 * has, and the spec of each field whose JSON is not yet the value it was.
 */
export interface PageState {
  readonly fields: Record<string, unknown>;
  readonly specs: Record<string, CarriedSpec>;
}

/** A value the reload cannot give back as it was: see {@link carry}. */
const NOT_DATA = Symbol('not data');

/** A record or a struct the compiler emitted: its twin has `with` and `equals`. */
function isRecord(prototype: object | null): boolean {
  const twin = prototype as { with?: unknown; equals?: unknown } | null;
  return typeof twin?.with === 'function' && typeof twin.equals === 'function';
}

/**
 * THE RULE, the one that decides at every depth what crosses and how it comes back: the spec a value
 * is rebuilt by from its JSON (null when its JSON is already it), or NOT_DATA when the reload cannot
 * give it back as it was, and the field that holds it keeps its initializer.
 *
 * - A string, a bool and a null; a number JSON writes as itself, a finite one: NaN and the infinities
 *   are written as null.
 * - A `long`, a `decimal` and the dates, by their tag.
 * - An array of data, element by element. One that holds anything but its elements (a pair's `key`
 *   and `value`) is not, since JSON writes the elements alone.
 * - A dictionary of data whose keys share one type a key spec revives, found as it found them.
 * - A plain object (an anonymous type, a colour) of data, member by member.
 * - A vocabulary value type (`Point`, `EdgeInsets`, `ColorToken`) of data, member by member, wherever
 *   it is: the runtime that registers it is the runtime after the reload.
 * - A record or a struct of the app's, member by member, only where the reloaded page's initializer can
 *   hold one to take its class from (`placed`): a field, or a member of one. Its class is the app's
 *   own module's, which the reload evaluates again, and nothing but that initializer names it, so one
 *   inside an array or a dictionary cannot come back.
 *
 * Anything else keeps its initializer: a controller or a service, whose JSON is not it (its maps came
 * back as objects no map method accepts, its callbacks as nothing); a sorted dictionary, whose order is
 * a comparer the capture cannot name; and the other collections the runtime holds as classes of their
 * own, a set or a queue, which this does not carry.
 */
function carry(
  value: unknown,
  placed: boolean,
  depth: number,
): CarriedSpec | null | typeof NOT_DATA {
  if (value === null || value === undefined) return null;
  const tag = scalarTagOf(value);
  if (tag !== undefined) return tag;
  switch (typeof value) {
    case 'string':
    case 'boolean':
      return null;
    case 'number':
      return Number.isFinite(value) ? null : NOT_DATA;
    case 'object':
      break;
    default:
      return NOT_DATA;
  }
  // A cycle, or a depth no page's data reaches.
  if (depth > 64) return NOT_DATA;
  if (Array.isArray(value)) return list(value, depth);
  if (value instanceof Dictionary) return dictionary(value, depth);
  if (isPlain(value)) return members(value, {}, depth);
  const prototype = Object.getPrototypeOf(value) as object | null;
  const twin = valueTwinOf(value);
  if (twin !== undefined) return members(jsonOf(value), { twin: twin.name }, depth);
  if (placed && isRecord(prototype)) {
    return members(
      jsonOf(value),
      { record: (prototype as { constructor: { name: string } }).constructor.name },
      depth,
    );
  }
  return NOT_DATA;
}

/** An array, by one spec for every element when one fits them all, and position by position otherwise. */
function list(items: unknown[], depth: number): CarriedSpec | null | typeof NOT_DATA {
  const specs: (CarriedSpec | null)[] = [];
  let present = 0;
  for (let at = 0; at < items.length; at++) {
    if (at in items) present++;
    const spec = carry(items[at], false, depth + 1);
    if (spec === NOT_DATA) return NOT_DATA;
    specs.push(spec);
  }
  if (Object.keys(items).length !== present) return NOT_DATA;
  const one = oneSpec(items, specs);
  if (one === null) return null;
  return one === undefined ? { tuple: specs } : [one];
}

/**
 * A dictionary, by `hydrate`'s own dictionary spec, which is how the runtime revives one whatever its
 * JSON is: its keys by one key spec, its values by one value spec, found as it found them.
 */
function dictionary(
  map: Dictionary<unknown, unknown>,
  depth: number,
): CarriedSpec | typeof NOT_DATA {
  // A comparison the compiler generated for a tuple's keys is a function, which no JSON holds.
  const equality = map.equality;
  if (typeof equality === 'function') return NOT_DATA;
  let key: HydrationKey | null | undefined;
  for (const item of map.keys()) {
    const spec = keySpec(item);
    if (spec === NOT_DATA || (key !== undefined && spec !== key)) return NOT_DATA;
    key = spec;
  }
  const values = map.values();
  const specs: (CarriedSpec | null)[] = [];
  for (const item of values) {
    const spec = carry(item, false, depth + 1);
    if (spec === NOT_DATA) return NOT_DATA;
    specs.push(spec);
  }
  const one = oneSpec(values, specs);
  if (one === undefined) return NOT_DATA;
  return {
    dict: one,
    ...(key ? { key } : {}),
    ...(equality ? { byValue: equality } : {}),
  };
}

/**
 * How a dictionary's key is revived ({@link HydrationKey}): none for a string, whose wire form is
 * itself, a number, a bool or a compat scalar by its tag, and NOT_DATA for anything else.
 */
function keySpec(key: unknown): HydrationKey | null | typeof NOT_DATA {
  switch (typeof key) {
    case 'string':
      return null;
    case 'number':
      return 'number';
    case 'boolean':
      return 'bool';
    default:
      return scalarTagOf(key) ?? NOT_DATA;
  }
}

/**
 * The one spec every item hydrates by, null when none needs one, and undefined when no single spec
 * fits them all. A null item fits any, since `hydrate` hands a null back as it is; any other item whose
 * JSON is already it fits only none, which a spec for the others would not leave alone.
 */
function oneSpec(
  items: readonly unknown[],
  specs: readonly (CarriedSpec | null)[],
): CarriedSpec | null | undefined {
  let one: CarriedSpec | null = null;
  let text: string | undefined;
  for (let at = 0; at < items.length; at++) {
    if (items[at] === null || items[at] === undefined) continue;
    const written = JSON.stringify(specs[at]);
    if (text === undefined) {
      one = specs[at];
      text = written;
    } else if (written !== text) {
      return undefined;
    }
  }
  return one;
}

/** A plain object: an anonymous type's value, a colour, a class's members as JSON writes them. */
function isPlain(value: unknown): value is Record<string, unknown> {
  if (value === null || typeof value !== 'object') return false;
  const prototype = Object.getPrototypeOf(value) as object | null;
  return prototype === Object.prototype || prototype === null;
}

/**
 * The members JSON writes for an instance of a class: its own, or the plain object its `toJSON`
 * answers (a twin that keeps a property's store), and undefined when that answer is anything else.
 */
function jsonOf(value: object): object | undefined {
  const own = (value as { toJSON?: unknown }).toJSON;
  if (typeof own !== 'function') return value;
  const json: unknown = (own as () => unknown).call(value);
  return isPlain(json) ? json : undefined;
}

/**
 * An object member by member: a plain object's spec names only the members that need one, and is none
 * when no member does; a class's spec always names its class, to be rebuilt on its prototype.
 */
function members(
  view: object | undefined,
  owner: { readonly twin?: string; readonly record?: string },
  depth: number,
): CarriedSpec | null | typeof NOT_DATA {
  if (view === undefined) return NOT_DATA;
  const source = view as Record<string, unknown>;
  const specs: Record<string, CarriedSpec> = {};
  let named = false;
  for (const name of Object.keys(source)) {
    const spec = carry(source[name], true, depth + 1);
    if (spec === NOT_DATA) return NOT_DATA;
    if (spec === null) continue;
    adoptMember(specs, name, spec);
    named = true;
  }
  if (owner.twin === undefined && owner.record === undefined)
    return named ? { members: specs } : null;
  return { members: specs, ...owner };
}

/** A `long` is a BigInt here, which JSON refuses: it crosses as its digits, which its tag reads back. */
const wire = (_: string, value: unknown): unknown =>
  typeof value === 'bigint' ? value.toString() : value;

/**
 * The page's fields that are data, each in the form JSON writes it, with the spec of each one whose
 * JSON is not yet the value it was. One that cannot cross (a controller, a cycle, a NaN) is left
 * behind alone, and the others still cross.
 */
export function capturePageState(page: object): PageState {
  const skip = runtimeKeys(page);
  const fields: Record<string, unknown> = {};
  const specs: Record<string, CarriedSpec> = {};
  for (const key of Object.keys(page)) {
    if (skip.has(key)) continue;
    const value = (page as Record<string, unknown>)[key];
    if (value === undefined) continue;
    try {
      const spec = carry(value, true, 0);
      if (spec === NOT_DATA) continue;
      adoptMember(fields, key, JSON.parse(JSON.stringify(value, wire)));
      if (spec !== null) adoptMember(specs, key, spec);
    } catch {
      continue;
    }
  }
  return { fields, specs };
}

/** A list's spec, which `Array.isArray` does not tell apart from the others for a readonly tuple. */
function isList(spec: CarriedSpec): spec is readonly [CarriedSpec] {
  return Array.isArray(spec);
}

/** A carried spec whose class the reloaded page cannot hand back: see {@link resolve}. */
const UNRESOLVED = Symbol('unresolved');

/**
 * A carried spec made into the one `hydrate` reads, against what the reloaded page holds at the same
 * place (its initializer, `held`): a vocabulary value type is the class the runtime registers under its
 * name, and an app's record is the class of the record that initializer holds there, of the same name.
 * Where it holds none, the record cannot come back.
 */
function resolve(spec: CarriedSpec, held: unknown): HydrationSpec | typeof UNRESOLVED {
  if (typeof spec === 'string') return spec;
  if (isList(spec)) {
    const element = resolve(spec[0], undefined);
    return element === UNRESOLVED ? UNRESOLVED : [element];
  }
  if ('tuple' in spec) {
    const parts: (HydrationSpec | null)[] = [];
    for (const part of spec.tuple) {
      const resolved = part === null ? null : resolve(part, undefined);
      if (resolved === UNRESOLVED) return UNRESOLVED;
      parts.push(resolved);
    }
    return { tuple: parts };
  }
  if ('dict' in spec) {
    const values = spec.dict === null ? null : resolve(spec.dict, undefined);
    if (values === UNRESOLVED) return UNRESOLVED;
    return {
      dict: values,
      ...(spec.key !== undefined ? { key: spec.key } : {}),
      ...(spec.byValue !== undefined ? { byValue: spec.byValue } : {}),
    };
  }
  const owner =
    spec.twin !== undefined
      ? valueTwinNamed(spec.twin)
      : spec.record !== undefined
        ? recordClass(held, spec.record)
        : null;
  if (owner === undefined) return UNRESOLVED;
  const specs: Record<string, HydrationSpec> = {};
  for (const name of Object.keys(spec.members)) {
    const member = resolve(spec.members[name], memberOf(held, name));
    if (member === UNRESOLVED) return UNRESOLVED;
    adoptMember(specs, name, member);
  }
  return owner === null
    ? { members: specs }
    : { members: specs, of: owner as HydratableConstructor };
}

/** The class of the record `held` is, when it is one of the class named `name`. */
function recordClass(held: unknown, name: string): object | undefined {
  if (held === null || typeof held !== 'object') return undefined;
  const prototype = Object.getPrototypeOf(held) as { constructor?: { name?: string } } | null;
  return isRecord(prototype) && prototype?.constructor?.name === name
    ? prototype.constructor
    : undefined;
}

/** What `held` declares under `name`, as the place a member of the same name is rebuilt into. */
function memberOf(held: unknown, name: string): unknown {
  return held !== null &&
    typeof held === 'object' &&
    !Array.isArray(held) &&
    declaresMember(held, name)
    ? (held as Record<string, unknown>)[name]
    : undefined;
}

/**
 * Whether the reloaded page's initializer holds a value of the same type, or none: a field whose type
 * the edit changed keeps its initializer, which is the shape the edit gave it.
 */
function sameType(value: unknown, held: unknown): boolean {
  if (value === null || value === undefined || held === null || held === undefined) return true;
  if (typeof value !== typeof held) return false;
  return typeof value !== 'object' || Object.getPrototypeOf(value) === Object.getPrototypeOf(held);
}

/**
 * Hands the reloaded page the fields it held, before it builds, each rebuilt by its spec with `hydrate`,
 * the server payload's own machinery, as the type it was. Only a field the new page declares too: one
 * the edit removed or renamed is left behind, one it added keeps its initializer, and so does one whose
 * type the edit changed or whose record class the reloaded page no longer holds there.
 */
export function restorePageState(page: object, saved: PageState): void {
  const skip = runtimeKeys(page);
  const specs = saved.specs ?? {};
  for (const key of Object.keys(saved.fields ?? {})) {
    if (skip.has(key) || !Object.prototype.hasOwnProperty.call(page, key)) continue;
    const held = (page as Record<string, unknown>)[key];
    try {
      const carried = Object.prototype.hasOwnProperty.call(specs, key) ? specs[key] : null;
      const spec = carried === null ? null : resolve(carried, held);
      if (spec === UNRESOLVED) continue;
      const value = spec === null ? saved.fields[key] : hydrate(saved.fields[key], spec);
      if (sameType(value, held)) adoptMember(page, key, value);
    } catch {
      // A field that cannot be rebuilt keeps its initializer, and the others still cross.
    }
  }
}
