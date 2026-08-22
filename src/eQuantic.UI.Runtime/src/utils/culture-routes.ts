/**
 * The language as the first path segment — the browser twin of C# `CultureRouteMap`.
 *
 * The server declares the policy once (`UseCultureRoutes`) and inlines it into `__EQ_CONFIG`, so
 * the client does not guess: a segment is a language because the app said so, never because it
 * looks like one. `pt` is a language and `pt` could also be a page.
 *
 * It exists for one reason — an href written `/pricing` has to become `/pt-BR/pricing` while the
 * reader is in Portuguese. Doing that here rather than at every call site is the same decision the
 * C# side makes: a site with two hundred links has two hundred chances to forget, and forgetting
 * is silent, because the link still works. It just leaves the language behind.
 */

import { activeCulture } from './culture';

export interface CultureRouteConfig {
  /** The culture served WITHOUT a prefix. */
  default: string;
  /** The cultures that appear as a leading path segment. */
  prefixed: string[];
}

function config(): CultureRouteConfig | null {
  const declared = (globalThis as { __EQ_CONFIG?: { cultureRoutes?: CultureRouteConfig } })
    .__EQ_CONFIG?.cultureRoutes;
  return declared && Array.isArray(declared.prefixed) ? declared : null;
}

/** The prefix segment for a culture, or '' for the default. Case-insensitive: a URL somebody
 * typed is not a URL a link produced. */
function segmentFor(map: CultureRouteConfig, culture: string): string {
  return map.prefixed.find((p) => p.toLowerCase() === culture.toLowerCase()) ?? '';
}

/** Splits a path into the language it names and what is left — `/pt-BR/pricing` →
 * `['pt-BR', '/pricing']`, `/pricing` → `[default, '/pricing']`. */
export function splitCulturePath(path: string): [culture: string, rest: string] {
  const map = config();
  if (!map) return ['', path];
  const trimmed = path.startsWith('/') ? path.slice(1) : path;
  const slash = trimmed.indexOf('/');
  const head = slash < 0 ? trimmed : trimmed.slice(0, slash);
  const match = map.prefixed.find((p) => p.toLowerCase() === head.toLowerCase());
  if (!match) return [map.default, path.length === 0 ? '/' : path];
  return [match, slash < 0 ? '/' : trimmed.slice(slash)];
}

/**
 * The same path in another language. Idempotent — a path that already carries a prefix has it
 * REPLACED, never stacked, which is what makes it safe to call on a destination the caller did
 * not write.
 */
export function pathForCulture(culture: string, path: string): string {
  const map = config();
  if (!map) return path;
  const [, rest] = splitCulturePath(path);
  const segment = segmentFor(map, culture);
  if (segment.length === 0) return rest;
  return rest === '/' ? `/${segment}` : `/${segment}${rest}`;
}

/**
 * An in-app destination with the ACTIVE language on it. Only rooted app paths are touched:
 * `//cdn`, `https://`, `#anchor` and `mailto:` belong to someone else.
 */
export function localizeDestination(destination: string): string {
  if (!config()) return destination;
  if (destination.length === 0 || destination[0] !== '/' || destination.startsWith('//')) {
    return destination;
  }
  return pathForCulture(activeCulture().ui, destination);
}
