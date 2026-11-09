/**
 * `a.GetType() == b.GetType()`, and either against `typeof(X)`, for the values whose runtime type the
 * browser knows (#1202): an exception, by the .NET type it carries, and an instance of a class twin, by
 * its prototype and the closed type it was marked with (`closures.ts`). The compiler writes these only
 * where the receiver's static type says the value is one of them, and refuses the comparison elsewhere:
 * an `int` and a `double` are one JavaScript number, and every array one `Array`.
 */
import { closureOf, sameMark } from './closures';
import { receiver, typesOf } from './exceptions';

/** A class twin's prototype closed over the type arguments a value of it was marked with. */
class Closed {
  constructor(
    readonly prototype: object | null,
    readonly closure: string,
  ) {}
}

/**
 * A runtime type as the browser tells it: an exception's most derived .NET type, its full name; a class
 * twin's prototype, for a value built with no mark; or the prototype closed over its mark. The element
 * engine compares the runtime types of every child it reconciles, so an unmarked value's type is its
 * prototype itself, and a closed one is made once for each mark.
 */
export type RuntimeType = string | object | null;

const closedTypes = new WeakMap<object, Map<string, Closed>>();

function closed(prototype: object | null, closure: string | undefined): RuntimeType {
  if (closure === undefined || prototype === null) return prototype;
  let byMark = closedTypes.get(prototype);
  if (byMark === undefined) closedTypes.set(prototype, (byMark = new Map()));
  let type = byMark.get(closure);
  if (type === undefined) byMark.set(closure, (type = new Closed(prototype, closure)));
  return type;
}

/**
 * `value.GetType()`: the NullReferenceException .NET throws where the value is null, before the other
 * side of the comparison is evaluated, since each side is an argument of {@link same}.
 */
export function of(value: unknown): RuntimeType {
  const held = receiver(value) as object;
  const types = typesOf(held);
  if (types !== null) return types[0];
  return closed(Object.getPrototypeOf(held) as object | null, closureOf(held));
}

/** `typeof(X)` for a class twin, closed over the type arguments C# names, if any. */
export function named(type: { readonly prototype: object }, closure?: string): RuntimeType {
  return closed(type.prototype, closure);
}

/** Whether two runtime types are one: a value marked with no type arguments is not taken for another. */
export function same(type: RuntimeType, other: RuntimeType): boolean {
  if (type === other) return true;
  if (typeof type === 'string' || typeof other === 'string') return false;
  const [mine, theirs] = [type instanceof Closed ? type : null, other instanceof Closed ? other : null];
  return (mine?.prototype ?? type) === (theirs?.prototype ?? other) && sameMark(mine?.closure, theirs?.closure);
}
