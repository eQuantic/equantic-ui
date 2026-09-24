import {
  dec,
  decConvert,
  decFromDouble,
  decFromSingle,
  decParse,
  decTryParse,
} from './utils/decimal';
import { combineDelegate, removeDelegate } from './utils/delegates';
import { hydrate } from './utils/hydrate';
import { long } from './utils/long';
import {
  round,
  roundSingle,
  roundWithMode,
  roundSingleWithMode,
  sinPi,
  cosPi,
  tanPi,
  fma,
  bitIncrement,
  bitDecrement,
  bitIncrementSingle,
  bitDecrementSingle,
  ieeeRemainder,
  logBase,
  hypotSingle,
  fmaSingle,
  ilogb,
  rootN,
  maxMagnitude,
  minMagnitude,
  maxMagnitudeNumber,
  minMagnitudeNumber,
  maxNumber,
  minNumber,
} from './utils/dotnet-math';
import {
  popCount32,
  popCount64,
  rotateLeft64,
  rotateRight64,
  leadingZeroCount64,
  trailingZeroCount64,
  log2Of64,
} from './utils/bits';
import {
  checked,
  dictGet,
  mapGet,
  mapSet,
  divRem,
  divRemLong,
  intDiv,
  intRem,
  longDiv,
  longRem,
  singleFromLong,
  substring,
} from './utils/overflow';
import { double, single } from './utils/real-text';
import { fromBase, toBase } from './utils/convert-base';
import { max, min, toDictionary, toValueDictionary } from './utils/linq';
import {
  compare,
  compareRange,
  compareRangeBy,
  equals as stringEquals,
  joinRange,
} from './utils/string-statics';
import { format, parseEnum, stringFormat } from './utils/format';
import { nextTextElementLength, textElementStarts } from './utils/text-elements';
import { unicodeCategory } from './utils/unicode-category';
import { str } from './utils/culture';
import { dateTime, timeSpan, dateOnly, timeOnly, dateTimeOffset } from './utils/datetime';
import { stringBuilder } from './utils/string-builder';
import {
  queue,
  stack,
  valueMap,
  linkedList,
  contains,
  count,
  setAdd,
  entries,
  zip,
} from './utils/collections';
import { sortedSet, sortedDictionary, sortedList } from './utils/sorted';
import { liftArith, liftCmp, liftUnary } from './utils/nullable';
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

/**
 * C#'s NON-short-circuit logical operators on `bool`: `a | b` and `a & b` evaluate BOTH operands,
 * in order, and answer a bool. JavaScript's `|` and `&` evaluate both too but answer a NUMBER (1 or
 * 0) — and TypeScript refuses them on booleans — while `||` and `&&` answer a bool and SKIP the
 * right side, so `typed |= Type(c)` written as `||` would stop typing after the first character
 * that took. A call is the one spelling that keeps both halves: its arguments are evaluated left to
 * right before the body runs.
 */
export const or = (left: boolean, right: boolean): boolean => left || right;
export const and = (left: boolean, right: boolean): boolean => left && right;

export const $eq = {
  /** A rewritten resx accessor (Track L D2): resolves against the installed culture catalog at
   * CALL time — the whole reason the compiler never inlines it. */
  str,
  /** Dictionary enumeration for transpiled foreach/List-copy — see utils/collections. */
  entries,
  /** LINQ Zip: pairs stop with the shorter sequence. */
  zip,
  /** LINQ's Max and Min by the type they answer, and ToDictionary with .NET's refusals. */
  linq: { max, min, toDictionary, toValueDictionary },
  /** C# `with` over a runtime value type — prototype preserved. */
  withPatch,
  /** C# range indexing whose endpoints count from the end — see `slice`. */
  slice,
  /** Design mode only: the source span that constructed a node — see `origin`. */
  origin,
  /** The typed boundary: a server value coerced ONCE to its runtime type — see utils/hydrate. */
  hydrate,
  /** Numeric compat: exact decimal and 64-bit integer, and an integer read or written in a base. */
  num: {
    dec,
    decParse,
    decTryParse,
    decFromDouble,
    decFromSingle,
    decConvert,
    long,
    checked,
    divRem,
    divRemLong,
    intDiv,
    intRem,
    longDiv,
    longRem,
    single,
    double,
    singleFromLong,
    fromBase,
    toBase,
  },
  /** Math with .NET semantics: banker's rounding, the *Pi family (exact at special angles),
   * fused multiply-add, the neighbours of a double or a single, the IEEE remainder, sign-aware
   * roots, and the min/max tie rules. */
  math: {
    round,
    roundSingle,
    roundWithMode,
    roundSingleWithMode,
    sinPi,
    cosPi,
    tanPi,
    fma,
    bitIncrement,
    bitDecrement,
    bitIncrementSingle,
    bitDecrementSingle,
    ieeeRemainder,
    logBase,
    hypotSingle,
    fmaSingle,
    ilogb,
    rootN,
    maxMagnitude,
    minMagnitude,
    maxMagnitudeNumber,
    minMagnitudeNumber,
    maxNumber,
    minNumber,
  },
  /** Bit operations of the IBinaryInteger surface: population counts, rotations, zero counts —
   * 32-bit on numbers, 64-bit on the BigInt-backed long. */
  bits: {
    popCount32,
    popCount64,
    rotateLeft64,
    rotateRight64,
    leadingZeroCount64,
    trailingZeroCount64,
    log2Of64,
  },
  /** Text: number/string formatting, StringBuilder, StringInfo's text elements (grapheme clusters,
   * from the platform's segmenter), a character's general category, and string's comparisons and
   * ranged join. */
  text: {
    format,
    stringFormat,
    stringBuilder,
    substring,
    textElementStarts,
    nextTextElementLength,
    unicodeCategory,
    compare,
    compareRange,
    compareRangeBy,
    equals: stringEquals,
    joinRange,
  },
  /** A dictionary read that fails on a missing key, the way .NET does. */
  dictGet,
  /** The same read on a runtime map (a sorted or value-keyed dictionary), and its write, which
   * answers the value written as C#'s assignment does. */
  mapGet,
  mapSet,
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
  collections: {
    queue,
    stack,
    valueMap,
    linkedList,
    sortedSet,
    sortedDictionary,
    sortedList,
    contains,
    count,
    setAdd,
  },
  /** Nullable<T> lifted operators (null-propagating arithmetic, false-on-null relational). */
  nullable: { arith: liftArith, cmp: liftCmp, unary: liftUnary },
  /** `bool | bool` and `bool & bool`: both operands evaluated, a bool answered — see `or`. */
  logic: { or, and },
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
