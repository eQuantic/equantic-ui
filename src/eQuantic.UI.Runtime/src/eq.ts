import { dec } from './utils/decimal';
import { combineDelegate, removeDelegate } from './utils/delegates';
import { long } from './utils/long';
import { round } from './utils/dotnet-math';
import { format, parseEnum, stringFormat } from './utils/format';
import { str } from './utils/culture';
import { dateTime, timeSpan, dateOnly, timeOnly, dateTimeOffset } from './utils/datetime';
import { stringBuilder } from './utils/string-builder';
import { queue, stack, valueMap, linkedList, contains, count, setAdd, entries } from './utils/collections';
import { sortedSet, sortedDictionary, sortedList } from './utils/sorted';
import { liftArith, liftCmp } from './utils/nullable';
import { equals } from './utils/equals';
import { resolveService } from './utils/services';
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

/**
 * C# `value[range]` where an endpoint may count from the END and may be ZERO. The direct shapes
 * (`text[a..b]`, `text[..^1]`) compile to a plain `.slice` — this is only for the case JS cannot
 * say: `^0` means "the end", while `slice(0, -0)` is `slice(0, 0)`, which is empty.
 */
export const slice = <T extends string | unknown[]>(
  value: T,
  start: number,
  startFromEnd: boolean,
  end: number | null,
  endFromEnd: boolean,
): T => {
  const from = startFromEnd ? value.length - start : start;
  const to = end === null ? value.length : endFromEnd ? value.length - end : end;
  return (value as string).slice(from, to) as T;
};

/**
 * Stamps a node with the C# span that constructed it and returns the SAME node, so the wrapper can
 * sit around any construction without changing what the expression means.
 *
 * Emitted only by a DESIGN-MODE compilation — a visual editor has to answer "which C# made this
 * pixel", and the source maps cannot say: the whole `Build` body is emitted as one span. Production
 * bundles never contain a call to this.
 *
 * Assigned rather than passed to a constructor because the node is already built by the time the
 * wrapper sees it, and because that keeps this indifferent to every node's own signature.
 */
export const origin = <T extends object>(node: T, source: string, label?: string): T => {
  const carrier = node as { origin?: string; originLabel?: string };
  carrier.origin = source;
  if (label) carrier.originLabel = label;
  return node;
};

export const $eq = {
  /** A rewritten resx accessor (Track L D2): resolves against the installed culture catalog at
   * CALL time — the whole reason the compiler never inlines it. */
  str,
  /** Dictionary enumeration for transpiled foreach/List-copy — see utils/collections. */
  entries,
  /** C# `with` over a runtime value type — prototype preserved. */
  withPatch,
  /** C# range indexing whose endpoints count from the end — see `slice`. */
  slice,
  /** Design mode only: the source span that constructed a node — see `origin`. */
  origin,
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
  /** What a transpiled constructor resolves its dependencies through — see utils/services. */
  services: { resolve: resolveService },
  collections: { queue, stack, valueMap, linkedList, sortedSet, sortedDictionary, sortedList, contains, count, setAdd },
  /** Nullable<T> lifted operators (null-propagating arithmetic, false-on-null relational). */
  nullable: { arith: liftArith, cmp: liftCmp },
  /**
   * C# multicast delegates: `+=` composes an invocation list and `-=` drops the last occurrence.
   * JavaScript has neither, and emitting `+=` literally made `null + function` a STRING.
   */
  delegates: { combine: combineDelegate, remove: removeDelegate },
  /** Structural (value) equality for records/structs/tuples — backs ==, Contains, Distinct. */
  equals,
  /** CSS class composition (the styling subsystem). */
  css: { styleBuilder: StyleBuilder, classBuilder: ClassBuilder, joinClasses, whenClass },
} as const;

export type EqNamespace = typeof $eq;
