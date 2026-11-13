/**
 * A value C# boxed into `object` whose .NET type the browser cannot read from the value itself (#552, #768,
 * #954): an enum member, held as its camelCase key (or its number, for a flags enum), and a float, held as a
 * number. Boxed as itself, a member was a string of its name, a float printed a double's digits, and a member of
 * another enum spelled the same was that member.
 *
 * The box carries the type, so it answers `Equals`, `GetHashCode` and `ToString` as .NET's boxed value does, an
 * unboxing to anything but its own type (or, for an enum, its underlying type) is refused, and JSON writes the
 * value it holds, so the wire does not move. An int and a double, a char and a string, a long and a ulong held
 * as `object` stay one kind each: boxing every number would cost every `object` of an app.
 */
import {
  hold,
  isDefined as definedIn,
  name as nameIn,
  numberOf,
  text as enumText,
  underlying,
  unbox as unboxNumber,
  values as valuesOf,
  type EnumShape,
  type EnumValue,
} from './enums';
import { exception } from './exceptions';
import { hash } from './hash';
import { BOXED } from './marks';
import { single } from './real-text';

/** The type name a boxed float carries, .NET's own. */
export const SINGLE = 'System.Single';

/** An enum member or a float held as `object`, with its type. */
export class Boxed {
  constructor(
    /** What the browser holds for the value unboxed: a member's key or number, or the float's number. */
    readonly value: unknown,
    /** The .NET type's full name, which an unboxing and an equality compare. */
    readonly type: string,
    /** The enum's shape, which writes its text and gives its number; none for a float. */
    readonly shape?: EnumShape,
  ) {}

  /** The tag a module below the box reads it by (`utils/marks.ts`). */
  get [BOXED](): boolean {
    return true;
  }

  /** `Equals(object)`: a box of the same type holding the same value; a float's NaN equal to itself, as .NET's. */
  equals(other: unknown): boolean {
    if (!(other instanceof Boxed) || other.type !== this.type) return false;
    return other.value === this.value || (this.shape === undefined && Number.isNaN(this.value) && Number.isNaN(other.value));
  }

  /** `GetHashCode()`: the hash the value has unboxed, an enum's being its underlying value's. */
  getHashCode(): number {
    if (this.shape === undefined) return hash(this.value);
    return hash(numberOf(this.value, this.shape));
  }

  /** `ToString()` in the invariant culture, for a caller that is no formatter; the formatter writes it in the culture in force. */
  toString(): string {
    return this.shape === undefined ? single(this.value as number) : enumText(this.value, this.shape);
  }

  /** What JSON writes: the value itself, as it was written before it was boxed. */
  toJSON(): unknown {
    return this.value;
  }
}

/** A boxing conversion of an enum member (with its shape) or a float: null stays null, and a box stays itself. */
export function box(value: unknown, type: string, shape?: EnumShape): unknown {
  if (value == null || value instanceof Boxed) return value;
  return new Boxed(value, type, shape);
}

/**
 * An unboxing to a primitive value type (`(float)o`, `(int)o`): a box gives what it holds when it holds that
 * type, and an enum's box gives its number to the enum's underlying type, as the runtime unboxes it; anything
 * else in a box is refused with .NET's InvalidCastException. A null is refused with the NullReferenceException
 * .NET throws, unless the target is nullable. A value the browser never boxed (an int, a double, a string)
 * passes as it is.
 */
export function unbox(value: unknown, type: string, nullable = false): unknown {
  if (value == null) {
    if (nullable) return null;
    throw exception('System.NullReferenceException', 'Object reference not set to an instance of an object.');
  }
  if (!(value instanceof Boxed)) return value;
  if (value.type === type) return value.value;
  if (value.shape !== undefined && underlying(value.shape) === type) return numberOf(value.value, value.shape);
  throw exception('System.InvalidCastException', `Unable to cast object of type '${value.type}' to type '${type}'.`);
}

/**
 * What a `Convert` of an `object` reads from a box: an enum member's number, through the enum's IConvertible,
 * and a float's own number; anything else as it is.
 */
export function unboxed(value: unknown): unknown {
  if (!(value instanceof Boxed)) return value;
  if (value.shape === undefined) return value.value;
  return numberOf(value.value, value.shape);
}

/** Whether `value` is a box of the type named, which `is` asks of a value held as `object`. */
export function isBoxOf(value: unknown, type: string): value is Boxed {
  return value instanceof Boxed && value.type === type;
}

/** The box's number, read as the other enum's own when it has the same underlying type, or refused. */
function memberOf(given: Boxed, shape: EnumShape, name: string): string | EnumValue {
  if (given.type === name) return given.value as string | EnumValue;
  if (given.shape !== undefined && underlying(given.shape) === underlying(shape))
    return hold(numberOf(given.value, given.shape), shape);
  throw exception('System.InvalidCastException', `Unable to cast object of type '${given.type}' to type '${name}'.`);
}

/**
 * `(Tone)o`, the unboxing to an enum: its own box gives the member, and a box of another enum over the same
 * underlying type gives the member with its number, as the runtime unboxes it; any other box is refused, and
 * what is not a box goes to the enum's own unboxing, which takes a number and refuses the rest.
 */
export function unboxEnum(given: unknown, shape: EnumShape, name: string, nullable = false): string | EnumValue | null {
  if (nullable && given == null) return null;
  return given instanceof Boxed ? memberOf(given, shape, name) : unboxNumber(given, shape, name);
}

/**
 * `Enum.IsDefined(Type, object)`: a box of the enum asks for its member, a box of another type is refused in
 * .NET's words, and anything else is asked as the enum asks it: a string by its name, a number by its value.
 */
export function isDefined(given: unknown, shape: EnumShape, as: 'held' | 'number' | 'name' | 'object', type?: string): boolean {
  if (as === 'object' && given instanceof Boxed) return definedIn(sameEnum(given, type), shape, 'held');
  return definedIn(given, shape, as);
}

/** What a box holds, where the enum statics take only their own enum's box, in .NET's words otherwise. */
function sameEnum(given: Boxed, type: string | undefined): unknown {
  if (given.type !== type)
    throw exception(
      'System.ArgumentException',
      `Object must be the same type as the enum. The type passed in was '${given.type}'; the enum type was '${type}'.`,
    );
  return given.value;
}

/** `Enum.GetName(Type, object)`: a box of the enum gives the member it holds, and a box of another type is refused. */
export function name(given: unknown, shape: EnumShape, as: 'held' | 'number' | 'object', type?: string): string | null {
  if (as === 'object' && given instanceof Boxed) return nameIn(sameEnum(given, type), shape, 'held');
  return nameIn(given, shape, as);
}

/** `Enum.GetValues(Type)`, which answers an `Array` of boxed members; the generic overload answers them unboxed. */
export function values(shape: EnumShape, type?: string): unknown[] {
  const held = valuesOf(shape);
  return type === undefined ? held : held.map((value) => new Boxed(value, type, shape));
}
