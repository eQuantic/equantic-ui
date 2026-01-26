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

function formatNumber(value: number, format: string): string {
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
