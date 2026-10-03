/**
 * An enum as .NET reads it, from the shape the compiler writes at each call: an enum has no object of
 * its own in the browser, where a value is its member's camelCase name, or its number for a flags
 * enum. `Enum.Parse<Status>("Pending")` named an object `Status` that no module declares, and threw.
 */
export interface EnumShape {
  /** The declared names, in declaration order. */
  readonly names: readonly string[];
  /** What the browser holds for each member, by position: its camelCase name. A flags enum writes
   * none, since what it holds is the member's value. */
  readonly keys?: readonly string[];
  /** Each member's value, by position. */
  readonly values: readonly number[];
  /** A [Flags] enum, whose value is its number and whose text names its set flags. */
  readonly flags: boolean;
  /** The hex digits the `X` format writes: twice the underlying type's size in bytes. */
  readonly digits: number;
  /** An unsigned underlying type (byte, ushort, uint, ulong); a signed one writes nothing. */
  readonly unsigned?: boolean;
}

/** The underlying type's width in bits, which bounds a number `Parse` reads. */
function bits(shape: EnumShape): bigint {
  return BigInt(shape.digits * 4);
}

/** Whether a number fits the underlying type, as `Enum.Parse` requires of one it reads. */
function fits(value: bigint, shape: EnumShape): boolean {
  const width = bits(shape);
  if (shape.unsigned) return value >= 0n && value < 1n << width;
  return value >= -(1n << (width - 1n)) && value < 1n << (width - 1n);
}

/** What the browser holds for each member: its key, or, for a flags enum, its value. */
function keys(shape: EnumShape): readonly (string | number)[] {
  return shape.keys ?? shape.values;
}

/** The value .NET holds, from what the browser holds: a key's member's value, or a number as it is. */
function valueOf(held: unknown, shape: EnumShape): number {
  if (typeof held === 'number') return held;
  const at = keys(shape).indexOf(held as string);
  return at < 0 ? Number(held) : shape.values[at];
}

/** What the browser holds for a value: the first member with it, or the number no member names. */
function hold(value: number, shape: EnumShape): string | number {
  if (shape.flags) return value;
  const at = shape.values.indexOf(value);
  return at < 0 ? value : keys(shape)[at];
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
      throw new Error('Format string can be only "G", "g", "X", "x", "F", "f", "D" or "d".');
  }
}

/**
 * The name or names `value` has: an exact member's, then, when `flags`, its set flags', searched from
 * the highest value as .NET searches them. The bits are BigInt's, which a 32-bit operator would cut:
 * a uint's high bit read negative, and a long's flags above bit 31 vanished.
 */
function names(shape: EnumShape, value: number, flags: boolean): string {
  const exact = shape.values.indexOf(value);
  if (exact >= 0) return shape.names[exact];
  if (!flags || value === 0) return String(value);
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
function read(input: string, shape: EnumShape, ignoreCase: boolean): number | undefined {
  const trimmed = input.trim();
  if (trimmed.length === 0) return undefined;
  if (/^[+-]?\d+$/.test(trimmed)) {
    const number = BigInt(trimmed);
    return fits(number, shape) ? Number(number) : undefined;
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
  return Number(value);
}

/** `Enum.Parse`: the value text names, which throws where .NET throws. */
export function parse(input: string, shape: EnumShape, ignoreCase = false): string | number {
  if (input == null) throw new Error("Value cannot be null. (Parameter 'value')");
  const value = read(input, shape, ignoreCase);
  if (value === undefined) throw new Error(`Requested value '${input}' was not found.`);
  return hold(value, shape);
}

/**
 * `Enum.TryParse`: the value text names, or undefined where .NET answers false, whose out argument
 * then holds the enum's default, `zero`.
 */
export function tryParse(input: string | null | undefined, shape: EnumShape, ignoreCase = false): string | number | undefined {
  if (input == null) return undefined;
  const value = read(input, shape, ignoreCase);
  return value === undefined ? undefined : hold(value, shape);
}

/** The enum's default, `default(TEnum)`: the value 0, as the browser holds it. */
export function zero(shape: EnumShape): string | number {
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
  const value = as === 'number' || typeof given === 'number' ? Number(given) : valueOf(given, shape);
  const at = shape.values.indexOf(value);
  return at < 0 ? null : shape.names[at];
}

/** `GetNames`: the declared names, in the order of their values. */
export function declaredNames(shape: EnumShape): string[] {
  return byValue(shape).map((at) => shape.names[at]);
}

/** `GetValues`: the values, in their order, as the browser holds them. */
export function values(shape: EnumShape): (string | number)[] {
  return byValue(shape).map((at) => hold(shape.values[at], shape));
}

/**
 * `IsDefined`: whether a member has this value, given as the enum (`held`), as a number, by its
 * declared name, which is matched as it is written, or as an `object`, which may be any of them: a
 * string is a name, or the key of a boxed member, and a number is a value.
 */
export function isDefined(given: unknown, shape: EnumShape, as: 'held' | 'number' | 'name' | 'object'): boolean {
  if (given == null) throw new Error("Value cannot be null. (Parameter 'value')");
  if (as === 'object') {
    if (typeof given === 'string') return shape.names.includes(given) || keys(shape).includes(given);
    return shape.values.includes(Number(given));
  }
  if (as === 'name') return shape.names.includes(given as string);
  const value = as === 'number' ? Number(given) : valueOf(given, shape);
  return shape.values.includes(value);
}

/**
 * A cast from `object` to the enum, an unboxing: a boxed member is held as its key, and a boxed number
 * of the underlying type is the member with that value, as the runtime unboxes one. A null and anything
 * else are refused as .NET refuses them: `(Status)(object)"Pending"` passed the text through. A boxed
 * string spelled as a member's key cannot be told from the member, both being that string here.
 */
export function unbox(given: unknown, shape: EnumShape, name: string): string | number {
  if (given == null) throw new Error('Object reference not set to an instance of an object.');
  if (typeof given === 'number') return hold(given, shape);
  if (typeof given === 'string' && keys(shape).includes(given)) return given;
  const type = typeof given === 'string' ? 'System.String' : typeof given === 'boolean' ? 'System.Boolean' : 'System.Object';
  throw new Error(`Unable to cast object of type '${type}' to type '${name}'.`);
}
