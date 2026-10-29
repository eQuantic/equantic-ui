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
import { twinJson } from './utils/twin-json';
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
import {
  toBase64CharArray,
  toBase64String,
  toHexString,
  toHexStringLower,
} from './utils/convert-encoding';
import {
  intConvert,
  intParse,
  intTryParse,
  realConvert,
  realParse,
  realTryParse,
} from './utils/number-parse';
import {
  average,
  enumerable,
  firstOrDefault,
  iterate,
  lastOrDefault,
  max,
  min,
  seq,
  sequenceEqual,
  singleOrDefault,
  toArray,
  toDictionary,
  zipTuples,
} from './utils/linq';
import { range, repeat } from './utils/sequence-factories';
import {
  chars,
  compare,
  compareRange,
  compareRangeBy,
  equals as stringEquals,
  join,
  joinRange,
} from './utils/string-statics';
import {
  compareTo,
  contains as textContains,
  endsWith,
  indexOf,
  indexOfChar,
  instanceEquals,
  lastIndexOf,
  lastIndexOfChar,
  replace,
  startsWith,
} from './utils/string-search';
import {
  asNumber,
  format,
  recordText,
  recordTextGroup,
  stringFormat,
  stringFormatInvariant,
  textGroup,
  tupleText,
  tupleTextGroup,
} from './utils/format';
import { nextTextElementLength, textElementStarts } from './utils/text-elements';
import { unicodeCategory } from './utils/unicode-category';
import { hasNonWhiteSpace, isWhiteSpace, trim, trimEnd, trimStart } from './utils/white-space';
import { toLower, toUpper } from './utils/casing';
import { split, splitAny } from './utils/string-split';
import { str } from './utils/culture';
import { dateTime, timeSpan, dateOnly, timeOnly, dateTimeOffset } from './utils/datetime';
import { stringBuilder } from './utils/string-builder';
import {
  queue,
  stack,
  linkedList,
  linkedListNode,
  contains,
  remove,
  add as collectionAdd,
  clear as collectionClear,
  item as listItem,
  setItem as listSetItem,
  sameItem,
  pairComparer,
  count,
  setAdd,
  zip,
} from './utils/collections';
import { dictionary, pair } from './utils/dictionary';
import { hashSet, hashSetOf } from './utils/hash-set';
import {
  equalsOwn,
  equalsOwnGeneric,
  equatable,
  equatableCall,
  memberEquality,
  sameGeneric,
  sameKey,
  tupleEquality,
} from './utils/key-equality';
import {
  addRange,
  arrayFind,
  arrayFindIndex,
  arrayFindLast,
  arrayFindLastIndex,
  arrayIndexOf,
  arrayLastIndexOf,
  arrayReverse,
  arraySort,
  arraySortBy,
  binarySearch,
  comparerOrder,
  copyRangeTo,
  copyTo,
  find,
  findIndex,
  findLast,
  findLastIndex,
  indexOf as listIndexOf,
  getRange as listGetRange,
  insert as listInsert,
  insertRange,
  lastIndexOf as listLastIndexOf,
  listCapacity,
  listCopy,
  listReverse,
  listSort,
  listSortBy,
  order,
  removeAll,
  removeAt as listRemoveAt,
  removeRange as listRemoveRange,
  stringOrder,
} from './utils/list';
import { boolConvert, boolParse, boolTryParse } from './utils/boolean-text';
import { sortedSet, sortedDictionary, sortedList } from './utils/sorted';
import { liftArith, liftCmp, liftUnary, unwrap } from './utils/nullable';
import { equals, equalsGroup, equalsInstance } from './utils/equals';
import { hash, hashCombine, hashFields, hashGroup, identityHash, instanceHash } from './utils/hash';
import { guidOf, guidParse, guidTryParse } from './utils/guid';
import { CancellationToken, CancellationTokenRegistration, CancellationTokenSource, delay as taskDelay } from './utils/cancellation';
import {
  bases as exceptionBases,
  create as createException,
  exception,
  Exception,
  filter as exceptionFilter,
  is as isException,
  raise,
  receiver,
  delegateTarget,
  thrown,
  construct as constructException,
  typeInitialization,
} from './utils/exceptions';
import {
  isDefined as enumIsDefined,
  declaredNames as enumNames,
  name as enumName,
  parse as enumParse,
  text as enumText,
  tryParse as enumTryParse,
  unbox as enumUnbox,
  values as enumValues,
  zero as enumZero,
} from './utils/enums';
import { lockGate, newObject } from './utils/net-object';
import { resolveService } from './utils/services';
import { StyleBuilder } from './utils/style-builder';
import { ClassBuilder, joinClasses, whenClass } from './utils/class-builder';

/**
 * `$eq` — the single runtime namespace the transpiler emits for .NET-compat helpers, organised by
 * domain. Instead of scattering loose imports (`dec`, `long`, `dateTime`, …) into every generated
 * module — short, collision-prone names — the compiler emits `$eq.num.dec(...)`, `$eq.time.dateTime.of(...)`,
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
  Object.assign(closingLike(Object.create(Object.getPrototypeOf(value)) as T, value), value, patch);

/**
 * The closed type a generic record or struct was built as, by instance (#651). C# compares a
 * record's EqualityContract, its closed type, so `Box<int>` never equals `Box<double>`; the twin is one
 * `Box` class for every type argument, and both values were a `Box` holding 1. Where C# names the
 * type arguments, the build marks the value with them; held aside, so its JSON and its members are
 * the value's alone.
 */
const closures = new WeakMap<object, string>();

/** `value` as built for the closed type `typeArguments` names. */
export const closing = <T extends object>(value: T, typeArguments: string): T => {
  closures.set(value, typeArguments);
  return value;
};

/**
 * `copy` as built for the closed type `source` was built as: a copy of a generic record or struct is a
 * value of its source's type. A struct's `$clone` and a record's `with` build the copy without its
 * constructor, so the copy went unmarked, and a boxed `Pair<double>` equalled a `Pair<int>` (#751).
 * Called on the bare copy, before a member or a patch is written to it: .NET's copy is of its closed
 * type from its allocation, so a copy constructor or an `init` accessor that compares the copy must
 * not meet an unmarked one (found by Copilot's review of #752).
 */
export const closingLike = <T extends object>(copy: T, source: object): T => {
  const closure = closures.get(source);
  if (closure !== undefined) closures.set(copy, closure);
  return copy;
};

/**
 * Whether two values of one generic record or struct are of one closed type. A value built where the
 * type arguments were only known at run time (inside generic code), or rebuilt from the wire, carries
 * no mark: it is not taken for another type, only the two marked differently are.
 */
export const sameClosure = (value: object, other: object): boolean => {
  const mine = closures.get(value);
  const theirs = closures.get(other);
  return mine === undefined || theirs === undefined || mine === theirs;
};

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
  /** LINQ Zip: pairs stop with the shorter sequence. */
  zip,
  /** LINQ's Max and Min by the type they answer, ToDictionary with .NET's refusals, Range and
   * Repeat over arguments evaluated once, the iteration of a twin that implements IEnumerable<T>, and
   * the overloads a sibling's lowering mistook: Average by its element type, FirstOrDefault,
   * LastOrDefault and SingleOrDefault with a default value, SequenceEqual by the default equality, and
   * Zip's tuples. */
  linq: {
    average,
    enumerable,
    firstOrDefault,
    iterate,
    lastOrDefault,
    max,
    min,
    seq,
    sequenceEqual,
    singleOrDefault,
    toArray,
    toDictionary,
    range,
    repeat,
    zipTuples,
  },
  /** C# `with` over a runtime value type — prototype preserved. */
  withPatch,
  /** A generic record's closed type, by value, and the comparison of two — see `closing`. */
  closing,
  closingLike,
  sameClosure,
  /** A twin's JSON, a property's store under the property's name — see utils/twin-json. */
  json: twinJson,
  /** `new object()`, an identity of its own, and a `lock` statement's gate, refused when null. */
  newObject,
  lockGate,
  /** C# range indexing whose endpoints count from the end — see `slice`. */
  slice,
  /** Design mode only: the source span that constructed a node — see `origin`. */
  origin,
  /** The typed boundary: a server value coerced ONCE to its runtime type — see utils/hydrate. */
  hydrate,
  /** Numeric compat: exact decimal and 64-bit integer, every numeric type read from text as .NET
   * reads it, and an integer read or written in a base. */
  num: {
    dec,
    decParse,
    decTryParse,
    decFromDouble,
    decFromSingle,
    decConvert,
    intParse,
    intTryParse,
    intConvert,
    realParse,
    realTryParse,
    realConvert,
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
   * from the platform's segmenter), a character's general category, string's comparisons and
   * ranged join, a string's own searches by a comparison, its split, .NET's white space, and its
   * casing. */
  text: {
    /** A range of chars, refused where it leaves its source, as .NET refuses it. */
    chars,
    format,
    record: recordText,
    recordGroup: recordTextGroup,
    /** A value tuple's text, `(1, a)`, and its `ToString` method group. */
    tuple: tupleText,
    tupleGroup: tupleTextGroup,
    /** The `ToString` method group of a value whose text its type writes, an enum's or a type's name. */
    group: textGroup,
    stringFormat,
    stringFormatInvariant,
    /** A number boxed with its .NET type for `string.Format`, whose arguments are objects. */
    asNumber,
    stringBuilder,
    substring,
    textElementStarts,
    nextTextElementLength,
    unicodeCategory,
    compare,
    compareRange,
    compareRangeBy,
    equals: stringEquals,
    join,
    joinRange,
    startsWith,
    endsWith,
    indexOf,
    lastIndexOf,
    indexOfChar,
    lastIndexOfChar,
    contains: textContains,
    replace,
    instanceEquals,
    compareTo,
    isWhiteSpace,
    hasNonWhiteSpace,
    trim,
    trimStart,
    trimEnd,
    split,
    splitAny,
    /** A text's case and a char's, as .NET changes them, in the page's culture or the invariant one. */
    toUpper,
    toLower,
  },
  /** A dictionary's indexer read, which fails on a missing key the way .NET does, and its write,
   * which answers the value written as C#'s assignment does. */
  mapGet,
  mapSet,
  /** Date and time, tick-precise. */
  time: { dateTime, timeSpan, dateOnly, timeOnly, dateTimeOffset },
  /** An enum as .NET reads it, from the shape the compiler writes: its text, Parse and TryParse,
   * its default, GetNames, GetValues and IsDefined. */
  enums: { text: enumText, parse: enumParse, tryParse: enumTryParse, zero: enumZero, name: enumName, names: enumNames, values: enumValues, unbox: enumUnbox, isDefined: enumIsDefined },
  /**
   * Collections — Queue (FIFO), Stack (LIFO), Dictionary (by slot, as .NET's), LinkedList, and the
   * sorted family (SortedSet / SortedDictionary / SortedList).
   */
  /** What a transpiled constructor resolves its dependencies through — see utils/services. */
  services: { resolve: resolveService },
  collections: {
    queue,
    stack,
    dictionary,
    linkedList,
    linkedListNode,
    sortedSet,
    sortedDictionary,
    sortedList,
    contains,
    count,
    setAdd,
    remove,
    /** `ICollection<T>`'s `Add` and `Clear`, for whichever collection the interface holds (#593). */
    add: collectionAdd,
    clear: collectionClear,
    /** A list face's indexer, read and written, for an array and a twin alike (#586). */
    item: listItem,
    setItem: listSetItem,
    sameItem,
    pairComparer,
    /** `new HashSet<T>(…)`, and a set an initializer or a collection expression builds. */
    hashSet,
    hashSetOf,
    /** The equalities eqc generates for a tuple and an anonymous type, and the one a type that does
     * not decide is compared by (`utils/key-equality.ts`). */
    tupleEquality,
    memberEquality,
    sameKey,
    /** `EqualityComparer<T>.Default` for a type that answers `IEquatable<T>` over itself, by the contract. */
    equatable,
    /** `EqualityComparer<T>.Default` for a type parameter: the value's `IEquatable<T>` over its own type. */
    sameGeneric,
    /** An interface's typed `Equals`, called or made a delegate of, by the contract it binds. */
    equatableCall,
    /** `List<T>`'s and `Array`'s searches, sorts and copies, as .NET answers them (`utils/list.ts`). */
    indexOf: listIndexOf,
    lastIndexOf: listLastIndexOf,
    arrayIndexOf,
    arrayLastIndexOf,
    find,
    findLast,
    findIndex,
    findLastIndex,
    arrayFind,
    arrayFindLast,
    arrayFindIndex,
    arrayFindLastIndex,
    /** `List<T>.AddRange` and `InsertRange`, one element at a time, never a spread past an engine's cap (#1060). */
    addRange,
    insertRange,
    /** `List<T>.Insert`, `RemoveAt`, `RemoveRange` and `GetRange`, each position checked as .NET checks it (#1160). */
    listInsert,
    listRemoveAt,
    listRemoveRange,
    listGetRange,
    removeAll,
    copyTo,
    copyRangeTo,
    order,
    stringOrder,
    comparerOrder,
    listSort,
    listSortBy,
    arraySort,
    arraySortBy,
    /** `List<T>.Reverse` and `Array.Reverse`, in place, a range of them too (#1076). */
    listReverse,
    arrayReverse,
    binarySearch,
    /** `new List<T>(collection)`'s copy, and its refusal of a null collection (#726). */
    listCopy,
    /** `new List<T>(capacity)`'s refusal of a negative capacity, evaluated before the elements. */
    listCapacity,
    /** `new KeyValuePair<K, V>(key, value)` and `KeyValuePair.Create`: the shape a dictionary yields. */
    pair,
  },
  /** `bool.Parse`, `bool.TryParse` and `Convert.ToBoolean(string)`, as .NET reads the text. */
  bool: { parse: boolParse, tryParse: boolTryParse, convert: boolConvert },
  /** `Convert.ToBase64String`, `ToBase64CharArray`, `ToHexString` and `ToHexStringLower`, as .NET
   * encodes the bytes (#716). */
  convert: { toBase64String, toBase64CharArray, toHexString, toHexStringLower },
  /** Nullable<T> lifted operators (null-propagating arithmetic, false-on-null relational), and
   * `Value`, the unwrap that throws for an empty one. */
  nullable: { arith: liftArith, cmp: liftCmp, unary: liftUnary, value: unwrap },
  /** `bool | bool` and `bool & bool`: both operands evaluated, a bool answered — see `or`. */
  logic: { or, and },
  /**
   * C# multicast delegates: `+=` composes an invocation list and `-=` drops the last occurrence.
   * JavaScript has neither, and emitting `+=` literally made `null + function` a STRING.
   */
  delegates: { combine: combineDelegate, remove: removeDelegate },
  /** Structural (value) equality for records/structs/tuples — backs ==, Contains, Distinct. */
  equals,
  /** The method group `value.Equals` of a value the browser holds as data: a delegate over it. */
  equalsGroup,
  /** An instance `Equals` whose receiver may be null, an anonymous object's: refused through null. */
  equalsInstance,
  /** `Equals` on a value whose own equality the app may have written: a null equal only to a null, and
   * otherwise the value's own `equals`, asked every time, itself included. */
  equalsOwn,
  /** The default comparer's `Equals` in generic code: the value's `IEquatable<T>`, then its own `equals`. */
  equalsOwnGeneric,
  /** The receiver of an instance call, refused as .NET refuses a call through null. */
  receiver,
  /** The instance a method group is made over, refused where the delegate is made when it is null. */
  delegateTarget,
  /**
   * `GetHashCode` by .NET's contract, values `equals` finds equal hashing equal (`hash`), an instance
   * call refused on null (`instance`), the method group (`group`), `HashCode.Combine` (`combine`),
   * `object.GetHashCode`'s identity (`identity`), and `ValueType.GetHashCode`'s members (`fields`),
   * which a struct reaches through `base`.
   */
  hash: {
    of: hash,
    instance: instanceHash,
    group: hashGroup,
    combine: hashCombine,
    identity: identityHash,
    fields: hashFields,
  },
  /** A Guid's canonical text, the lowercase `D` format, from any format .NET reads. */
  guid: { parse: guidParse, tryParse: guidTryParse, of: guidOf },
  /**
   * The cancellation pair, built where C# builds it: `new CancellationTokenSource(delay?)`,
   * `CancellationToken.None` (and `default`), `new CancellationToken(canceled)`,
   * `default(CancellationTokenRegistration)` and `CancellationTokenSource.CreateLinkedTokenSource(…)`.
   * Their members are their own, in camelCase.
   */
  cancellation: {
    source: (delay?: number | { readonly totalMilliseconds: number }) => new CancellationTokenSource(delay),
    none: CancellationToken.none,
    registration: CancellationTokenRegistration.none,
    token: CancellationToken.of,
    linked: CancellationTokenSource.createLinkedTokenSource,
  },
  /** `Task.Delay(delay, cancellationToken)`, which ends as cancelled when its token is (utils/cancellation). */
  task: { delay: taskDelay },
  /**
   * .NET exceptions: one built from its type and the types it derives from (`new T(…)`), one of a
   * type the runtime knows, the test a typed `catch` and a type pattern write, a `throw`
   * expression, what a `throw` statement throws when its exception may be null, an exception
   * filter, which answers false where it throws, and what a type whose initializer threw throws on
   * every use; and the base an exception class of the app's extends, with the construction of a
   * generic one, which hands it the types it is before its constructor's body runs.
   */
  exceptions: {
    create: createException,
    of: exception,
    is: isException,
    raise,
    thrown,
    filter: exceptionFilter,
    typeInitialization,
    bases: exceptionBases,
    Exception,
    construct: constructException,
  },
  /** CSS class composition (the styling subsystem). */
  css: { styleBuilder: StyleBuilder, classBuilder: ClassBuilder, joinClasses, whenClass },
} as const;

export type EqNamespace = typeof $eq;
