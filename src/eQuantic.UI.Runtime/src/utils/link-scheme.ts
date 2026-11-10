/**
 * Which destinations an href may carry (C# twin: `LinkScheme`): one with no scheme (a path, relative or
 * rooted, a query, a fragment, a protocol-relative address) or one whose scheme is `http`, `https`,
 * `mailto` or `tel`. Any other scheme is dropped (#1286). The scheme is read as a browser's URL parser
 * reads it, with its letter case and the characters that parser ignores folded away. The parity case
 * `links-with-schemes` holds the two sides to the same answer.
 */

import { localizeDestination } from './culture-routes';

/** The scheme a browser reads in a destination, lower-cased, or null when it reads none. */
function schemeOf(destination: string): string | null {
  let start = 0;
  let end = destination.length;
  while (start < end && destination.charCodeAt(start) <= 0x20) start++;
  while (end > start && destination.charCodeAt(end - 1) <= 0x20) end--;
  let scheme = '';
  for (let i = start; i < end; i++) {
    const c = destination[i];
    if (c === '\t' || c === '\n' || c === '\r') continue;
    if (c === ':') return scheme.length > 0 ? scheme : null;
    const letter = (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z');
    const other = (c >= '0' && c <= '9') || c === '+' || c === '-' || c === '.';
    if (scheme.length === 0 ? !letter : !(letter || other)) return null;
    scheme += c.toLowerCase();
  }
  return null;
}

/** Whether an href may carry a destination. */
export function allowsScheme(destination: string): boolean {
  const scheme = schemeOf(destination);
  return scheme === null || scheme === 'http' || scheme === 'https' || scheme === 'mailto' || scheme === 'tel';
}

/**
 * The destination an href carries (C# twin: `RenderContext.ResolveDestination`): the active language on an
 * app-internal path, or null when its scheme is one an href never carries, and the link is written with no
 * href at all.
 */
export function writtenDestination(destination: string): string | null {
  return allowsScheme(destination) ? localizeDestination(destination) : null;
}
