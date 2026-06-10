/**
 * The client-side route table — generated from the server's `[Page]` attributes and injected into
 * `window.__EQ_CONFIG.routes`, so the runtime can resolve a URL to a page bundle without a round-trip.
 * Patterns use ASP.NET route-template syntax (`/users/{id}`, `/users/{id:int}`); the matcher captures
 * the named segments (constraints are accepted but not enforced client-side — the server validates).
 */
export interface RouteEntry {
  /** ASP.NET-style route template, e.g. `/` or `/users/{id:int}`. */
  pattern: string;
  /** The page component / bundle name to load for this route. */
  page: string;
  /** Document title for this route (from `[Page(Title = …)]`), applied on navigation. */
  title?: string;
}

export interface RouteMatch {
  page: string;
  /** Document title for the matched route, if any. */
  title?: string;
  /** Named route-segment values, decoded (empty when the pattern has no parameters). */
  params: Record<string, string>;
}

/** Splits a path into its non-empty segments (`/a/b/` → `['a','b']`, `/` → `[]`). */
function segments(path: string): string[] {
  return path.split('/').filter((s) => s.length > 0);
}

/** The parameter name of a `{name}` / `{name:constraint}` template segment, or null for a literal. */
function paramName(templateSegment: string): string | null {
  if (
    templateSegment.length < 2 ||
    templateSegment[0] !== '{' ||
    templateSegment[templateSegment.length - 1] !== '}'
  ) {
    return null;
  }
  const inner = templateSegment.slice(1, -1);
  const colon = inner.indexOf(':');
  return colon >= 0 ? inner.slice(0, colon) : inner;
}

/**
 * Matches `path` (a URL pathname, no query/hash) against a single route template, returning the captured
 * params or `null` when it doesn't match. Literal segments compare case-insensitively (ASP.NET routing is
 * case-insensitive); parameter segments capture exactly one (decoded) segment.
 */
export function matchPattern(pattern: string, path: string): Record<string, string> | null {
  const pat = segments(pattern);
  const seg = segments(path);
  if (pat.length !== seg.length) return null;

  const params: Record<string, string> = {};
  for (let i = 0; i < pat.length; i++) {
    const name = paramName(pat[i]);
    if (name === null) {
      if (pat[i].toLowerCase() !== seg[i].toLowerCase()) return null;
    } else {
      params[name] = decodeURIComponent(seg[i]);
    }
  }
  return params;
}

/**
 * Finds the first route that matches `path`. Static (parameterless) routes are tried before parameterised
 * ones so a literal like `/users/new` wins over `/users/{id}`.
 */
export function matchRoute(routes: readonly RouteEntry[], path: string): RouteMatch | null {
  const ordered = [...routes].sort(
    (a, b) => paramCount(a.pattern) - paramCount(b.pattern),
  );
  for (const route of ordered) {
    const params = matchPattern(route.pattern, path);
    if (params !== null) return { page: route.page, title: route.title, params };
  }
  return null;
}

function paramCount(pattern: string): number {
  return segments(pattern).filter((s) => paramName(s) !== null).length;
}
