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
 *
 * A member that .NET makes of several appends makes them here too, each growing the chunks and each
 * refused on its own, so a refusal keeps what the appends before it added: `AppendLine(value)` is the
 * value and then the line's end, an interpolated `Append($"…")` each of its parts in turn
 * (`appendAligned` for a hole with an alignment), and a `Replace` past the maximum leaves replaced the
 * chunks it reached first. Every text is built before the chunks change, and a text longer than the
 * browser's string can hold is .NET's OutOfMemoryException rather than JavaScript's RangeError.
 */

import { exception } from './exceptions';
import { INDEX_AND_LENGTH, INDEX_AT_MOST_LENGTH, outOfRange, requireNonNegative } from './string-statics';

const DEFAULT_CAPACITY = 16;
const MAX_CHUNK = 8000;
const MAX_CAPACITY = 2147483647;
/** The longest array of chars .NET allocates (`Array.MaxLength`). */
const ARRAY_MAX = 0x7fffffc7;
const ARRAY_DIMENSIONS = 'Array dimensions exceeded supported range.';
const INSUFFICIENT_MEMORY = 'Insufficient memory to continue the execution of the program.';
/** An insert goes in place only in a chunk holding at most this many characters, as .NET's MakeRoom. */
const SMALL_CHUNK = 2 * DEFAULT_CAPACITY;

/** One of .NET's chunks: the array it allocated, and how much of it holds text. */
interface Chunk {
  size: number;
  used: number;
}

function outOfMemory(message: string): Error {
  return exception('System.OutOfMemoryException', message);
}

/** The size of a chunk .NET allocates, refused as .NET refuses an array longer than it can be: a
 *  capacity the browser only records, but .NET allocates whole. */
function allocated(size: number): number {
  if (size > ARRAY_MAX) throw outOfMemory(ARRAY_DIMENSIONS);
  return size;
}

/**
 * The text `build` makes, and .NET's OutOfMemoryException where the browser's string cannot hold it,
 * in place of JavaScript's RangeError, which no catch of a .NET exception would see. An engine's
 * longest string is shorter than .NET's (2^29 characters in V8), so a text .NET holds can be one the
 * browser cannot make.
 */
function materialized(build: () => string): string {
  try {
    return build();
  } catch (error) {
    if (error instanceof RangeError) throw outOfMemory(INSUFFICIENT_MEMORY);
    throw error;
  }
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
function charsOf(value: readonly string[], startIndex = 0, charCount = value.length): string {
  return materialized(() => value.slice(startIndex, startIndex + charCount).join(''));
}

/** The matches of a `Replace` that start in one chunk, which .NET replaces together. */
interface Matches {
  chunk: number;
  at: number[];
}

/**
 * The range `Append(value, startIndex, count)` takes of a string's or a builder's text, refused as
 * .NET refuses it, its count of zero and a null value with an empty range being nothing (null).
 */
function ranged(value: string | null, startIndex: number, count: number): string | null {
  requireNonNegative('startIndex', startIndex);
  requireNonNegative('count', count);
  if (value === null) {
    if (startIndex === 0 && count === 0) return null;
    throw nullValue('value');
  }
  if (count === 0) return null;
  if (startIndex > value.length - count) throw outOfRange('startIndex', INDEX_AT_MOST_LENGTH);
  return value.slice(startIndex, startIndex + count);
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
    this.chunks = [{ size: allocated(size), used: initial.length }];
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
      this.appendRepeated('\0', delta, 'repeatCount');
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
    last.size = allocated(value - (this.value.length - last.used));
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

  /** `Equals(StringBuilder)`: the same text, whatever either's capacity. */
  equalsBuilder(other: StringBuilder | null): boolean {
    return other != null && other.value === this.value;
  }

  /** `Equals(object)`: identity, as a class that does not override it has, which a collection and
   *  `$eq.equals` read too. It was a TypeError where a call reached it as a method. */
  equals(other: unknown): boolean {
    return this === other;
  }

  /** `CopyTo(sourceIndex, char[] destination, destinationIndex, count)`, refused as .NET refuses it. */
  copyTo(sourceIndex: number, destination: string[] | null, destinationIndex: number, count: number): void {
    if (destination == null) throw nullValue('destination');
    requireNonNegative('count', count);
    requireNonNegative('destinationIndex', destinationIndex);
    // The destination's room first: .NET's char[] overload checks it before the span overload it
    // hands the rest to checks the source.
    if (destinationIndex > destination.length - count) {
      throw exception(
        'System.ArgumentException',
        'Either offset did not refer to a position in the string, or there is an insufficient length of destination character array.',
      );
    }
    if (sourceIndex < 0 || sourceIndex > this.value.length) throw outOfRange('sourceIndex', INDEX_AT_MOST_LENGTH);
    if (sourceIndex > this.value.length - count) {
      throw exception('System.ArgumentException', 'Source string was not long enough. Check sourceIndex and count.');
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
      return this.appendRepeated(stringify(value), startOrCount, 'repeatCount');
    }
    const text = ranged(value == null ? null : String(value), startOrCount, count);
    return text === null ? this : this.appendText(text, 'valueCount');
  }

  /**
   * `Append(StringBuilder)` and `Append(StringBuilder, startIndex, count)`, which the transpiler names
   * for the overload the call binds, since `Append(object)` holding a builder is a string's append.
   * .NET copies another builder's chunks after checking the whole length against the maximum, even
   * where it would fit the last chunk, and refuses it in words of its own; a builder appended to
   * itself is appended as its text.
   */
  appendBuilder(value: StringBuilder | null, startIndex?: number, count?: number): StringBuilder {
    const text =
      startIndex === undefined || count === undefined
        ? value?.value || null
        : ranged(value == null ? null : value.value, startIndex, count);
    if (text === null) return this;
    if (value !== this && this.value.length + text.length > this.max) {
      throw outOfRange('Capacity', 'Capacity exceeds maximum capacity.');
    }
    return this.appendText(text, 'valueCount');
  }

  /**
   * `Append(char[])` and `Append(char[], startIndex, charCount)`: the array's characters, where
   * JavaScript's text of an array put commas between them. Unlike a string's range, an empty range
   * past the end is refused, and it names the count.
   */
  appendChars(value: readonly string[] | null, startIndex?: number, charCount?: number): StringBuilder {
    if (startIndex === undefined || charCount === undefined) {
      return value == null ? this : this.appendText(charsOf(value), 'valueCount');
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

  /** `AppendLine(value)`: the value, then the line's end, an append of its own as on .NET. */
  appendLine(value: unknown = ''): StringBuilder {
    return this.appendText(stringify(value), 'valueCount').appendText('\n', 'valueCount');
  }

  /**
   * A hole of an interpolated `Append($"…")` written with an alignment, as .NET's handler appends it:
   * the padding is an append of its own, before the text when it aligns right and after it when it
   * aligns left.
   */
  appendAligned(value: string, alignment: number): StringBuilder {
    const padding = Math.abs(alignment) - value.length;
    if (padding <= 0) return this.appendText(value, 'valueCount');
    return alignment < 0
      ? this.appendText(value, 'valueCount').appendRepeated(' ', padding, 'repeatCount')
      : this.appendRepeated(' ', padding, 'repeatCount').appendText(value, 'valueCount');
  }

  /** `Insert(index, value)`, and `Insert(index, string, count)`, which inserts it `count` times. */
  insert(index: number, value: unknown, count?: number): StringBuilder {
    if (count !== undefined) requireNonNegative('count', count);
    this.requireIndex(index);
    const text = stringify(value);
    if (count === undefined) return this.insertText(index, text);
    if (text.length === 0 || count === 0) return this;
    // .NET counts what the copies need before it makes room for them, and past the maximum it is out
    // of memory there, where a single insert is refused as too long.
    if (text.length * count > this.max - this.value.length) throw outOfMemory(INSUFFICIENT_MEMORY);
    return this.insertText(index, materialized(() => text.repeat(count)));
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
      return this.insertText(index, value == null ? '' : charsOf(value));
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
    const value = this.value.slice(0, startIndex) + this.value.slice(startIndex + length);
    this.taken(startIndex, length);
    this.value = value;
    return this;
  }

  /**
   * `Replace(oldValue, newValue)` and `Replace(oldValue, newValue, startIndex, count)`, for a string
   * or a char: every occurrence that lies inside the range, left to right, and a null new value
   * removes them. An old value that is null or empty is refused before the range is read. A longer
   * replacement takes a chunk of its own, as .NET's does, and .NET replaces chunk by chunk, so past
   * the maximum it refuses at the first chunk that does not fit, the ones before it replaced.
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
    // The matches, grouped by the chunk each starts in: .NET scans chunk by chunk and replaces a
    // chunk's matches together (ReplaceAllInChunk), checking each group's room as it goes.
    const groups: Matches[] = [];
    for (let at = this.value.indexOf(oldValue, start); at >= 0 && at + oldValue.length <= end; at = this.value.indexOf(oldValue, at + oldValue.length)) {
      const chunk = this.chunkAt(at);
      const group = groups[groups.length - 1];
      if (group?.chunk === chunk) group.at.push(at);
      else groups.push({ chunk, at: [at] });
    }
    let fit = groups.length;
    let grown = length;
    for (let i = 0; delta > 0 && i < groups.length; i++) {
      grown += delta * groups[i].at.length;
      if (grown > this.max) {
        fit = i;
        break;
      }
    }
    this.replaced(groups.slice(0, fit), oldValue.length, replacement);
    if (fit < groups.length) throw outOfRange('requiredLength', 'capacity was less than the current size.');
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
    this.room(text.length, parameter);
    const value = materialized(() => this.value + text);
    this.grow(text.length);
    this.value = value;
    return this;
  }

  /** `text` appended `count` times, refused before the text is built, as .NET refuses it. */
  private appendRepeated(text: string, count: number, parameter: string): StringBuilder {
    if (text.length === 0 || count === 0) return this;
    this.room(text.length * count, parameter);
    const value = materialized(() => this.value + text.repeat(count));
    this.grow(text.length * count);
    this.value = value;
    return this;
  }

  private insertText(index: number, text: string): StringBuilder {
    if (text.length === 0) return this;
    // MakeRoom's own refusal, for any insert that would pass the maximum.
    if (this.value.length + text.length > this.max) {
      throw outOfRange('requiredLength', 'capacity was less than the current size.');
    }
    const value = materialized(() => this.value.slice(0, index) + text + this.value.slice(index));
    const at = this.chunkAt(index);
    const chunk = this.chunks[at];
    if (chunk.used <= SMALL_CHUNK && chunk.size - chunk.used >= text.length) chunk.used += text.length;
    else this.chunks.splice(at, 0, { size: Math.max(text.length, DEFAULT_CAPACITY), used: text.length });
    this.value = value;
    return this;
  }

  /** The matches of `groups` replaced, `width` characters each: the text first, then the chunks. */
  private replaced(groups: readonly Matches[], width: number, replacement: string): void {
    if (groups.length === 0) return;
    const value = materialized(() => {
      let text = '';
      let from = 0;
      for (const group of groups) {
        for (const at of group.at) {
          text += this.value.slice(from, at) + replacement;
          from = at + width;
        }
      }
      return text + this.value.slice(from);
    });
    const delta = replacement.length - width;
    // Right to left, so a chunk opened before one group leaves the indices of the groups before it.
    for (let i = groups.length - 1; i >= 0; i--) {
      const total = delta * groups[i].at.length;
      if (total > 0) this.chunks.splice(groups[i].chunk, 0, { size: Math.max(total, DEFAULT_CAPACITY), used: total });
      else if (total < 0) this.takenFrom(groups[i].chunk, -total);
    }
    this.dropEmpty();
    this.value = value;
  }

  /**
   * Whether `count` characters may be added at the end. What fits the last chunk's array goes in
   * unchecked, as on .NET, past `MaxCapacity` too when an earlier block made the capacity larger;
   * past it, .NET refuses a length over the maximum and a block longer than an array can be.
   */
  private room(count: number, parameter: string): void {
    const last = this.chunks[this.chunks.length - 1];
    const free = last.size - last.used;
    if (count <= free) return;
    if (this.value.length + count > this.max) throw outOfRange(parameter, 'The length cannot be greater than the capacity.');
    if (Math.max(count - free, Math.min(this.value.length + free, MAX_CHUNK)) > ARRAY_MAX) throw outOfMemory(ARRAY_DIMENSIONS);
  }

  /** `count` characters added at the end, `room` having allowed them: the last chunk filled, and one
   * opened for what is left as large as the text so far, up to 8,000, as .NET's ExpandByABlock opens it. */
  private grow(count: number): void {
    if (count <= 0) return;
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

  /** `count` characters taken out at `index`, from each chunk the range crosses; a chunk it empties is
   *  unlinked, as .NET's Remove unlinks it. */
  private taken(index: number, count: number): void {
    let offset = 0;
    for (const chunk of this.chunks) {
      const used = chunk.used;
      const from = Math.max(index, offset);
      const to = Math.min(index + count, offset + used);
      if (to > from) chunk.used -= to - from;
      offset += used;
    }
    this.dropEmpty();
  }

  /** `count` characters taken from the chunk at `at`, and from the ones after it where a match ran on. */
  private takenFrom(at: number, count: number): void {
    for (let i = at; count > 0 && i < this.chunks.length; i++) {
      const take = Math.min(count, this.chunks[i].used);
      this.chunks[i].used -= take;
      count -= take;
    }
  }

  /** The chunks a removal emptied, unlinked; the last stays, since it holds the capacity. */
  private dropEmpty(): void {
    const last = this.chunks[this.chunks.length - 1];
    this.chunks = this.chunks.filter((chunk) => chunk.used > 0 || chunk === last);
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
    if (second === undefined) {
      requireNonNegative('capacity', first);
      return new StringBuilder('', first);
    }
    // .NET's order: the capacity against the maximum, then the maximum, then the capacity's sign.
    if (first > second) throw outOfRange('capacity', 'Capacity exceeds maximum capacity.');
    if (second < 1) {
      throw outOfRange('maxCapacity', `maxCapacity ('${second}') must be a non-negative and non-zero value.`, second);
    }
    requireNonNegative('capacity', first);
    return new StringBuilder('', first, second);
  }
  const text = first ?? '';
  if (third !== undefined && fourth !== undefined) {
    const startIndex = second ?? 0;
    requireNonNegative('capacity', fourth);
    requireNonNegative('length', third);
    requireNonNegative('startIndex', startIndex);
    if (startIndex > text.length - third) throw outOfRange('length', INDEX_AND_LENGTH);
    return new StringBuilder(text.slice(startIndex, startIndex + third), fourth);
  }
  if (second !== undefined) requireNonNegative('capacity', second);
  return new StringBuilder(text, second ?? 0);
}
