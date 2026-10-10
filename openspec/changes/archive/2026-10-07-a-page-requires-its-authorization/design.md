# Design

## How Flutter answers it

Flutter has no server. ASP.NET Core's pages and Blazor read `[Authorize]` from the endpoint's
metadata, which `RequireAuthorization` sets, and the authorization middleware enforces it before the
endpoint runs. Next.js answers with middleware matchers per route.

## Decisions

- **The framework's mechanism, not a home-grown check.** The page's attributes become endpoint
  metadata (the SDK's own attributes translated to ASP.NET Core's, since the vocabulary has no
  dependencies), so the app's schemes, policies, fallback policy and `UseAuthorization` order apply
  as they do to every other endpoint, and the page handler never runs for a refused request.
- **A navigation is answered by an `IAuthorizationMiddlewareResultHandler`.** A navigation is a fetch
  to the page's own route; a challenge is a redirect to another origin that a fetch cannot follow. The
  handler answers a navigation 401 (challenged) or 403 (forbidden), marked like every navigation
  answer, and leaves every other request to the framework's handler. An app that registers its own
  handler after `AddUI` replaces it.
- **The router loads a refused route in full** rather than rendering the page's empty state, so the
  server's own challenge or 403 is what the visitor meets.
