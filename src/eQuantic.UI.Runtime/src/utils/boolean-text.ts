/**
 * A bool read from text, as .NET's `Boolean.TryParse` reads it (#402).
 *
 * The twin used to read `String(s).trim().toLowerCase() === 'true'`, which never throws: every text
 * that is not "true" was false, a null text was the string "null", and a trailing NUL, which .NET
 * trims, made "true\0" false. `bool.TryParse` had no translation at all.
 */

import { isWhiteSpace } from './white-space';

/** `ArgumentNullException` for the text `bool.Parse` was handed, as .NET words it. */
const NULL_VALUE = "Value cannot be null. (Parameter 'value')";

/** The text .NET compares: white space and NUL characters trimmed from both ends, as its
 * `TrimWhiteSpaceAndNull` trims them. White space is `char.IsWhiteSpace`'s set, which is not
 * `trim()`'s: U+FEFF is not white there, so "﻿true" is refused. */
function trimmedOfWhiteSpaceAndNull(text: string): string {
  let start = 0;
  let end = text.length;
  while (start < end && (text[start] === '\0' || isWhiteSpace(text[start]))) start++;
  while (end > start && (text[end - 1] === '\0' || isWhiteSpace(text[end - 1]))) end--;
  return text.slice(start, end);
}

/** An ASCII letter's lower case, any other character as it is. */
function lower(code: number): number {
  return code >= 65 && code <= 90 ? code + 32 : code;
}

/** Whether `text` is `word`, a lowercase ASCII word, in any case. .NET folds nothing else onto
 * "True" or "False": a long s (U+017F) is not an s there, where `toUpperCase` makes it one. */
function isWord(text: string, word: string): boolean {
  if (text.length !== word.length) return false;
  for (let i = 0; i < word.length; i++) {
    if (lower(text.charCodeAt(i)) !== word.charCodeAt(i)) return false;
  }
  return true;
}

/** `bool.TryParse`: true or false for "True" or "False" in any case, trimmed; undefined for
 * anything else, a null included, which the caller answers as false in the `out`. */
export function boolTryParse(text: string | null | undefined): boolean | undefined {
  if (text == null) return undefined;
  const value = trimmedOfWhiteSpaceAndNull(text);
  return isWord(value, 'true') ? true : isWord(value, 'false') ? false : undefined;
}

/** `bool.Parse`: what TryParse reads, and .NET's exceptions where it reads nothing. */
export function boolParse(text: string | null | undefined): boolean {
  if (text == null) throw new Error(NULL_VALUE);
  const value = boolTryParse(text);
  if (value === undefined)
    throw new Error(`String '${text}' was not recognized as a valid Boolean.`);
  return value;
}

/** `Convert.ToBoolean(string)`: `bool.Parse`, except that a null text is false. */
export function boolConvert(text: string | null | undefined): boolean {
  return text == null ? false : boolParse(text);
}
