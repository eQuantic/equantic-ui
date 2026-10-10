# Proposal

#673 (under #288, the roadmap ahead), asked by Cura for its backoffice: a page cannot require
authorization. `[Authorize]` held on Server Actions only, so a page's route served anyone and its
`IServerPrefetch` ran for an anonymous visitor, writing its fields into the HTML.

## Why

An app guards a page today with its own middleware in Program.cs, and keeps the rule in its head for
every prefetch: one that forgets it leaks to anonymous visitors, and nothing fails. The page should
say what it requires, as an ASP.NET Core page or a Blazor component does.

## What Changes

- **A page's `[Authorize]` and `[AllowAnonymous]` guard its route.** The route a `[Page]` or a
  `MapPage<T>` declares carries them as endpoint metadata, which is what `RequireAuthorization` sets,
  and the app's authorization middleware decides with the app's own schemes and policies: an anonymous
  full load is challenged by the default scheme, a signed-in visitor without the policy gets 403, and
  the page is never built for a refused request, so its prefetch never runs.
- **A refused client navigation is a 401 or a 403**, marked with `X-EQ-Navigate`, never a challenge a
  fetch cannot follow and never a rendered page. The router loads the route in full on either, and the
  server then challenges or forbids as on any visit.
- **The SDK's own code serves anyone**: the runtime and the page modules allow anonymous access, so a
  sign-in page marked `[AllowAnonymous]` still comes alive under an app-wide fallback policy.

What a developer writes:

```csharp
[Page("/backoffice/queue")]
[Authorize(Policy = "Backoffice")]
public sealed class VerificationQueuePage : StatefulComponent, IServerPrefetch { … }
```

## Parts reached and surfaces moved

The server (page endpoints, an `IAuthorizationMiddlewareResultHandler` for navigations) and the
runtime's navigation (the page-state fetch and the boot). No public or developer surface moves: the
attributes are the ones the SDK already has, and the new types are internal.

## Migration

A class-level `[Authorize]` on a page guarded only its Server Actions; it guards the page now too. A
page that must stay public while its actions require a sign-in moves the attribute to the actions.
