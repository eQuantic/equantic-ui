/**
 * The closed type a generic type of the app's was built as, by instance (#651). C# compares a
 * record's EqualityContract, its closed type, so `Box<int>` never equals `Box<double>`; the twin is one
 * `Box` class for every type argument, and both values were a `Box` holding 1. Where C# names the
 * type arguments, the build marks the value with them, a generic class's too, whose runtime type is
 * compared by `GetType()` (`runtime-types.ts`); held aside, so its JSON and its members are the
 * value's alone.
 */
const closures = new WeakMap<object, string>();

/** `value` as built for the closed type `typeArguments` names. */
export const closing = <T>(value: T, typeArguments: string): T => {
  // A generic method's answer is marked at its call, and a record class's may be null.
  if (typeof value === 'object' && value !== null) closures.set(value, typeArguments);
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

/** The type arguments `value` was marked with, or undefined for a value built unmarked. */
export const closureOf = (value: object): string | undefined => closures.get(value);

/**
 * Whether two marks name one closed type. A value built where the type arguments were only known at
 * run time (inside generic code), or rebuilt from the wire, carries no mark: it is not taken for
 * another type, only the two marked differently are.
 */
export const sameMark = (mine: string | undefined, theirs: string | undefined): boolean =>
  mine === undefined || theirs === undefined || mine === theirs;

/** Whether two values of one generic record or struct are of one closed type, by {@link sameMark}. */
export const sameClosure = (value: object, other: object): boolean =>
  sameMark(closures.get(value), closures.get(other));

/** Each static generic class's statics, by the type arguments of the closed type they are (#1855). */
const closedStatics = new WeakMap<object, Map<string, object>>();

/**
 * The statics of the static generic class `type` for the closed type `typeArguments` names (#1855). C# keeps one set
 * of statics per closed type: `Cache<int>.Last` and `Cache<string>.Last` are two fields, and each closed type runs its
 * type initializer on its own first use. The twin is one class for every type argument, so each closed type is an
 * object made from the class, once, and the class's static members, reached on it, keep their state on it: a static's
 * getter and setter, the type initializer's slots and a static method read `this`. A static the class holds as a
 * constant is read through to the class until the closed type writes its own.
 */
export const statics = <C extends object>(type: C, typeArguments: string): C => {
  let closed = closedStatics.get(type);
  if (!closed) closedStatics.set(type, (closed = new Map()));
  let held = closed.get(typeArguments) as C | undefined;
  // Its own type initializer's state: read through to the class, every closed type would share one.
  if (!held) closed.set(typeArguments, (held = Object.assign(Object.create(type) as C, { $slots: null, $failure: null })));
  return held;
};
