import { exception } from './exceptions';

/**
 * An enum as .NET reads it, from the shape the compiler writes at each call: an enum has no object of
 * its own in the browser, where a value is its member's camelCase name, or its number for a flags
 * enum. `Enum.Parse<Status>("Pending")` named an object `Status` that no module declares, and threw.
 *
 * A value is held at the underlying type's width: a number for a type of 32 bits or fewer, and a
 * BigInt for a `long` or a `ulong`, as the SDK holds every 64-bit integer (`utils/long.ts`). A member
 * past 2^53 was a number that is not its value (#551).
 */
export interface EnumShape {
  /** The declared names, in declaration order. */
  readonly names: readonly string[];
  /** What the browser holds for each member, by position: its camelCase name. A flags enum writes
   * none, since what it holds is the member's value. */
  readonly keys?: readonly string[];
  /** Each member's value, by position: a BigInt for a 64-bit enum, a number for any other. */
  readonly values: readonly EnumValue[];
  /** A [Flags] enum, whose value is its number and whose text names its set flags. */
  readonly flags: boolean;
  /** The hex digits the `X` format writes: twice the underlying type's size in bytes. */
  readonly digits: number;
  /** An unsigned underlying type (byte, ushort, uint, ulong); a signed one writes nothing. */
  readonly unsigned?: boolean;
}

/** A value of an enum: a number, or a BigInt for a 64-bit one. */
export type EnumValue = number | bigint;

/** The underlying type's width in bits, which bounds a number `Parse` reads. */
function bits(shape: EnumShape): bigint {
  return BigInt(shape.digits * 4);
}

/** Whether the underlying type is a `long` or a `ulong`, whose values are BigInts. */
function wide(shape: EnumShape): boolean {
  return shape.digits === 16;
}

/** A value as the shape holds its values: a BigInt for a 64-bit enum, a number for any other. */
function atWidth(value: EnumValue, shape: EnumShape): EnumValue {
  return wide(shape) ? BigInt(value) : Number(value);
}

/** Whether a number fits the underlying type, as `Enum.Parse` requires of one it reads. */
function fits(value: bigint, shape: EnumShape): boolean {
  const width = bits(shape);
  if (shape.unsigned) return value >= 0n && value < 1n << width;
  return value >= -(1n << (width - 1n)) && value < 1n << (width - 1n);
}

/** What the browser holds for each member: its key, or, for a flags enum, its value. */
function keys(shape: EnumShape): readonly (string | EnumValue)[] {
  return shape.keys ?? shape.values;
}

/**
 * The value .NET holds, from what the browser holds: a key's member's value, or a value as it is, at
 * the underlying type's width. Text that is no key is the digits a value no member names crossed as.
 */
function valueOf(held: unknown, shape: EnumShape): EnumValue {
  if (typeof held === 'number' || typeof held === 'bigint') return atWidth(held, shape);
  const at = keys(shape).indexOf(held as string);
  if (at >= 0) return shape.values[at];
  return wide(shape) && /^[+-]?\d+$/.test(String(held).trim()) ? BigInt(String(held).trim()) : Number(held);
}

/** The value .NET holds for what the browser holds, at the underlying type's width, for a box that reads its number (`utils/box.ts`). */
export function numberOf(held: unknown, shape: EnumShape): EnumValue {
  return valueOf(held, shape);
}

/** What the browser holds for a value: the first member with it, or the value no member names. */
export function hold(value: EnumValue, shape: EnumShape): string | EnumValue {
  const held = atWidth(value, shape);
  if (shape.flags) return held;
  const at = shape.values.indexOf(held);
  return at < 0 ? held : keys(shape)[at];
}

/**
 * `ToString()`: an exact member's name; for a flags enum, the names of its set flags, highest first
 * in the search and joined in ascending order, as .NET writes them, or the number when a bit is no
 * member's; a value no member names as its number; and a null, which only a nullable enum holds, as
 * the empty string. A format says otherwise: `D` the number, `X` its hex in the underlying type's
 * width, `F` the set flags whether or not the enum is a flags one, and `G` (or none) the rest.
 */
export function text(held: unknown, shape: EnumShape, format?: string | null): string {
  if (held == null) return '';
  // A string no member's key matches crossed with another spelling: it reads as itself, not NaN.
  if (typeof held === 'string' && !keys(shape).includes(held)) return held;
  const value = valueOf(held, shape);
  switch ((format ?? '').toUpperCase()) {
    case 'D':
      return String(value);
    case 'X':
      return BigInt.asUintN(Number(bits(shape)), BigInt(value)).toString(16).toUpperCase().padStart(shape.digits, '0');
    case 'F':
      return names(shape, value, true);
    case '':
    case 'G':
      return names(shape, value, shape.flags);
    default:
      throw exception('System.FormatException', 'Format string can be only "G", "g", "X", "x", "F", "f", "D" or "d".');
  }
}

/**
 * The name or names `value` has: an exact member's, then, when `flags`, its set flags', searched from
 * the highest value as .NET searches them. The bits are BigInt's, which a 32-bit operator would cut:
 * a uint's high bit read negative, and a long's flags above bit 31 vanished.
 */
function names(shape: EnumShape, value: EnumValue, flags: boolean): string {
  const exact = shape.values.indexOf(value);
  if (exact >= 0) return shape.names[exact];
  if (!flags || BigInt(value) === 0n) return String(value);
  const order = shape.values
    .map((member, at) => ({ member: BigInt(member), at }))
    .filter(({ member }) => member !== 0n)
    .sort((a, b) => (unsigned(b.member) > unsigned(a.member) ? 1 : unsigned(b.member) < unsigned(a.member) ? -1 : 0));
  let rest = BigInt(value);
  const found: string[] = [];
  for (const { member, at } of order) {
    if ((rest & member) === member) {
      found.push(shape.names[at]);
      rest &= ~member;
      if (rest === 0n) break;
    }
  }
  return rest === 0n ? found.reverse().join(', ') : String(value);
}

/** A value as .NET orders an enum's: its bits read unsigned, so a negative member comes last. */
function unsigned(value: bigint): bigint {
  return BigInt.asUintN(64, value);
}

/**
 * The value text names, as `Enum.Parse` reads it: a name, names joined by commas (their values or'd,
 * for any enum), or a number; surrounding white space ignored; case kept unless `ignoreCase`.
 * Undefined where .NET refuses it.
 */
function read(input: string, shape: EnumShape, ignoreCase: boolean): EnumValue | undefined {
  const trimmed = input.trim();
  if (trimmed.length === 0) return undefined;
  if (/^[+-]?\d+$/.test(trimmed)) {
    const number = BigInt(trimmed);
    return fits(number, shape) ? atWidth(number, shape) : undefined;
  }
  let value = 0n;
  for (const part of trimmed.split(',')) {
    const name = part.trim();
    const at = shape.names.findIndex((member) =>
      ignoreCase ? member.toLowerCase() === name.toLowerCase() : member === name,
    );
    if (at < 0) return undefined;
    value |= BigInt(shape.values[at]);
  }
  return atWidth(value, shape);
}

/** `Enum.Parse`: the value text names, which throws where .NET throws. */
export function parse(input: string, shape: EnumShape, ignoreCase = false): string | EnumValue {
  if (input == null) throw exception('System.ArgumentNullException', "Value cannot be null. (Parameter 'value')");
  const value = read(input, shape, ignoreCase);
  if (value === undefined) throw exception('System.ArgumentException', `Requested value '${input}' was not found.`);
  return hold(value, shape);
}

/**
 * `Enum.TryParse`: the value text names, or undefined where .NET answers false, whose out argument
 * then holds the enum's default, `zero`.
 */
export function tryParse(input: string | null | undefined, shape: EnumShape, ignoreCase = false): string | EnumValue | undefined {
  if (input == null) return undefined;
  const value = read(input, shape, ignoreCase);
  return value === undefined ? undefined : hold(value, shape);
}

/** The enum's default, `default(TEnum)`: the value 0, as the browser holds it. */
export function zero(shape: EnumShape): string | EnumValue {
  return hold(0, shape);
}

/** The members by value, as `GetNames` and `GetValues` order them: unsigned, so a negative last. */
function byValue(shape: EnumShape): number[] {
  return shape.values
    .map((_, at) => at)
    .sort((a, b) => {
      const left = unsigned(BigInt(shape.values[a]));
      const right = unsigned(BigInt(shape.values[b]));
      return left < right ? -1 : left > right ? 1 : 0;
    });
}

/**
 * `GetName`: the name of the member with this value, given as the enum (`held`), as a number, or as an
 * `object` holding either, or null where no member has it, a flags combination included.
 */
export function name(given: unknown, shape: EnumShape, as: 'held' | 'number' | 'object'): string | null {
  if (given == null) throw exception('System.ArgumentNullException', "Value cannot be null. (Parameter 'value')");
  // An object must hold the enum or an integer, as .NET's GetName(Type, object) requires: a boxed member
  // arrives unboxed (`utils/box.ts`), and a string, a bool or a fraction is refused in .NET's words, where a
  // 64-bit enum's BigInt of a fraction threw a RangeError.
  const integral = typeof given === 'bigint' || (typeof given === 'number' && Number.isInteger(given));
  if (as === 'object' && !integral)
    throw exception(
      'System.ArgumentException',
      "The value passed in must be an enum base or an underlying type for an enum, such as an Int32. (Parameter 'value')",
    );
  const value =
    as === 'number' || typeof given === 'number' || typeof given === 'bigint'
      ? atWidth(given as EnumValue, shape)
      : valueOf(given, shape);
  const at = shape.values.indexOf(value);
  return at < 0 ? null : shape.names[at];
}

/** `GetNames`: the declared names, in the order of their values. */
export function declaredNames(shape: EnumShape): string[] {
  return byValue(shape).map((at) => shape.names[at]);
}

/** `GetValues`: the values, in their order, as the browser holds them. */
export function values(shape: EnumShape): (string | EnumValue)[] {
  return byValue(shape).map((at) => hold(shape.values[at], shape));
}

/**
 * `IsDefined`: whether a member has this value, given as the enum (`held`), as a number, by its
 * declared name, which is matched as it is written, or as an `object`, which may be any of them: a
 * string is a name, or the key of a boxed member, and a number is a value.
 */
export function isDefined(given: unknown, shape: EnumShape, as: 'held' | 'number' | 'name' | 'object'): boolean {
  if (given == null) throw exception('System.ArgumentNullException', "Value cannot be null. (Parameter 'value')");
  if (as === 'object') {
    // A string is a name, matched as it is written: a member held as `object` is boxed (`utils/box.ts`), so a
    // string spelled as its key is a string here as in .NET, and `IsDefined(typeof(Status), "pending")` is false.
    if (typeof given === 'string') return shape.names.includes(given);
    // An integral number of the underlying type, as .NET requires; a bool, a fraction or any other
    // object is a type .NET refuses with "Unknown enum type.". A 64-bit integer is a BigInt here and
    // any narrower one a number, so a 64-bit enum given an int, or another given a long, is the other
    // type .NET refuses in words of its own.
    if ((typeof given !== 'number' || !Number.isInteger(given)) && typeof given !== 'bigint')
      throw exception('System.InvalidOperationException', 'Unknown enum type.');
    if ((typeof given === 'bigint') !== wide(shape)) {
      throw exception(
        'System.ArgumentException',
        'Enum underlying type and the object must be same type or object must be a String. ' +
          `Type passed in was '${typeof given === 'bigint' ? 'System.Int64' : 'System.Int32'}'; the enum underlying type was '${underlying(shape)}'.`,
      );
    }
    return shape.values.includes(atWidth(given, shape));
  }
  if (as === 'name') return shape.names.includes(given as string);
  const value = as === 'number' ? atWidth(given as EnumValue, shape) : valueOf(given, shape);
  return shape.values.includes(value);
}

/** The underlying type's full name, from its width and its sign. */
export function underlying(shape: EnumShape): string {
  const signed = { 2: 'SByte', 4: 'Int16', 8: 'Int32', 16: 'Int64' }[shape.digits] ?? 'Int32';
  const unsigned = { 2: 'Byte', 4: 'UInt16', 8: 'UInt32', 16: 'UInt64' }[shape.digits] ?? 'UInt32';
  return `System.${shape.unsigned ? unsigned : signed}`;
}

/**
 * A cast from `object` to the enum, an unboxing of what is not a box (`utils/box.ts` takes a box first): a
 * boxed number of the underlying type is the member with that value, as the runtime unboxes one. A 64-bit
 * integer is a BigInt here and any narrower one a number: a 64-bit enum unboxes the first and a narrower one
 * the second, as .NET unboxes only the underlying type (`(Huge)(object)1` is refused). A null and anything
 * else are refused as .NET refuses them, a string included, which a member held as `object` never is.
 */
export function unbox(given: unknown, shape: EnumShape, name: string): string | EnumValue {
  if (given == null) throw exception('System.NullReferenceException', 'Object reference not set to an instance of an object.');
  if (typeof given === (wide(shape) ? 'bigint' : 'number')) return hold(given as EnumValue, shape);
  const type =
    typeof given === 'string'
      ? 'System.String'
      : typeof given === 'boolean'
        ? 'System.Boolean'
        : typeof given === 'bigint'
          ? 'System.Int64'
          : typeof given === 'number'
            ? 'System.Int32'
            : 'System.Object';
  throw exception('System.InvalidCastException', `Unable to cast object of type '${type}' to type '${name}'.`);
}
