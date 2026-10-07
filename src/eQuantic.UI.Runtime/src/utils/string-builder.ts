/**
 * .NET-compat `System.Text.StringBuilder`.
 *
 * A small mutable string accumulator with .NET's fluent API. The transpiler emits `stringBuilder(...)`
 * for `new StringBuilder(...)` and maps the instance methods to their camelCase equivalents here.
 *
 * Two .NET quirks are reproduced so `ToString()` matches: `Append(bool)` yields `"True"`/`"False"`
 * (capitalised, unlike JS `String(true)`), and `AppendLine` uses `"\n"` (Unix `Environment.NewLine`,
 * matching the server/runtime). Other values append via their JS string form, and a null appends
 * nothing, as it does in every overload.
 *
 * A method takes each of .NET's overload shapes by how many arguments the call passes, which C# fixes
 * per overload: `append(value)`, `append(char, repeatCount)`, `append(value, startIndex, count)`. The
 * `char[]` overloads are methods of their own (`appendChars`, `insertChars`), which the transpiler
 * names from the overload the call binds, because a null array is refused in words a null string is
 * not. Each refusal is .NET's, its checks in .NET's order (#650).
 *
 * `Capacity` is .NET's, which its chunks make: each chunk is an array .NET allocated, and the capacity
 * is the text the chunks before the last hold plus the last one's array. The text is a string here, so
 * only the chunks' sizes are kept, and every member that changes the text changes them as .NET's
 * does: an append fills the last chunk and opens one as large as the text so far, up to 8,000, or as
 * what is left; an insert goes in place in a small chunk with room, and into a chunk of its own
 * anywhere else; a removal takes from the chunks it crosses; and shortening the text keeps
 * `min(Capacity, max(Length * 6 / 5, the last chunk))`. The members a page reaches were missing
 * altogether, so `Capacity` read undefined and `EnsureCapacity` was a TypeError (#679).
 */

import { exception } from './exceptions';
import { INDEX_AT_MOST_LENGTH, outOfRange, requireNonNegative } from './string-statics';

const DEFAULT_CAPACITY = 16;
const MAX_CHUNK = 8000;
const MAX_CAPACITY = 2147483647;
/** An insert goes in place only in a chunk holding at most this many characters, as .NET's MakeRoom. */
const SMALL_CHUNK = 2 * DEFAULT_CAPACITY;

/** One of .NET's chunks: the array it allocated, and how much of it holds text. */
interface Chunk {
  size: number;
  used: number;
}

function stringify(value: unknown): string {
  if (value == null) return '';
  if (typeof value === 'boolean') return value ? 'True' : 'False';
  return String(value);
}

function nullValue(parameter: string): Error {
  return exception('System.ArgumentNullException', `Value cannot be null. (Parameter '${parameter}')`);
}

/** The characters of a `char[]` from `startIndex`, `charCount` of them. */
function charsOf(value: readonly string[], startIndex: number, charCount: number): string {
  return value.slice(startIndex, startIndex + charCount).join('');
}

export class StringBuilder {
  private value: string;
  private chunks: Chunk[];
  private readonly max: number;

  /**
   * @param initial the text it starts with.
   * @param capacity the capacity asked for, the text's length if that is more, and 16 for nothing.
   * @param maxCapacity `MaxCapacity`, past which the text may not grow.
   */
  constructor(initial = '', capacity = 0, maxCapacity = MAX_CAPACITY) {
    this.value = initial;
    this.max = maxCapacity;
    const size = Math.max(capacity === 0 ? Math.min(DEFAULT_CAPACITY, maxCapacity) : capacity, initial.length);
    this.chunks = [{ size, used: initial.length }];
  }

  get length(): number {
    return this.value.length;
  }

  /** `Length = value`: the text cut there, or filled with `\0` up to it, as .NET's setter does. */
  set length(value: number) {
    requireNonNegative('value', value);
    if (value > this.max) throw outOfRange('value', 'capacity was less than the current size.');
    const delta = value - this.value.length;
    if (delta > 0) {
      this.grew(delta, 'repeatCount');
      this.value += '\0'.repeat(delta);
    } else if (delta < 0) {
      this.cut(value);
      this.value = this.value.slice(0, value);
    }
  }

  /** `Capacity`: what the chunks before the last hold, and the last one's array. */
  get capacity(): number {
    const last = this.chunks[this.chunks.length - 1];
    return this.value.length - last.used + last.size;
  }

  /** `Capacity = value`: the last chunk's array resized, refused below the length or past the maximum. */
  set capacity(value: number) {
    requireNonNegative('value', value);
    if (value > this.max) throw outOfRange('value', 'Capacity exceeds maximum capacity.');
    if (value < this.value.length) throw outOfRange('value', 'capacity was less than the current size.');
    const last = this.chunks[this.chunks.length - 1];
    last.size = value - (this.value.length - last.used);
  }

  /** `MaxCapacity`. */
  get maxCapacity(): number {
    return this.max;
  }

  /** `EnsureCapacity(capacity)`: the capacity, raised to `capacity` where it is less. */
  ensureCapacity(capacity: number): number {
    requireNonNegative('capacity', capacity);
    if (this.capacity < capacity) this.capacity = capacity;
    return this.capacity;
  }

  /** The `Chars` indexer's getter: the char at `index`, which an array's bounds refuse past. */
  item(index: number): string {
    if (index < 0 || index >= this.value.length) {
      throw exception('System.IndexOutOfRangeException', 'Index was outside the bounds of the array.');
    }
    return this.value[index];
  }

  /** The `Chars` indexer's setter, refused past the text as .NET's is. */
  setItem(index: number, value: string): void {
    if (index < 0 || index >= this.value.length) {
      throw outOfRange('index', 'Index was out of range. Must be non-negative and less than the size of the collection.');
    }
    this.value = this.value.slice(0, index) + value + this.value.slice(index + 1);
  }

  /** `Equals(StringBuilder)`: the same text, whatever either's capacity. `Equals(object)` is identity. */
  equalsBuilder(other: StringBuilder | null): boolean {
    return other != null && other.value === this.value;
  }

  /** `CopyTo(sourceIndex, char[] destination, destinationIndex, count)`, refused as .NET refuses it. */
  copyTo(sourceIndex: number, destination: string[] | null, destinationIndex: number, count: number): void {
    if (destination == null) throw nullValue('destination');
    requireNonNegative('count', count);
    requireNonNegative('destinationIndex', destinationIndex);
    if (sourceIndex < 0 || sourceIndex > this.value.length) throw outOfRange('sourceIndex', INDEX_AT_MOST_LENGTH);
    if (sourceIndex > this.value.length - count) {
      throw exception('System.ArgumentException', 'Source string was not long enough. Check sourceIndex and count.');
    }
    if (destinationIndex > destination.length - count) {
      throw exception(
        'System.ArgumentException',
        'Either offset did not refer to a position in the string, or there is an insufficient length of destination character array.',
      );
    }
    for (let i = 0; i < count; i++) destination[destinationIndex + i] = this.value[sourceIndex + i];
  }

  /**
   * `Append(value)`, `Append(char, repeatCount)`, and `Append(string, startIndex, count)` or
   * `Append(StringBuilder, startIndex, count)`, which append `count` characters from `startIndex`.
   * Every one of them appended the whole value once (#650).
   */
  append(value: unknown, startOrCount?: number, count?: number): StringBuilder {
    if (startOrCount === undefined) return this.appendText(stringify(value), 'valueCount');
    if (count === undefined) {
      requireNonNegative('repeatCount', startOrCount);
      return this.appendText(stringify(value).repeat(startOrCount), 'repeatCount');
    }
    const startIndex = startOrCount;
    requireNonNegative('startIndex', startIndex);
    requireNonNegative('count', count);
    if (value == null) {
      if (startIndex === 0 && count === 0) return this;
      throw nullValue('value');
    }
    if (count === 0) return this;
    const text = String(value);
    if (startIndex > text.length - count) throw outOfRange('startIndex', INDEX_AT_MOST_LENGTH);
    return this.appendText(text.slice(startIndex, startIndex + count), 'valueCount');
  }

  /**
   * `Append(char[])` and `Append(char[], startIndex, charCount)`: the array's characters, where
   * JavaScript's text of an array put commas between them. Unlike a string's range, an empty range
   * past the end is refused, and it names the count.
   */
  appendChars(value: readonly string[] | null, startIndex?: number, charCount?: number): StringBuilder {
    if (startIndex === undefined || charCount === undefined) {
      return value == null ? this : this.appendText(value.join(''), 'valueCount');
    }
    requireNonNegative('startIndex', startIndex);
    requireNonNegative('charCount', charCount);
    if (value == null) {
      if (startIndex === 0 && charCount === 0) return this;
      throw nullValue('value');
    }
    if (charCount > value.length - startIndex) throw outOfRange('charCount', INDEX_AT_MOST_LENGTH);
    return this.appendText(charsOf(value, startIndex, charCount), 'valueCount');
  }

  appendLine(value: unknown = ''): StringBuilder {
    return this.appendText(stringify(value) + '\n', 'valueCount');
  }

  /** `Insert(index, value)`, and `Insert(index, string, count)`, which inserts it `count` times. */
  insert(index: number, value: unknown, count?: number): StringBuilder {
    if (count !== undefined) requireNonNegative('count', count);
    this.requireIndex(index);
    const text = stringify(value);
    return this.insertText(index, count === undefined ? text : text.repeat(count));
  }

  /** `Insert(index, char[])` and `Insert(index, char[], startIndex, charCount)`. */
  insertChars(
    index: number,
    value: readonly string[] | null,
    startIndex?: number,
    charCount?: number,
  ): StringBuilder {
    this.requireIndex(index);
    if (startIndex === undefined || charCount === undefined) {
      return this.insertText(index, value == null ? '' : value.join(''));
    }
    if (value == null) {
      if (startIndex === 0 && charCount === 0) return this;
      throw nullValue('value');
    }
    requireNonNegative('startIndex', startIndex);
    requireNonNegative('charCount', charCount);
    if (startIndex > value.length - charCount) throw outOfRange('startIndex', INDEX_AT_MOST_LENGTH);
    return this.insertText(index, charsOf(value, startIndex, charCount));
  }

  /** `Remove(startIndex, length)`, its length checked first as .NET checks it; all of it is `Length = 0`. */
  remove(startIndex: number, length: number): StringBuilder {
    requireNonNegative('length', length);
    requireNonNegative('startIndex', startIndex);
    if (length > this.value.length - startIndex) throw outOfRange('length', INDEX_AT_MOST_LENGTH);
    if (startIndex === 0 && length === this.value.length) {
      this.length = 0;
      return this;
    }
    this.taken(startIndex, length);
    this.value = this.value.slice(0, startIndex) + this.value.slice(startIndex + length);
    return this;
  }

  /**
   * `Replace(oldValue, newValue)` and `Replace(oldValue, newValue, startIndex, count)`, for a string
   * or a char: every occurrence that lies inside the range, left to right, and a null new value
   * removes them. An old value that is null or empty is refused before the range is read. A longer
   * replacement takes a chunk of its own, as .NET's does.
   */
  replace(oldValue: string, newValue: string | null, startIndex?: number, count?: number): StringBuilder {
    if (oldValue == null) throw nullValue('oldValue');
    if (oldValue === '') {
      throw exception(
        'System.ArgumentException',
        "The value cannot be an empty string. (Parameter 'oldValue')",
      );
    }
    const length = this.value.length;
    const start = startIndex ?? 0;
    const end = count === undefined ? length : start + count;
    if (startIndex !== undefined && count !== undefined) {
      if (start < 0 || start > length) throw outOfRange('startIndex', INDEX_AT_MOST_LENGTH);
      if (count < 0 || start > length - count) throw outOfRange('count', INDEX_AT_MOST_LENGTH);
    }
    const replacement = newValue ?? '';
    const delta = replacement.length - oldValue.length;
    let text = '';
    let from = start;
    let shift = 0;
    for (let at = this.value.indexOf(oldValue, start); at >= 0 && at + oldValue.length <= end; at = this.value.indexOf(oldValue, at + oldValue.length)) {
      text += this.value.slice(from, at) + replacement;
      from = at + oldValue.length;
      if (delta > 0) this.chunks.splice(this.chunkAt(at + shift), 0, { size: delta, used: delta });
      else if (delta < 0) this.taken(at + shift, -delta);
      shift += delta;
    }
    this.value = this.value.slice(0, start) + text + this.value.slice(from);
    return this;
  }

  clear(): StringBuilder {
    this.length = 0;
    return this;
  }

  /** `ToString()`, and `ToString(startIndex, length)`, the characters of that range. */
  toString(startIndex?: number, length?: number): string {
    if (startIndex === undefined || length === undefined) return this.value;
    requireNonNegative('startIndex', startIndex);
    if (startIndex > this.value.length) {
      throw outOfRange('startIndex', 'startIndex cannot be larger than length of string.');
    }
    requireNonNegative('length', length);
    if (startIndex > this.value.length - length) {
      throw outOfRange('length', 'Index and length must refer to a location within the string.');
    }
    return this.value.slice(startIndex, startIndex + length);
  }

  private requireIndex(index: number): void {
    if (index < 0 || index > this.value.length) throw outOfRange('index', INDEX_AT_MOST_LENGTH);
  }

  private appendText(text: string, parameter: string): StringBuilder {
    this.grew(text.length, parameter);
    this.value += text;
    return this;
  }

  private insertText(index: number, text: string): StringBuilder {
    if (text.length === 0) return this;
    if (this.value.length + text.length > this.max) {
      throw outOfRange('valueCount', 'The length cannot be greater than the capacity.');
    }
    const at = this.chunkAt(index);
    const chunk = this.chunks[at];
    if (chunk.used <= SMALL_CHUNK && chunk.size - chunk.used >= text.length) chunk.used += text.length;
    else this.chunks.splice(at, 0, { size: text.length, used: text.length });
    this.value = this.value.slice(0, index) + text + this.value.slice(index);
    return this;
  }

  /** `count` characters added at the end: the last chunk filled, and one opened for what is left as
   * large as the text so far, up to 8,000, as .NET's ExpandByABlock opens it. */
  private grew(count: number, parameter: string): void {
    if (count <= 0) return;
    if (this.value.length + count > this.max) throw outOfRange(parameter, 'The length cannot be greater than the capacity.');
    const last = this.chunks[this.chunks.length - 1];
    const fits = Math.min(count, last.size - last.used);
    last.used += fits;
    const rest = count - fits;
    if (rest > 0) this.chunks.push({ size: Math.max(rest, Math.min(this.value.length + fits, MAX_CHUNK)), used: rest });
  }

  /** The text cut to `length`, as .NET's `Length` setter cuts its chunks. */
  private cut(length: number): void {
    const at = this.chunkAt(length);
    const offset = this.offsetOf(at);
    if (at !== this.chunks.length - 1) {
      const last = this.chunks[this.chunks.length - 1];
      const preserve = Math.min(this.capacity, Math.max(Math.trunc((this.value.length * 6) / 5), last.size));
      const chunk = this.chunks[at];
      chunk.size = Math.max(chunk.size, preserve - offset);
      this.chunks.length = at + 1;
    }
    this.chunks[at].used = length - offset;
  }

  /** `count` characters taken out at `index`, from each chunk the range crosses. */
  private taken(index: number, count: number): void {
    let offset = 0;
    for (const chunk of this.chunks) {
      const used = chunk.used;
      const from = Math.max(index, offset);
      const to = Math.min(index + count, offset + used);
      if (to > from) chunk.used -= to - from;
      offset += used;
    }
  }

  /** The chunk that holds `index`: the last whose text starts at or before it. */
  private chunkAt(index: number): number {
    let at = this.chunks.length - 1;
    let offset = this.value.length - this.chunks[at].used;
    while (at > 0 && offset > index) {
      at--;
      offset -= this.chunks[at].used;
    }
    return at;
  }

  private offsetOf(at: number): number {
    let offset = 0;
    for (let i = 0; i < at; i++) offset += this.chunks[i].used;
    return offset;
  }
}

/**
 * The `new StringBuilder(...)` overloads, told apart by their arguments as C# binds them: none, a
 * capacity, a text, a text and a capacity, a capacity and a maximum, and a range of a text with a
 * capacity. A capacity of zero is 16, and each refusal is .NET's.
 */
export function stringBuilder(
  first?: string | number | null,
  second?: number,
  third?: number,
  fourth?: number,
): StringBuilder {
  if (typeof first === 'number') {
    requireNonNegative('capacity', first);
    if (second === undefined) return new StringBuilder('', first);
    if (second < 1) {
      throw outOfRange('maxCapacity', `maxCapacity ('${second}') must be a non-negative and non-zero value.`, second);
    }
    if (first > second) throw outOfRange('capacity', 'Capacity exceeds maximum capacity.');
    return new StringBuilder('', first, second);
  }
  const text = first ?? '';
  if (third !== undefined && fourth !== undefined) {
    requireNonNegative('capacity', fourth);
    return new StringBuilder(text.slice(second ?? 0, (second ?? 0) + third), fourth);
  }
  if (second !== undefined) requireNonNegative('capacity', second);
  return new StringBuilder(text, second ?? 0);
}
