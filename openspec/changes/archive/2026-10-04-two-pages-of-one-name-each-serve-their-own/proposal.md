# Proposal

Fixes #514: two pages with one simple name in two namespaces collided on the server.

## Why

- `ServerRenderingService` held every page under its simple type name, for a `[Page]` type and a
  `MapPage<T>` route alike, and each endpoint handed it that name. Two pages called `Dashboard` in two
  namespaces were one entry: the page registered last answered at both routes, with no error and no
  log line. Measured with two such pages in the server's test assembly: `/same-named/shop` rendered
  the admin's dashboard.
- In an app the shape is ordinary: an area's `Index` beside another's, or a page a library routes
  beside the app's own. Inside one project the build refuses two types of one name (EQ1005), since
  each becomes a module named after it, so the collision is the server's alone.

## What Changes

- Each endpoint carries its page's TYPE to the shell, the client navigation's state and the SSR, and
  the rendering service holds its pages by type: `RenderPageAsync`, `PreparePageAsync` and
  `IsSsrEnabled` take a `Type`. The client still receives the page's module name.
- **Break**: `IServerRenderingService` takes a page's `Type` where it took its name.

## Capabilities

### New Capabilities

- `page-routes`: a route renders the page declared for it, by the page's type.

### Modified Capabilities

(none)

## Impact

- **Server**: `ServerRenderingService`, `IServerRenderingService` and the page endpoints in
  `UIExtensions` (`MapPage<T>`, `MapPages`, the shell, the navigation state, the 404 and 500 pages).
