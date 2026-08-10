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
 * `current` reads the router's live route, which boot seeds from the initial URL and the router
 * updates before mounting a page: the same values the server built with.
 */

import { getCurrentRoute, routeData, type RouteData } from '../router/current-route';

export class RouteValues {
  private readonly data: RouteData;

  constructor(
    parameters?: Record<string, string> | null,
    query?: Record<string, string> | null,
  ) {
    this.data = routeData(parameters ?? {}, query ?? {});
  }

  /** A matched route parameter — the `slug` of `/docs/{slug}`. Null when nothing matched it. */
  param(name: string): string | null {
    return this.data.param(name) ?? null;
  }

  /** A query-string value — the `page` of `?page=2`. */
  query(name: string): string | null {
    return this.data.query(name) ?? null;
  }

  /** The route nothing matched — every lookup answers null (C# `RouteValues.Empty`). */
  static readonly empty = new RouteValues();

  /**
   * The route for what is being rendered right now. On the server this is per-request state; here
   * there is one document, so it is the router's current route.
   */
  static get current(): RouteValues {
    const active = getCurrentRoute();
    const values = Object.create(RouteValues.prototype) as RouteValues;
    (values as unknown as { data: RouteData }).data = active;
    return values;
  }

  /** The C# `ClearCurrent` — a per-request seam the server needs and a document does not. */
  static clearCurrent(): void {}
}
