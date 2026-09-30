/**
 * Client twin of the C# `eQuantic.UI.Primitives.RouteValues` — what the route said, in a shape with
 * no target in it.
 *
 * It has to exist HERE because it exists there: every type in the Primitives namespace is routed by
 * eqc to `@equantic/runtime` implicitly (the shared vocabulary), so a page writing
 * `RouteValues.Current.Param("slug")` emits an import of this name. Without the export the page
 * dies at hydration on an unresolvable import while SSR keeps answering 200 with correct markup —
 * a failure that looks like nothing is wrong right up until the page is blank.
 *
 * It also owns the AMBIENT route, and that direction is deliberate. The router used to own a
 * `RouteData` and this class wrapped it, mirroring a C# `RouteData`/`RouteValues` pair that no
 * longer exists. Now the router SETS this and nothing here imports the router — the leaf holds the
 * state and the layer above registers into it, which is the shape this runtime already uses to keep
 * `core/*` and `shared/*` out of a cycle.
 */

import { bagEntries, type Bag } from '../utils/dictionary';

/**
 * A record with NO prototype, for keys that come from a URL.
 *
 * `pairs['__proto__'] = value` on a plain `{}` calls `Object.prototype`'s setter instead of
 * creating an own property, so the key silently disappears — and a query key is whatever a visitor
 * typed. Measured before the fix: `?__proto__=x` answered `null` here while the server answered
 * `x`. Found in review.
 *
 * Reading was already safe (`hasOwnProperty.call`); it is the WRITE that loses the key, which is
 * why a lookup guard was not enough.
 */
export function ownProperties(source?: Bag<string> | null): Record<string, string> {
  const safe: Record<string, string> = Object.create(null);
  if (source) for (const [key, value] of bagEntries(source)) safe[key] = value;
  return safe;
}

export class RouteValues {
  private readonly parameters: Readonly<Record<string, string>>;
  private readonly queries: Readonly<Record<string, string>>;

  /** Each side a dictionary when transpiled C# builds one, as its C# type says, or a plain object. */
  constructor(parameters?: Bag<string> | null, query?: Bag<string> | null) {
    // COPIED into prototype-less records rather than held: what a caller hands over may itself have
    // lost a key to the trap above, and holding it would also let a later mutation of the caller's
    // object change a route already handed to a page.
    this.parameters = ownProperties(parameters);
    this.queries = ownProperties(query);
  }

  /**
   * From a router match's params and a URL's search string — what the router has to hand.
   *
   * A REPEATED key keeps its FIRST value (`?tag=a&tag=b` is `a`). That is a policy rather than an
   * accident: `Object.fromEntries` would keep the last, `URLSearchParams.get` answers the first,
   * and the server used to hand over ASP.NET's comma-join — three answers to one question, none of
   * them written down. `RouteValuesQueryPolicyTests` holds both sides to this one.
   */
  static from(
    parameters: Record<string, string>,
    query: URLSearchParams | Record<string, string>,
  ): RouteValues {
    const pairs: Record<string, string> = Object.create(null);
    if (query instanceof URLSearchParams) {
      for (const [key, value] of query.entries()) {
        if (!Object.prototype.hasOwnProperty.call(pairs, key)) pairs[key] = value;
      }
    } else {
      for (const key of Object.keys(query ?? {})) pairs[key] = (query as Record<string, string>)[key];
    }
    return new RouteValues(parameters, pairs);
  }

  /** A matched route parameter — the `slug` of `/docs/{slug}`. Null when nothing matched it. */
  param(name: string): string | null {
    return Object.prototype.hasOwnProperty.call(this.parameters, name)
      ? this.parameters[name]
      : null;
  }

  /** A query-string value — the `page` of `?page=2`. A key repeated in the URL answers its FIRST
   * value, which is the policy `from` applies and the server applies too. */
  query(name: string): string | null {
    return Object.prototype.hasOwnProperty.call(this.queries, name) ? this.queries[name] : null;
  }

  /** Every matched parameter, by name — what a router hands on and a page rarely needs. */
  get params(): Readonly<Record<string, string>> {
    return this.parameters;
  }

  /** The route nothing matched — every lookup answers null (C# `RouteValues.Empty`). */
  static readonly empty = new RouteValues();

  private static active: RouteValues = RouteValues.empty;

  /**
   * The route for what is being rendered right now. On the server this is per-request state; here
   * there is one document, so it is whatever the router last set — seeded by boot from the initial
   * URL and updated before a page mounts, which is the same value the server built with.
   */
  static get current(): RouteValues {
    return RouteValues.active;
  }

  /** Set by the router, and by boot for the initial URL. */
  static setCurrent(values: RouteValues): void {
    RouteValues.active = values;
  }

  /** The C# `ClearCurrent` — a per-request seam the server needs and a document does not, so here
   * it puts the empty route back rather than doing nothing. */
  static clearCurrent(): void {
    RouteValues.active = RouteValues.empty;
  }
}
