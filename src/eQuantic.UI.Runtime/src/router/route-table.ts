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

interface ParamSegment {
  name: string;
  /** Lower-cased inline constraint (`int`, `guid`, …), or null when unconstrained. */
  constraint: string | null;
}

/** Parses a `{name}` / `{name:constraint}` template segment, or returns null for a literal. */
function paramSegment(templateSegment: string): ParamSegment | null {
  if (
    templateSegment.length < 2 ||
    templateSegment[0] !== '{' ||
    templateSegment[templateSegment.length - 1] !== '}'
  ) {
    return null;
  }
  const inner = templateSegment.slice(1, -1);
  const colon = inner.indexOf(':');
  return colon >= 0
    ? { name: inner.slice(0, colon), constraint: inner.slice(colon + 1).toLowerCase() }
    : { name: inner, constraint: null };
}

/**
 * The inline route constraints we mirror client-side so a value that the server would reject (and 404)
 * doesn't match here either. Unknown/parameterised constraints (e.g. `minlength(2)`, `regex(...)`) are
 * accepted — the server remains the source of truth for those.
 */
const CONSTRAINT_TESTS: Record<string, RegExp> = {
  int: /^-?\d+$/,
  long: /^-?\d+$/,
  bool: /^(true|false)$/i,
  double: /^-?\d+(\.\d+)?$/,
  float: /^-?\d+(\.\d+)?$/,
  decimal: /^-?\d+(\.\d+)?$/,
  guid: /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i,
  alpha: /^[a-z]+$/i,
};

function satisfiesConstraint(value: string, constraint: string | null): boolean {
  if (!constraint) return true;
  const base = constraint.split('(')[0]; // strip args, e.g. minlength(2) → minlength
  const test = CONSTRAINT_TESTS[base];
  return test ? test.test(value) : true; // unknown → accept; server validates
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
    const param = paramSegment(pat[i]);
    if (param === null) {
      if (pat[i].toLowerCase() !== seg[i].toLowerCase()) return null;
    } else {
      const value = decodeURIComponent(seg[i]);
      if (!satisfiesConstraint(value, param.constraint)) return null;
      params[param.name] = value;
    }
  }
  return params;
}

/**
 * Finds the first route that matches `path`. Static (parameterless) routes are tried before parameterised
 * ones so a literal like `/users/new` wins over `/users/{id}`.
 */
export function matchRoute(routes: readonly RouteEntry[], path: string): RouteMatch | null {
  const ordered = [...routes].sort((a, b) => paramCount(a.pattern) - paramCount(b.pattern));
  for (const route of ordered) {
    const params = matchPattern(route.pattern, path);
    if (params !== null) return { page: route.page, title: route.title, params };
  }
  return null;
}

function paramCount(pattern: string): number {
  return segments(pattern).filter((s) => paramSegment(s) !== null).length;
}
