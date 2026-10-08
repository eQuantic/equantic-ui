# Proposal

Closes #678, a Feature under #306 (A formal security hardening review, and CSP guidance the SDK
writes for the developer).

## Why

A Server Action is a `POST` to the SDK's action endpoint, and nothing on that endpoint asks where the
request came from. The middleware reads the body whatever its `Content-Type`, so another site can
send a "simple" request, a form post or a `fetch` in `text/plain`, which a browser sends without a
CORS preflight. An anonymous action runs for it as it runs for the app's own page, and an authorized
one does too wherever the session cookie is not `SameSite=Lax` or `Strict`. Cura met it: its public
forms submit through anonymous actions and its backoffice through authorized ones, and it would have
to write its own middleware comparing `Origin` and `Sec-Fetch-Site` with the host.

## What Changes

- **The action endpoint refuses a request from another site, on by default.** A browser says where a
  request came from: in `Origin`, which it sends on every `POST`, and in `Sec-Fetch-Site`. The
  endpoint answers 403 before the action runs when the `Origin` is not the app's own host (or is the
  opaque `null`), or, with no `Origin`, when `Sec-Fetch-Site` says `cross-site` or `same-site`. A
  request with neither header is not a browser's and runs as before.
- **An app allows named origins**, for a page served from another domain than its actions: in
  `appsettings.json` under `EQuantic:ServerActions:AllowedOrigins`, or in `Program.cs` with
  `AddUI(options => options.AllowServerActionOrigins("https://admin.example.com"))`. An origin that
  is not a bare `scheme://host[:port]` stops the app at start, saying which one.
- **The app's own host** is the request's `Host`, which ASP.NET Core's `UseForwardedHeaders` restores
  behind a proxy the app trusts. A raw `X-Forwarded-Host` is never read.

## Parts reached

- The server: `ServerActionsMiddleware`, a new `ServerActionsOptions` bound from configuration and
  validated at start, and `UIOptions`.
- eqc, the runtime, the realizers, the Photon shells and the templates do not change: the browser
  already sends both headers on its own.
- The public surface grows (`ServerActionsOptions`, `UIOptions.AllowServerActionOrigins`), and so
  does the developer surface (the `EQuantic:ServerActions` section).

## The break

A request from a page on another origin than the app's host is refused where it used to run. An app
whose pages call its actions from another domain lists that origin in
`EQuantic:ServerActions:AllowedOrigins`.
