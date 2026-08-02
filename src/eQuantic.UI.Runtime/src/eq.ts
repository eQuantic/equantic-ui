import { dec } from './utils/decimal';
import { long } from './utils/long';
import { round } from './utils/dotnet-math';
import { format, parseEnum, stringFormat } from './utils/format';
import { dateTime, timeSpan, dateOnly, timeOnly, dateTimeOffset } from './utils/datetime';
import { stringBuilder } from './utils/string-builder';
import { queue, stack, valueMap, linkedList, contains, count, setAdd } from './utils/collections';
import { sortedSet, sortedDictionary, sortedList } from './utils/sorted';
import { liftArith, liftCmp } from './utils/nullable';
import { equals } from './utils/equals';
import { StyleBuilder } from './utils/style-builder';
import { ClassBuilder, joinClasses, whenClass } from './utils/class-builder';

/**
 * `$eq` — the single runtime namespace the transpiler emits for .NET-compat helpers, organised by
 * domain. Instead of scattering loose imports (`dec`, `long`, `dateTime`, …) into every generated
 * module — short, collision-prone names — the compiler emits `$eq.num.dec(...)`, `$eq.time.dateTime(...)`,
 * etc., and the runtime exposes `$eq` once (globally on `window`, like `StyleBuilder`). No per-module
 * imports, no risk of shadowing a user identifier.
 *
 * Members are the same function references as the individual exports, so the factories keep their
 * attached statics (`$eq.time.dateTime.now()`, `$eq.time.timeSpan.fromSeconds(1)`).
 */
/**
 * C#'s `with` over a VALUE the runtime hands out as a class instance (a TypeStyle, a ColorToken).
 * A record the compiler emits carries its own `with`, but a hand-written twin does not — and a
 * plain object spread would drop the prototype, taking every method on it. This copies the
 * prototype, then the fields, then the patch.
 */
export const withPatch = <T extends object>(value: T, patch: Partial<T>): T =>
  Object.assign(Object.create(Object.getPrototypeOf(value)), value, patch);

export const $eq = {
  /** C# `with` over a runtime value type — prototype preserved. */
  withPatch,
  /** Numeric compat: exact decimal and 64-bit integer. */
  num: { dec, long },
  /** Math with .NET semantics (banker's rounding). */
  math: { round },
  /** Text: number/string formatting and StringBuilder. */
  text: { format, stringFormat, stringBuilder },
  /** Date and time, tick-precise. */
  time: { dateTime, timeSpan, dateOnly, timeOnly, dateTimeOffset },
  /** Enum parsing (member-name string). */
  enums: { parse: parseEnum },
  /**
   * Collections — Queue (FIFO), Stack (LIFO), ValueMap (structurally-keyed dictionary), LinkedList,
   * and the sorted family (SortedSet / SortedDictionary / SortedList).
   */
  collections: { queue, stack, valueMap, linkedList, sortedSet, sortedDictionary, sortedList, contains, count, setAdd },
  /** Nullable<T> lifted operators (null-propagating arithmetic, false-on-null relational). */
  nullable: { arith: liftArith, cmp: liftCmp },
  /** Structural (value) equality for records/structs/tuples — backs ==, Contains, Distinct. */
  equals,
  /** CSS class composition (the styling subsystem). */
  css: { styleBuilder: StyleBuilder, classBuilder: ClassBuilder, joinClasses, whenClass },
} as const;

export type EqNamespace = typeof $eq;
