# Proposal

Closes #575, a Bug under #208 (The audit continues).

## Why

Every page route and the modules under `/_equantic/` were mapped with `MapGet`, so a HEAD fell
through to the fallback and answered 404 where the GET answered 200. Measured in the 0.2.0-preview.60
release proof, on a fresh `dotnet new equantic-app` published in Release: `HEAD /` answered 404. HTTP
defines HEAD as GET without the content (RFC 9110, section 9.3.2), and an uptime monitor, a link
checker or a crawler that asks with HEAD read every page of an app as missing.

## What Changes

- **A page and a module answer HEAD as they answer GET.** A `[Page]` route and its language-prefixed
  twin, a `MapPage<T>` route, `/_equantic/runtime.js`, `/_equantic/{name}.js` and its source map are
  mapped for GET and HEAD, with the GET's handler. Kestrel writes no body for a HEAD, so the answer is
  the GET's status and headers and nothing else.
- A HEAD for a path nothing serves stays the fallback's 404.

What does not move: the developer's C#, the public surface and the developer surface.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `page-routes`: a new requirement, a page answers HEAD as it answers GET.

## Impact

- `eQuantic.UI.Server`: `UIExtensions`, which maps the routes.
- The wiki's ServerIntegration page, in English and Portuguese.
