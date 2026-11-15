import type { RouteMatch } from './route-table';
import { RouteValues } from '../shared/route-values';
import { splitCulturePath } from '../utils/culture-routes';

/**
 * The ROUTER's route: what it last matched, seeded by boot from the initial URL and set before a page
 * mounts, which is the same value the server built with. A page hands it to the owners its render
 * builds through (`pageContext`), and a component reads it from its context, which reads its owner's
 * frame (#1395): nothing in a tree reads it from here.
 *
 * There used to be a `RouteData` interface here as well, mirroring a C# `RouteData` that mirrored
 * `RouteValues`. Both are gone: one route, one type, on both sides of the seam.
 */

let current: RouteValues = RouteValues.empty;

export function setCurrentRoute(values: RouteValues): void {
  current = values;
}

export function getCurrentRoute(): RouteValues {
  return current;
}

/** Sets the current route from a router match + URL (params from the pattern, query and path from the URL). */
export function setCurrentRouteFrom(match: RouteMatch, url: URL): void {
  current = RouteValues.from(match.params, url.searchParams, splitCulturePath(url.pathname)[1]);
}
