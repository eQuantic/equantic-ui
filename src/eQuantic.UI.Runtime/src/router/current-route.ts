import type { RouteMatch } from './route-table';
import { RouteValues } from '../shared/route-values';

/**
 * The router's view of the ambient route. The VALUE lives on `RouteValues` — the vocabulary's own
 * type, which a transpiled page names directly — and this is the router's way in and out of it.
 *
 * There used to be a `RouteData` interface here as well, mirroring a C# `RouteData` that mirrored
 * `RouteValues`. Both are gone: one route, one type, on both sides of the seam.
 */

export function setCurrentRoute(values: RouteValues): void {
  RouteValues.setCurrent(values);
}

export function getCurrentRoute(): RouteValues {
  return RouteValues.current;
}

/** Sets the current route from a router match + URL (params from the pattern, query from the URL). */
export function setCurrentRouteFrom(match: RouteMatch, url: URL): void {
  RouteValues.setCurrent(RouteValues.from(match.params, url.searchParams));
}
