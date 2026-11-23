# Proposal

Closes #1775 and #1778, two sub-issues of the epic #1774 ("Lessons from a real site: what fails only in the
browser is caught by the build and the tests").

## Why

A real site's pages passed every test of the server's HTML and broke only once they hydrated: a section
whose labels the browser drew blank, an icon size the browser refused, a string a static field kept in the
first request's language, and a path the server rewrote to its catalogue while the browser's router gave it
to the home page, which drew empty after a language change. Each was found by opening the page in a
browser, and the last one said nothing at all, even in development.

## What Changes

- **A served page is hydrated in a test** (#1775). `eQuantic.UI.Testing` gains `HydrationAssert.PageAsync(factory,
  "/catalogue")`. It serves the app on a port of the machine (`WebApplicationFactory.UseKestrel`), loads the
  page as a browser does in happy-dom (the DOM the runtime's own tests run in), runs its scripts against the
  server's markup, and throws a `HydrationException` naming what went wrong: an error the page reported, a
  text, an attribute or an element the browser drew differently from the server, a page the browser drew
  over, a missing string, and the browser's router choosing another page than the server drew. It runs
  under the embedded bun the app was built with, which the package's build targets find from the app the
  test project references: a test project references the app and the package, and installs nothing else.
- **Hydration records what it wrote over.** The reconciler kept the client's text and attributes where the
  server wrote others, in silence (#1302); it now records each difference, and an element only the server
  wrote, in the hydration's result. A label the browser draws empty, which was not even written over (the
  empty text is no child, and the server's text stayed in the document), is recorded too. The boot writes
  what the first load did on `window.__EQ_HYDRATION__`. Classes in another order and the client's own
  `data-eq-*` markers are no difference.
- **The browser says when its router chose another page** (#1778). At load, and after each client
  navigation, the page the server drew (the document's configuration, and now the navigation's payload,
  `page`) is compared with the one the router resolves for the URL; in development a different one is an
  error in the console that names both and points to the docs. Production says nothing.
- **An app of pages alone builds.** The SDK's prune of stale generated sources evaluated
  `GetLastWriteTime('')` when the generated folder held nothing but the resource accessors (which are
  compiled, so excluded), and failed the build of an app with no component of its own.

What an app writes: in its test project, a reference to the app and to `eQuantic.UI.Testing`, and
`await HydrationAssert.PageAsync(factory, "/catalogue")`. Nothing else, in the test or the app.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `component-testing`: a served page is hydrated in a test.
- `hydration-contract`: hydration records what the browser drew differently.
- `page-routes`: the browser's router and the server choose the same page.

## Impact

- **The runtime**: `reconciler.ts`, `renderer.ts`, the components' `hydrate`, `page-state.ts`, a new
  `router/page-agreement.ts`, and the boot.
- **The server**: the navigation payload carries `page`.
- **The SDK**: `ResolveBunPath` returns the bun's path; the generated-sources prune is guarded.
- **`eQuantic.UI.Testing`**: `HydrationAssert`, `HydrationReport`, `HydrationDifference`, `HydrationOptions`,
  `HydrationException`; a harness bundled with happy-dom (MIT, its licence packed beside it); a
  `buildTransitive` targets file; a dependency on `Microsoft.AspNetCore.Mvc.Testing`.
- **Public surface**: added to `eQuantic.UI.Testing` only; nothing removed. The developer surface does not
  move.

No break.

## Principles

The product principle: a test project installs no toolchain (no Node, no npm, no browser), and the bun is
the one the app's build already resolved. The structural fix over the patch: the bun's path is declared by
the SDK's own target, not searched for.
