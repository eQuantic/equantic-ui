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
 */

import { exception } from './exceptions';
import { INDEX_AT_MOST_LENGTH, outOfRange, requireNonNegative } from './string-statics';

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

  constructor(initial = '') {
    this.value = initial;
  }

  get length(): number {
    return this.value.length;
  }

  /**
   * `Append(value)`, `Append(char, repeatCount)`, and `Append(string, startIndex, count)` or
   * `Append(StringBuilder, startIndex, count)`, which append `count` characters from `startIndex`.
   * Every one of them appended the whole value once (#650).
   */
  append(value: unknown, startOrCount?: number, count?: number): StringBuilder {
    if (startOrCount === undefined) {
      this.value += stringify(value);
      return this;
    }
    if (count === undefined) {
      requireNonNegative('repeatCount', startOrCount);
      this.value += stringify(value).repeat(startOrCount);
      return this;
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
    this.value += text.slice(startIndex, startIndex + count);
    return this;
  }

  /**
   * `Append(char[])` and `Append(char[], startIndex, charCount)`: the array's characters, where
   * JavaScript's text of an array put commas between them. Unlike a string's range, an empty range
   * past the end is refused, and it names the count.
   */
  appendChars(value: readonly string[] | null, startIndex?: number, charCount?: number): StringBuilder {
    if (startIndex === undefined || charCount === undefined) {
      if (value != null) this.value += value.join('');
      return this;
    }
    requireNonNegative('startIndex', startIndex);
    requireNonNegative('charCount', charCount);
    if (value == null) {
      if (startIndex === 0 && charCount === 0) return this;
      throw nullValue('value');
    }
    if (charCount > value.length - startIndex) throw outOfRange('charCount', INDEX_AT_MOST_LENGTH);
    this.value += charsOf(value, startIndex, charCount);
    return this;
  }

  appendLine(value: unknown = ''): StringBuilder {
    this.value += stringify(value) + '\n';
    return this;
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

  /** `Remove(startIndex, length)`, its length checked first as .NET checks it. */
  remove(startIndex: number, length: number): StringBuilder {
    requireNonNegative('length', length);
    requireNonNegative('startIndex', startIndex);
    if (length > this.value.length - startIndex) throw outOfRange('length', INDEX_AT_MOST_LENGTH);
    this.value = this.value.slice(0, startIndex) + this.value.slice(startIndex + length);
    return this;
  }

  /**
   * `Replace(oldValue, newValue)` and `Replace(oldValue, newValue, startIndex, count)`, for a string
   * or a char: every occurrence that lies inside the range, left to right, and a null new value
   * removes them. An old value that is null or empty is refused before the range is read.
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
    const replaced = this.value.slice(start, end).split(oldValue).join(newValue ?? '');
    this.value = this.value.slice(0, start) + replaced + this.value.slice(end);
    return this;
  }

  clear(): StringBuilder {
    this.value = '';
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

  private insertText(index: number, text: string): StringBuilder {
    this.value = this.value.slice(0, index) + text + this.value.slice(index);
    return this;
  }
}

/**
 * Factory mirroring the `new StringBuilder(...)` overloads: empty, from an initial string, or with a
 * capacity (an int — ignored, since growth is automatic in JS).
 */
export function stringBuilder(initial?: string | number): StringBuilder {
  return new StringBuilder(typeof initial === 'string' ? initial : '');
}
