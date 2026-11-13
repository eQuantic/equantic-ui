# Proposal

Closes #1395 (E5b), split from #1095 (E5), under #937, in the 1.0 milestone. It builds on E4a (#1403,
merged), which draws a write-once page on the server through an owner for the request, and on E3b-1
(#1453), which builds the browser's trees through the element engine.

## Why

E5's core (#1412) gave the engine inherited components, `DependOn` and `Find`, and a context per
component position that reads the frame's theme, density and type scale and registers the position as
their reader. On the web and the server three things still answer from outside the tree:

- the route, from an `AsyncLocal` (`RouteValues.Current`) the server arms per request, which a
  navigation cannot invalidate selectively;
- `InFlow`, from an `AsyncLocal` the web visitor arms around a component it expands without a position
  (`InFlow.Current`);
- and in the browser, `dependOn` and `find` answer null, because the type the lookup asks for is
  erased on its way to JavaScript.

## What Changes

- **The lookup crosses.** eqc lowers `context.DependOn<T>()` and `Find<T>()` to a call with `T`'s
  twin (`context.dependOn(T)`), and the browser's position looks it up by `instanceof`, as C# does with
  `is T`. An app's inherited value is read the same way on every target.
- **The route is the frame's**, as the theme is, with a reader set of its own. `ComponentContext`'s
  public constructor takes the route a frame hands, a position's `Route` reads its owner's frame and
  registers the position, and an owner handed a frame with another route builds the positions that read
  the route and nothing else, so a navigation within one page type rebuilds only what read the route. The
  server hands its request's route to the owner it draws with; the browser hands the router's; a tree
  built with no route answers `RouteValues.Empty`.
- **`RouteValues.Current` goes.** Code outside the tree that needs the request's route (a prefetch,
  which is handed the request's services) resolves `RouteValues` from them: the server registers it per
  request. An escape-hatch page's bridges are drawn with their page's route too, handed by whatever draws
  them.
- **`InFlow.Current` goes.** A position answers `InFlow` from the tree, as it does since #1412; a
  component the web visitor expands without a position is built with the flag the visitor carries, and no
  ambient channel is armed.
- **`Theme` is an inherited component**, the vocabulary's: a subtree under a `Theme` reads its theme, and
  a component's `context.Theme` is the nearest one above it, or the frame's when none is. Nothing in the
  SDK places one yet; E6a-2 (#1387) puts one over a subtree. Every read registers, the frame's included,
  so no slot, key or fixture moves.

For someone using the SDK:
- `RouteValues.Current.Param("slug")` in a prefetch becomes
  `services.GetRequiredService<RouteValues>().Param("slug")`;
- `context.Route` is unchanged;
- `InFlow.Current` was the SDK's own and leaves the public surface.

The migration lines go in the wiki's Upgrading page, and the wiki's examples change with it in their five
languages.

Parts reached:
- eqc (a strategy for the lookup);
- Primitives (`ComponentContext`, `BuildOwner`, `RouteValues`, `InFlow`, a new `Theme`);
- the web realizer;
- the server;
- the runtime (the context, the page context, the router, the twins);
- the tests, the wiki.

The public surface moves:
- `RouteValues.Current` and `RouteValues.ClearCurrent` go;
- `InFlow.Current` goes;
- `ComponentContext`'s constructor takes a route;
- `Theme` arrives.

No developer-surface line moves.
