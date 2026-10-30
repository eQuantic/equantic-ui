/**
 * The closed type a generic record or struct was built as, by instance (#651). C# compares a
 * record's EqualityContract, its closed type, so `Box<int>` never equals `Box<double>`; the twin is one
 * `Box` class for every type argument, and both values were a `Box` holding 1. Where C# names the
 * type arguments, the build marks the value with them; held aside, so its JSON and its members are
 * the value's alone.
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

/**
 * Whether two values of one generic record or struct are of one closed type. A value built where the
 * type arguments were only known at run time (inside generic code) and that no call handed back to code
 * that knows them carries no mark: it is not taken for another type, only the two marked differently are.
 */
export const sameClosure = (value: object, other: object): boolean => {
  const mine = closures.get(value);
  const theirs = closures.get(other);
  return mine === undefined || theirs === undefined || mine === theirs;
};
