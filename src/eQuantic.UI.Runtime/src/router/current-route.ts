import type { RouteMatch } from './route-table';

/**
 * Route data exposed to a page through `RenderContext.route` (C# `context.Route`): the matched route
 * parameters and the URL query string. Mirrors the C# `RouteData` API (`Param`/`Query`/`Params`) in
 * camelCase, so `context.Route.Param("id")` transpiles to `context.route.param("id")`.
 */
export interface RouteData {
  param(name: string): string | undefined;
  query(name: string): string | undefined;
  readonly params: Record<string, string>;
}

/** Builds a {@link RouteData} from captured params and a query (a `URLSearchParams` or plain record). */
export function routeData(
  params: Record<string, string> = {},
  query: URLSearchParams | Record<string, string> = {},
): RouteData {
  const q = query instanceof URLSearchParams ? query : new URLSearchParams(query);
  return {
    params,
    param: (name) => (Object.prototype.hasOwnProperty.call(params, name) ? params[name] : undefined),
    query: (name) => q.get(name) ?? undefined,
  };
}

// The current route, ambient for the duration of a render. The router updates it before mounting a page
// (and boot seeds it from the initial URL), so a component's `context.route` reflects the active URL —
// the client-side analogue of the server's per-request RouteData.
let current: RouteData = routeData();

export function setCurrentRoute(data: RouteData): void {
  current = data;
}

export function getCurrentRoute(): RouteData {
  return current;
}

/** Sets the current route from a router match + URL (params from the pattern, query from the URL). */
export function setCurrentRouteFrom(match: RouteMatch, url: URL): void {
  current = routeData(match.params, url.searchParams);
}
