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

export class RouteValues {
  private readonly parameters: Readonly<Record<string, string>>;
  private readonly queries: Readonly<Record<string, string>>;

  constructor(parameters?: Record<string, string> | null, query?: Record<string, string> | null) {
    this.parameters = parameters ?? {};
    this.queries = query ?? {};
  }

  /** From a router match's params and a URL's search string — what the router has to hand. */
  static from(
    parameters: Record<string, string>,
    query: URLSearchParams | Record<string, string>,
  ): RouteValues {
    const pairs =
      query instanceof URLSearchParams
        ? Object.fromEntries(query.entries())
        : { ...(query ?? {}) };
    return new RouteValues(parameters, pairs);
  }

  /** A matched route parameter — the `slug` of `/docs/{slug}`. Null when nothing matched it. */
  param(name: string): string | null {
    return Object.prototype.hasOwnProperty.call(this.parameters, name)
      ? this.parameters[name]
      : null;
  }

  /** A query-string value — the `page` of `?page=2`. */
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
