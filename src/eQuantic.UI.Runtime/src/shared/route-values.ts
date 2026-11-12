/**
 * Client twin of the C# `eQuantic.UI.Primitives.RouteValues` — what the route said, in a shape with
 * no target in it.
 *
 * It has to exist HERE because it exists there: every type in the Primitives namespace is routed by
 * eqc to `@equantic/runtime` implicitly (the shared vocabulary), so a page naming it
 * (`RouteValues route = context.Route;`) emits an import of this name. Without the export the page
 * dies at hydration on an unresolvable import while SSR keeps answering 200 with correct markup —
 * a failure that looks like nothing is wrong right up until the page is blank.
 *
 * It keeps no route of its own. The router keeps what it matched (`router/current-route.ts`) and
 * hands it to the owners a page builds through, and a component reads its route from its context,
 * which reads its owner's frame (#1395). Nothing here imports the router, which keeps `core/*` and
 * `shared/*` out of a cycle.
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
}
