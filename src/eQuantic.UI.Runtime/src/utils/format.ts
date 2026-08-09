/**
 * C#-style string formatting helper
 * @param value The value to format
 * @param format The format string (e.g. "C2", "N0", "yyyy-MM-dd")
 * @param alignment Optional alignment width
 */
export function format(value: any, format: string | null, alignment?: number): string {
  if (value === null || value === undefined) return '';

  let result = String(value);

  if (format) {
    if (typeof value === 'number') {
      result = formatNumber(value, format);
    } else if (value instanceof Date) {
      result = formatDate(value, format);
    }
  }

  if (alignment) {
    const width = Math.abs(alignment);
    if (result.length < width) {
      const padding = ' '.repeat(width - result.length);
      return alignment > 0 ? padding + result : result + padding;
    }
  }

  return result;
}

/**
 * A CUSTOM numeric format is a picture of the number — `0.0`, `#,##0.00`, `000` — rather than one
 * of the single-letter standard specifiers. `0` is a digit that is always shown (padded), `#` is
 * one shown only if it is there, and a `,` among them asks for group separators.
 *
 * It used to fall through to `value.toString()`, so `$"{bytes / 1024.0:0.0} KB"` — ordinary C# —
 * reached the browser as `0.72265625 KB`. The C# side had already done its part: eqc emits the
 * specifier faithfully, and this is where it was dropped.
 */
function formatCustomNumber(value: number, format: string): string {
  const point = format.indexOf('.');
  const wholePart = point < 0 ? format : format.slice(0, point);
  const fractionPart = point < 0 ? '' : format.slice(point + 1);

  const minimumFractionDigits = (fractionPart.match(/0/g) ?? []).length;
  const maximumFractionDigits = (fractionPart.match(/[0#]/g) ?? []).length;
  const minimumIntegerDigits = Math.max(1, (wholePart.match(/0/g) ?? []).length);

  return value.toLocaleString(undefined, {
    minimumIntegerDigits,
    minimumFractionDigits,
    maximumFractionDigits: Math.max(minimumFractionDigits, maximumFractionDigits),
    useGrouping: wholePart.includes(','),
  });
}

function formatNumber(value: number, format: string): string {
  // A standard specifier is a LETTER (optionally followed by a precision); anything drawn with
  // digit placeholders is a custom picture and is read as one.
  if (!/^[A-Za-z]/.test(format)) {
    return /[0#]/.test(format) ? formatCustomNumber(value, format) : value.toString();
  }

  const specifier = format[0].toUpperCase();
  const precision = format.length > 1 ? parseInt(format.slice(1)) : 2;

  switch (specifier) {
    case 'C': // Currency
      return value.toLocaleString(undefined, {
        style: 'currency',
        currency: 'USD',
        minimumFractionDigits: precision,
        maximumFractionDigits: precision,
      });
    case 'N': // Number
      return value.toLocaleString(undefined, {
        minimumFractionDigits: precision,
        maximumFractionDigits: precision,
      });
    case 'P': // Percentage
      return value.toLocaleString(undefined, {
        style: 'percent',
        minimumFractionDigits: precision,
        maximumFractionDigits: precision,
      });
    case 'F': // Fixed point
      return value.toFixed(precision);
    case 'D': // Decimal (integer pad)
      return Math.round(value).toString().padStart(precision, '0');
    case 'X': // Hex
      return Math.floor(value).toString(16).toUpperCase().padStart(precision, '0');
    default:
      return value.toString();
  }
}

function formatDate(value: Date, format: string): string {
  // Basic date formatting support
  const yyyy = value.getFullYear().toString();
  const MM = (value.getMonth() + 1).toString().padStart(2, '0');
  const dd = value.getDate().toString().padStart(2, '0');
  const HH = value.getHours().toString().padStart(2, '0');
  const mm = value.getMinutes().toString().padStart(2, '0');
  const ss = value.getSeconds().toString().padStart(2, '0');

  // Simple replacements (not robust but handles common cases)
  return format
    .replace(/yyyy/g, yyyy)
    .replace(/MM/g, MM)
    .replace(/dd/g, dd)
    .replace(/HH/g, HH)
    .replace(/mm/g, mm)
    .replace(/ss/g, ss);
}

/**
 * .NET `string.Format(template, ...args)`. Substitutes `{i}` / `{i:spec}` placeholders (the latter via
 * {@link format}, so `{0:F2}` formats arg 0 to 2 decimals) and unescapes `{{`/`}}` to `{`/`}`. Mirrors
 * the interpolation path (`$"{x:F2}"`), which already uses `format`.
 */
export function stringFormat(template: string, ...args: unknown[]): string {
  return template.replace(/\{\{|\}\}|\{(\d+)(?::([^}]*))?\}/g, (m, idx, spec) => {
    if (m === '{{') return '{';
    if (m === '}}') return '}';
    const v = args[Number(idx)];
    if (spec != null) return format(v, spec);
    return v == null ? '' : String(v);
  });
}

/**
 * Parse enum value from string (case-insensitive)
 * @param value The string value to parse
 * @param enumType The enum object
 * @returns The enum value if found, undefined otherwise
 */
export function parseEnum<T extends Record<string, any>>(value: string | number, enumType: T): T[keyof T] | undefined {
  if (typeof value === 'number') {
    return enumType[value] !== undefined ? enumType[value] : undefined;
  }

  const strValue = String(value);

  // Try exact match first (case-sensitive)
  if (enumType[strValue] !== undefined) {
    return enumType[strValue];
  }

  // Try case-insensitive match
  const keys = Object.keys(enumType);
  const matchedKey = keys.find(k => k.toLowerCase() === strValue.toLowerCase());

  if (matchedKey) {
    return enumType[matchedKey];
  }

  // Try matching by value (reverse lookup for numeric enums)
  const values = Object.values(enumType);
  const matchedValue = values.find(v =>
    String(v).toLowerCase() === strValue.toLowerCase()
  );

  if (matchedValue !== undefined) {
    return matchedValue as T[keyof T];
  }

  return undefined;
}
