/**
 * A Guid as .NET reads one. A Guid is its text in the browser, so the text must be the one .NET writes
 * for its value, the lowercase `D` format, or two spellings of one Guid are two values: `==`, a
 * dictionary's key and a set compare the text (#459). `Parse` and the string constructor read every
 * format .NET reads (`N`, `D`, `B`, `P` and `X`, in either case, surrounded by white space) and answer
 * the canonical text, and a Guid from the wire arrives in it already, as System.Text.Json writes it.
 */

import { exception } from './exceptions';
import { isWhiteSpace, trim } from './white-space';

const D = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;
const N = /^[0-9a-f]{32}$/i;
const X = /^\{0x([0-9a-f]{1,8}),0x([0-9a-f]{1,4}),0x([0-9a-f]{1,4}),\{((?:0x[0-9a-f]{1,2},){7}0x[0-9a-f]{1,2})\}\}$/i;

/**
 * The canonical text of a Guid written in any format .NET reads, or undefined for any other text. The
 * white space around it, and inside the `X` format, is .NET's own set (`char.IsWhiteSpace`), which
 * JavaScript's differs from: .NET skips U+0085 and keeps U+FEFF.
 */
function read(text: string): string | undefined {
  const trimmed = trim(text);
  if (D.test(trimmed)) return trimmed.toLowerCase();
  if (N.test(trimmed)) return dashed(trimmed);
  const inner = trimmed.slice(1, -1);
  if (((trimmed.startsWith('{') && trimmed.endsWith('}')) || (trimmed.startsWith('(') && trimmed.endsWith(')'))) && D.test(inner))
    return inner.toLowerCase();
  const hex = X.exec(withoutWhiteSpace(trimmed));
  if (hex === null) return undefined;
  const bytes = hex[4].split(',').map((part) => part.slice(2).padStart(2, '0'));
  return dashed(hex[1].padStart(8, '0') + hex[2].padStart(4, '0') + hex[3].padStart(4, '0') + bytes.join(''));
}

function withoutWhiteSpace(text: string): string {
  let kept = '';
  for (const character of text) if (!isWhiteSpace(character)) kept += character;
  return kept;
}

function dashed(digits: string): string {
  const lower = digits.toLowerCase();
  return `${lower.slice(0, 8)}-${lower.slice(8, 12)}-${lower.slice(12, 16)}-${lower.slice(16, 20)}-${lower.slice(20)}`;
}

/** `Guid.Parse`: the canonical text, or .NET's refusal. */
export function guidParse(text: string | null | undefined): string {
  return parsed(text, 'input');
}

/** `new Guid(string)`, which reads its text as `Parse` does and names it `g` where it refuses a null (#569). */
export function guidOf(text: string | null | undefined): string {
  return parsed(text, 'g');
}

function parsed(text: string | null | undefined, parameter: string): string {
  if (text == null) throw exception('System.ArgumentNullException', `Value cannot be null. (Parameter '${parameter}')`);
  const guid = read(text);
  if (guid === undefined) throw exception('System.FormatException', 'Unrecognized Guid format.');
  return guid;
}

/** `Guid.TryParse`: the canonical text, or undefined where .NET answers false, leaving `Guid.Empty`. */
export function guidTryParse(text: string | null | undefined): string | undefined {
  return text == null ? undefined : read(text);
}

/** `Guid.Empty`. */
export const emptyGuid = '00000000-0000-0000-0000-000000000000';
