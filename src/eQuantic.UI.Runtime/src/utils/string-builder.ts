/**
 * .NET-compat `System.Text.StringBuilder`.
 *
 * A small mutable string accumulator with .NET's fluent API. The transpiler emits `stringBuilder(...)`
 * for `new StringBuilder(...)` and maps the instance methods to their camelCase equivalents here.
 *
 * Two .NET quirks are reproduced so `ToString()` matches: `Append(bool)` yields `"True"`/`"False"`
 * (capitalised, unlike JS `String(true)`), and `AppendLine` uses `"\n"` (Unix `Environment.NewLine`,
 * matching the server/runtime). Other values append via their JS string form.
 */

function stringify(value: unknown): string {
  if (typeof value === 'boolean') return value ? 'True' : 'False';
  return String(value);
}

export class StringBuilder {
  private value: string;

  constructor(initial = '') {
    this.value = initial;
  }

  get length(): number {
    return this.value.length;
  }

  append(value: unknown): StringBuilder {
    this.value += stringify(value);
    return this;
  }

  appendLine(value: unknown = ''): StringBuilder {
    this.value += stringify(value) + '\n';
    return this;
  }

  insert(index: number, value: unknown): StringBuilder {
    const s = stringify(value);
    this.value = this.value.slice(0, index) + s + this.value.slice(index);
    return this;
  }

  remove(startIndex: number, length: number): StringBuilder {
    this.value = this.value.slice(0, startIndex) + this.value.slice(startIndex + length);
    return this;
  }

  /** Replaces every occurrence (matching .NET). */
  replace(oldValue: string, newValue: string): StringBuilder {
    this.value = this.value.split(oldValue).join(newValue);
    return this;
  }

  clear(): StringBuilder {
    this.value = '';
    return this;
  }

  toString(): string {
    return this.value;
  }
}

/**
 * Factory mirroring the `new StringBuilder(...)` overloads: empty, from an initial string, or with a
 * capacity (an int — ignored, since growth is automatic in JS).
 */
export function stringBuilder(initial?: string | number): StringBuilder {
  return new StringBuilder(typeof initial === 'string' ? initial : '');
}
