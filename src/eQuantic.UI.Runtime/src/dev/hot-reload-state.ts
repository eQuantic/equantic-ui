import { StatefulComponent, StatelessComponent } from '../core/component';
import { Component } from '../core/types';
import { adoptMember } from '../utils/adopt-member';
import { Dictionary } from '../utils/dictionary';
import { valueTwinNamed, valueTwinOf } from '../utils/hash';
import {
  hydrate,
  scalarTagOf,
  type DictionarySpec,
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
 * and `record` names a record or a struct of the app's, whose class only the reloaded page can hand
 * back.
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
 * What a hot reload carries for one page: each field as JSON writes it, and the spec of each field
 * whose JSON is not yet the value it was.
 */
export interface PageState {
  readonly fields: Record<string, unknown>;
  readonly specs: Record<string, CarriedSpec>;
}

/** A value the reload cannot give back as it was: see {@link carry}. */
const NOT_DATA = Symbol('not data');

/** What the capture writes for one value: its spec (null when its JSON is it) and its JSON. */
interface Carried {
  readonly spec: CarriedSpec | null;
  readonly json: unknown;
}

const NOTHING: Carried = { spec: null, json: null };

/**
 * A record or a struct eqc emitted, by what its twin SAYS: `static $record`, which RecordTypeEmitter
 * writes on every one and on nothing else. Its methods cannot say it, since `with` and `equals` are
 * names any class may declare: an app's class with an `Equals` override and a `With(...)` of its own
 * was carried as data, and rebuilt without its constructor where its initializer made one.
 */
function isRecord(prototype: object | null): boolean {
  const type = (prototype as { constructor?: { $record?: unknown } } | null)?.constructor;
  return type?.$record === true;
}

/** A plain object: an anonymous type's value, a colour. */
function isPlain(value: unknown): value is Record<string, unknown> {
  if (value === null || typeof value !== 'object') return false;
  const prototype = Object.getPrototypeOf(value) as object | null;
  return prototype === Object.prototype || prototype === null;
}

/**
 * THE CAPTURE'S RULE, the one that decides at every depth what crosses and how it comes back: the
 * spec a value is rebuilt by and the JSON it crosses as, or NOT_DATA when the reload cannot give it
 * back as it was, and the field that holds it keeps its initializer.
 *
 * - A string, a bool and a null; a number JSON writes as itself: a finite one that is not a negative
 *   zero, since JSON writes NaN and the infinities as null and -0 as 0, after which `1 / x` answered
 *   the other infinity.
 * - A `long`, a `decimal` and the dates, by their tag, as their wire text.
 * - An array of data, element by element. One that holds anything but its elements (a pair's `key`
 *   and `value`) is not, since JSON writes the elements alone, and neither is one with a hole or an
 *   undefined element, which JSON writes as null.
 * - A dictionary of data whose keys share one type a key spec revives, found as it found them, written
 *   by its own `toJSON`, the form its hydration reads on any wire. A key that is a negative zero, and a
 *   value that is undefined, which an object drops, are not data.
 * - A plain object (an anonymous type, a colour) of data, member by member.
 * - A vocabulary value type (`Point`, `EdgeInsets`, `ColorToken`) of data, member by member, wherever
 *   it is: the runtime that registers it is the runtime after the reload.
 * - A record or a struct of the app's, which its twin says it is ({@link isRecord}), member by member,
 *   only where the reloaded page can hand back its class (`placed`): a field, or a member of one. Its
 *   class is the app's own module's, which the reload evaluates again, and only the page's
 *   declarations and initializers name it.
 *
 * A record and a vocabulary value cross as their OWN data members, a property's store included,
 * never through a `toJSON` or a getter: a store is the state, and an accessor's body is C# that would
 * run again on the way back, so `FRec`'s setter, which doubles what it is given, turned 10 into 20.
 * One with an accessor of its own is not data.
 *
 * Anything else keeps its initializer: a controller or a service, whose JSON is not it (its maps came
 * back as objects no map method accepts, its callbacks as nothing); a sorted dictionary, whose order is
 * a comparer the capture cannot name; and the other collections the runtime holds as classes of their
 * own, a set or a queue, which this does not carry.
 */
function carry(value: unknown, placed: boolean, depth: number): Carried | typeof NOT_DATA {
  if (value === null || value === undefined) return NOTHING;
  const tag = scalarTagOf(value);
  if (tag !== undefined) {
    return {
      spec: tag,
      json:
        typeof value === 'bigint' ? value.toString() : (value as { toJSON(): unknown }).toJSON(),
    };
  }
  switch (typeof value) {
    case 'string':
    case 'boolean':
      return { spec: null, json: value };
    case 'number':
      return Number.isFinite(value) && !Object.is(value, -0)
        ? { spec: null, json: value }
        : NOT_DATA;
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
  const twin = valueTwinOf(value);
  if (twin !== undefined) return members(value, { twin: twin.name }, depth);
  const prototype = Object.getPrototypeOf(value) as { constructor: { name: string } } | null;
  if (placed && isRecord(prototype)) {
    return members(value, { record: prototype!.constructor.name }, depth);
  }
  return NOT_DATA;
}

/** An array, by one spec for every element when one fits them all, or position by position. */
function list(items: unknown[], depth: number): Carried | typeof NOT_DATA {
  const specs: (CarriedSpec | null)[] = [];
  const json: unknown[] = [];
  for (let at = 0; at < items.length; at++) {
    // JSON writes a hole and an undefined element as null, which reads back as neither: eqc writes
    // the first for `Array.Resize` and the second for a vocabulary struct's `default`.
    if (!(at in items) || items[at] === undefined) return NOT_DATA;
    const carried = carry(items[at], false, depth + 1);
    if (carried === NOT_DATA) return NOT_DATA;
    specs.push(carried.spec);
    json.push(carried.json);
  }
  if (Object.keys(items).length !== items.length) return NOT_DATA;
  const one = oneSpec(items, specs);
  return { spec: one === null ? null : one === undefined ? { tuple: specs } : [one], json };
}

/**
 * A dictionary, by `hydrate`'s own dictionary spec, which is how the runtime revives one whatever its
 * JSON is: its keys by one key spec, its values by one value spec, found as it found them.
 */
function dictionary(map: Dictionary<unknown, unknown>, depth: number): Carried | typeof NOT_DATA {
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
    // An object drops a member that holds undefined, so the entry would not come back at all.
    if (item === undefined) return NOT_DATA;
    const carried = carry(item, false, depth + 1);
    if (carried === NOT_DATA) return NOT_DATA;
    specs.push(carried.spec);
  }
  const one = oneSpec(values, specs);
  if (one === undefined) return NOT_DATA;
  return {
    spec: { dict: one, ...(key ? { key } : {}), ...(equality ? { byValue: equality } : {}) },
    json: JSON.parse(JSON.stringify(map, digits)) as unknown,
  };
}

/** A `long` is a BigInt here, which JSON refuses: it crosses as its digits, which its tag reads back. */
const digits = (_: string, value: unknown): unknown =>
  typeof value === 'bigint' ? value.toString() : value;

/**
 * How a dictionary's key is revived ({@link HydrationKey}): none for a string, whose wire form is
 * itself, a number, a bool or a compat scalar by its tag, and NOT_DATA for anything else, a negative
 * zero included, which a key's text writes as 0.
 */
function keySpec(key: unknown): HydrationKey | null | typeof NOT_DATA {
  switch (typeof key) {
    case 'string':
      return null;
    case 'number':
      return Object.is(key, -0) ? NOT_DATA : 'number';
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

/**
 * An object by its own data members, the stores of a record's properties included: a plain object's
 * spec names only the members that need one, and is none when no member does; a class's spec always
 * names its class, to be rebuilt on its prototype. A member that holds undefined is left out, as JSON
 * leaves it out, and reads back undefined all the same.
 */
function members(
  value: object,
  owner: { readonly twin?: string; readonly record?: string },
  depth: number,
): Carried | typeof NOT_DATA {
  const specs: Record<string, CarriedSpec> = {};
  const json: Record<string, unknown> = {};
  let named = false;
  for (const name of Object.keys(value)) {
    const member = Object.getOwnPropertyDescriptor(value, name)!;
    if (!('value' in member)) return NOT_DATA;
    if (member.value === undefined) continue;
    const carried = carry(member.value, true, depth + 1);
    if (carried === NOT_DATA) return NOT_DATA;
    adoptMember(json, name, carried.json);
    if (carried.spec === null) continue;
    adoptMember(specs, name, carried.spec);
    named = true;
  }
  if (owner.twin === undefined && owner.record === undefined)
    return { spec: named ? { members: specs } : null, json };
  return { spec: { members: specs, ...owner }, json };
}

/**
 * The page's fields that are data, each in the form its spec reads back, with the spec of each one
 * whose JSON is not yet the value it was. One that cannot cross (a controller, a cycle, a NaN) is left
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
      const carried = carry(value, true, 0);
      if (carried === NOT_DATA) continue;
      adoptMember(fields, key, carried.json);
      if (carried.spec !== null) adoptMember(specs, key, carried.spec);
    } catch {
      continue;
    }
  }
  return { fields, specs };
}

/**
 * What the reloaded page DECLARES at a place: a hydration spec (the page's `$hydration` for a field, a
 * record class's for a member, a list's for its elements), `'declared'` for a member its manifest
 * names with no spec, or nothing. Only a component that prefetches, or a page holding a server value,
 * declares its members to the runtime; a page that does neither declares nothing.
 */
type Declared = HydrationSpec | 'declared' | undefined;

/** The spec a map gives `key` ITSELF, never one every object inherits (`constructor`, `__proto__`). */
function ownSpec(
  specs: Readonly<Record<string, HydrationSpec | 'declared'>> | undefined,
  key: string,
): Declared {
  return specs !== undefined && Object.prototype.hasOwnProperty.call(specs, key)
    ? specs[key]
    : undefined;
}

/** The property a member serves: a store `$name` keeps the value of the property `name`. */
const property = (member: string): string => (member.startsWith('$') ? member.slice(1) : member);

/** What the reloaded page declares for one member, by the class or the spec that owns it. */
function memberDeclared(owner: unknown, member: string): Declared {
  if (typeof owner === 'function')
    return ownSpec((owner as HydratableConstructor).$hydration, property(member));
  if (owner !== null && typeof owner === 'object' && 'members' in owner)
    return ownSpec((owner as { members: Readonly<Record<string, HydrationSpec>> }).members, member);
  return undefined;
}

/** What an object holds of its OWN under `name`, read without running an accessor. */
function own(value: unknown, name: string): unknown {
  if (value === null || typeof value !== 'object') return undefined;
  const member = Object.getOwnPropertyDescriptor(value, name);
  return member !== undefined && 'value' in member ? member.value : undefined;
}

/** A list's spec, which `Array.isArray` does not tell apart from the others for a readonly tuple. */
function isList(spec: CarriedSpec): spec is readonly [CarriedSpec] {
  return Array.isArray(spec);
}

/** A carried spec whose class the reloaded page cannot hand back: see {@link resolve}. */
const UNRESOLVED = Symbol('unresolved');

/**
 * A carried spec made into the one `hydrate` reads, with each class the reloaded page hands back: a
 * vocabulary value type is the class the runtime registers under its name, and an app's record is the
 * class the page declares at that place, or the class of the record its initializer holds there, of
 * the same name. Where it hands back none, the record cannot come back. Neither can one a saved member
 * of which the class now reaches through an accessor with a setter: rebuilding it would run that
 * setter, which is C#, as `hydrate` assigns through one.
 */
function resolve(
  spec: CarriedSpec,
  json: unknown,
  declared: Declared,
  held: unknown,
): HydrationSpec | typeof UNRESOLVED {
  if (typeof spec === 'string') return spec;
  if (isList(spec)) {
    const element = resolve(spec[0], undefined, undefined, undefined);
    return element === UNRESOLVED ? UNRESOLVED : [element];
  }
  if ('tuple' in spec) {
    const parts: (HydrationSpec | null)[] = [];
    for (const part of spec.tuple) {
      const resolved = part === null ? null : resolve(part, undefined, undefined, undefined);
      if (resolved === UNRESOLVED) return UNRESOLVED;
      parts.push(resolved);
    }
    return { tuple: parts };
  }
  if ('dict' in spec) {
    const values = spec.dict === null ? null : resolve(spec.dict, undefined, undefined, undefined);
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
        ? recordClass(spec.record, declared, held)
        : null;
  if (owner === undefined) return UNRESOLVED;
  if (owner !== null && setsAny(owner.prototype, json)) return UNRESOLVED;
  const specs: Record<string, HydrationSpec> = {};
  for (const name of Object.keys(spec.members)) {
    const member = resolve(
      spec.members[name],
      own(json, name),
      memberDeclared(owner ?? declared, name),
      own(held, name),
    );
    if (member === UNRESOLVED) return UNRESOLVED;
    adoptMember(specs, name, member);
  }
  return owner === null
    ? { members: specs }
    : { members: specs, of: owner as HydratableConstructor };
}

/** The record class named `name` the reloaded page hands back: declared there, or held there. */
function recordClass(
  name: string,
  declared: Declared,
  held: unknown,
): { readonly prototype: object; readonly name: string } | undefined {
  if (typeof declared === 'function') {
    const type = declared as unknown as { readonly prototype: object; readonly name: string };
    if (type.name === name && isRecord(type.prototype)) return type;
  }
  if (held === null || typeof held !== 'object') return undefined;
  const prototype = Object.getPrototypeOf(held) as {
    constructor?: { readonly prototype: object; readonly name: string };
  } | null;
  return isRecord(prototype) && prototype?.constructor?.name === name
    ? prototype.constructor
    : undefined;
}

/** Whether a member `json` holds is, on the class, an accessor with a setter, which rebuilding runs. */
function setsAny(prototype: object, json: unknown): boolean {
  if (json === null || typeof json !== 'object') return false;
  return Object.keys(json).some((name) => {
    for (
      let level = prototype as object | null;
      level !== null && level !== Object.prototype;
      level = Object.getPrototypeOf(level) as object | null
    ) {
      const member = Object.getOwnPropertyDescriptor(level, name);
      if (member !== undefined) return member.set !== undefined;
    }
    return false;
  });
}

/** Text, a number, a bool, a null, or a list or a plain object of them: JSON's own, nothing built. */
function plainData(value: unknown): boolean {
  if (value === null || value === undefined) return true;
  switch (typeof value) {
    case 'string':
    case 'number':
    case 'boolean':
      return true;
    case 'object':
      break;
    default:
      return false;
  }
  if (Array.isArray(value)) return value.every(plainData);
  return isPlain(value) && Object.values(value).every(plainData);
}

/** The element of an initializer's list a saved one is compared with: the one at its place, or any. */
function sample(held: unknown, at: number): unknown {
  if (!Array.isArray(held)) return undefined;
  const there: unknown = held[at];
  return there !== null && there !== undefined
    ? there
    : held.find((item: unknown) => item !== null && item !== undefined);
}

/** An object's own members that hold a value, as a type's members are its shape. */
function shapeOf(value: object): string[] {
  return Object.keys(value)
    .filter((name) => own(value, name) !== undefined)
    .sort();
}

/**
 * THE RESTORE'S RULE: whether a rebuilt value has, at every depth, the type the reloaded page gives
 * that place, which is how a field whose type the edit changed keeps its initializer. The page gives a
 * place a type by DECLARING one there (a hydration spec), which decides even over an empty or a null
 * initializer; where it declares none, by what its initializer HOLDS there. A value the reload had to
 * build (a long, a decimal, a date, a dictionary, a vocabulary value, a record) is taken only where one
 * of the two confirms its type: rebuilt into a place nothing types, a long[] saved before an edit that
 * made it a string[] handed the edited code BigInts. A value whose JSON is already it (text, a number,
 * a bool, a list or a plain object of them) is taken wherever neither contradicts it.
 */
function agrees(value: unknown, declared: Declared, held: unknown): boolean {
  if (value === null || value === undefined) return true;
  if (declared !== undefined && declared !== 'declared') return fits(value, declared, held);
  if (held === null || held === undefined) return plainData(value);
  return sameShape(value, held);
}

/** A rebuilt value against a declared spec; the initializer is the evidence for what it leaves out. */
function fits(value: unknown, declared: HydrationSpec, held: unknown): boolean {
  if (typeof declared === 'string')
    return declared === 'single' ? typeof value === 'number' : scalarTagOf(value) === declared;
  if (typeof declared === 'function') {
    const type = declared as unknown as { readonly prototype: object };
    return (
      Object.getPrototypeOf(value) === type.prototype &&
      membersAgree(value as object, declared, held)
    );
  }
  if (Array.isArray(declared)) {
    const element = (declared as readonly [HydrationSpec])[0];
    return (
      Array.isArray(value) && value.every((item, at) => agrees(item, element, sample(held, at)))
    );
  }
  if ('tuple' in declared) {
    const parts = (declared as { tuple: readonly (HydrationSpec | null)[] }).tuple;
    return (
      Array.isArray(value) &&
      value.every((item, at) => agrees(item, parts[at] ?? undefined, own(held, String(at))))
    );
  }
  if ('dict' in declared)
    return value instanceof Dictionary && dictionaryAgrees(value, declared, held);
  if ('members' in declared) {
    const of = (declared as { of?: { readonly prototype: object } }).of;
    const typed = of !== undefined ? Object.getPrototypeOf(value) === of.prototype : isPlain(value);
    return typed && membersAgree(value as object, declared, held);
  }
  // A collection the runtime holds as a class of its own: never carried.
  return false;
}

/** A rebuilt value against the initializer's value at the same place, with no declaration. */
function sameShape(value: unknown, held: unknown): boolean {
  if (typeof value !== typeof held) return false;
  if (typeof value !== 'object' || value === null) return true;
  if (Array.isArray(value) || Array.isArray(held))
    return (
      Array.isArray(value) &&
      Array.isArray(held) &&
      value.every((item, at) => agrees(item, undefined, sample(held, at)))
    );
  const prototype = Object.getPrototypeOf(value) as { constructor?: unknown } | null;
  if (prototype !== Object.getPrototypeOf(held)) return false;
  if (value instanceof Dictionary) return dictionaryAgrees(value, undefined, held);
  if (scalarTagOf(value) !== undefined) return true;
  return membersAgree(value, isRecord(prototype) ? prototype!.constructor : undefined, held);
}

/**
 * An object's members, each against what the owner declares of it and what the initializer holds
 * there; with an initializer to compare, its members are the type's shape, so a member the edit added
 * or removed is a type the edit changed.
 */
function membersAgree(value: object, owner: unknown, held: unknown): boolean {
  const names = shapeOf(value);
  if (
    held !== null &&
    typeof held === 'object' &&
    names.join('\u0000') !== shapeOf(held).join('\u0000')
  )
    return false;
  return names.every((name) =>
    agrees(own(value, name), memberDeclared(owner, name), own(held, name)),
  );
}

/** A dictionary's equality, false standing for the identity a dictionary is built with by default. */
const equalityOf = (equality: unknown): unknown => (equality === undefined ? false : equality);

/**
 * A rebuilt dictionary against what the page declares or holds: found as the page finds its keys, its
 * keys of the type the page gives them, and each value as {@link agrees} takes it. A key the reload
 * built from its text (a number, a bool, a long, a date) is taken only where the page confirms it.
 */
function dictionaryAgrees(
  value: Dictionary<unknown, unknown>,
  declared: DictionarySpec | undefined,
  held: unknown,
): boolean {
  const kept = held instanceof Dictionary ? (held as Dictionary<unknown, unknown>) : undefined;
  if (declared !== undefined) {
    if (declared.sorted !== undefined) return false;
    if (equalityOf(declared.byValue) !== equalityOf(value.equality)) return false;
  } else if (kept === undefined || equalityOf(kept.equality) !== equalityOf(value.equality)) {
    return false;
  }
  const keyWitness = kept?.keys()[0];
  for (const key of value.keys()) {
    const kind = keySpec(key);
    if (declared !== undefined) {
      const expected = declared.key === 'single' ? 'number' : (declared.key ?? null);
      if (kind !== expected) return false;
    } else if (keyWitness !== undefined) {
      if (kind !== keySpec(keyWitness)) return false;
    } else if (kind !== null) {
      return false;
    }
  }
  const valueWitness = kept?.values().find((item) => item !== null && item !== undefined);
  const valueDeclared = declared?.dict ?? undefined;
  return value.values().every((item) => agrees(item, valueDeclared, valueWitness));
}

/**
 * Hands the reloaded page the fields it held, before it builds, each rebuilt by its spec with
 * `hydrate`, the server payload's own machinery, as the type it was, and only into a field the page
 * still gives that type ({@link agrees}). Only a field the new page declares too: one the edit removed
 * or renamed is left behind, one it added keeps its initializer, and so does one whose type the edit
 * changed, one whose record class the page no longer hands back, and one a setter would rebuild.
 * Answers the fields it decided, restored or left at their initializers.
 */
export function restorePageState(page: object, saved: PageState): Set<string> {
  const skip = runtimeKeys(page);
  const declaredSpecs = (
    page.constructor as { $hydration?: Readonly<Record<string, HydrationSpec | 'declared'>> }
  ).$hydration;
  const specs = saved.specs ?? {};
  const decided = new Set<string>();
  for (const key of Object.keys(saved.fields ?? {})) {
    if (skip.has(key) || !Object.prototype.hasOwnProperty.call(page, key)) continue;
    decided.add(key);
    const held = own(page, key);
    const declared = ownSpec(declaredSpecs, key);
    try {
      const carried = Object.prototype.hasOwnProperty.call(specs, key) ? specs[key] : null;
      const spec = carried === null ? null : resolve(carried, saved.fields[key], declared, held);
      if (spec === UNRESOLVED) continue;
      const value = spec === null ? saved.fields[key] : hydrate(saved.fields[key], spec);
      if (agrees(value, declared, held)) adoptMember(page, key, value);
    } catch {
      // A field that cannot be rebuilt keeps its initializer, and the others still cross.
    }
  }
  return decided;
}

/**
 * The replay of a page's state: its fields restored before it builds ({@link restorePageState}), and
 * every field the replay decided taken out of the server's payload for the page, so the first render's
 * adoption of that payload neither writes back a field the replay left at its initializer, a number
 * into the string field the edit had made of it, nor overwrites one it restored. The members the server
 * alone hands the page, which the capture never holds, and every other component's payload stay.
 */
export function replayPageState(page: object, saved: PageState, key: string): void {
  const decided = restorePageState(page, saved);
  const w = window as unknown as { __INITIAL_STATE__?: Record<string, Record<string, unknown>> };
  const payload = w.__INITIAL_STATE__?.[key];
  if (payload === undefined || decided.size === 0) return;
  const rest: Record<string, unknown> = {};
  for (const name of Object.keys(payload)) {
    if (!decided.has(name)) adoptMember(rest, name, payload[name]);
  }
  w.__INITIAL_STATE__ = { ...w.__INITIAL_STATE__, [key]: rest };
}
