# Design

## The policy

```text
script-src 'nonce-<request>' 'strict-dynamic'; object-src 'none'; base-uri 'none';
frame-ancestors 'self' <app>; connect-src 'self' <features> <app>[; report-uri <uri>]
```

- **Scripts by nonce and `'strict-dynamic'`, never by host.** Every script the shell writes carries
  the request's nonce (#1266), and `'strict-dynamic'` admits what those scripts load: the runtime's
  modules, GTM's container and its tags, a chart library from its CDN. A host list would be ignored
  beside `'strict-dynamic'` anyway, and is the part of a policy that is always wrong.
- **No `default-src`.** It would fall back onto `style-src`, `img-src` and `font-src`. Styles are not
  inside a policy until #1268, and images and fonts from other origins are an app's ordinary content,
  so the policy names the directives it means and leaves the rest open.
- **`connect-src 'self'` plus what was stated.** Server Actions, server events, uploads and the
  hot-reload relay (an `EventSource` at `/_equantic/hmr`) are all the app's own origin. GTM is the one
  integration that connects elsewhere, and it declares its hosts itself.
- **`frame-ancestors 'self'`.** Every page could be framed by any origin (#1265's review); the app
  names the partners that may.

## Who states what

- The app: `UseContentSecurityPolicy(csp => csp.AllowConnectionsTo(...).AllowFramingBy(...))` in
  `Program.cs`, and `EQuantic:ContentSecurityPolicy` for what varies per environment. Both lists add
  up, and the configuration's report URI wins over the code's.
- An integration: `options.ContentSecurity.AllowConnectionsTo(...)` when it is installed, whether or
  not the policy is on, so turning the policy on later needs nothing from the app. The options read
  the builder lazily, so a feature installed after `UseContentSecurityPolicy` is still in it.
- Sources are strings, as `AllowServerActionOrigins` takes, because a host source may start with `*.`
  (GTM's are `https://*.google-analytics.com` and two more), which `Uri` cannot hold. Each is
  validated at start against `scheme://host[:port][/path]`, so a policy string smuggled into one
  (`https://a; script-src *`, `'unsafe-inline'`) stops the app.

## dotnet watch

dotnet watch injects its browser-refresh script into every page it serves. The SDK did not write
it and cannot stamp it, so an enforced policy would refuse the reload dotnet watch exists for. Under
`DOTNET_WATCH=1` the policy is written report-only, and the start-up log says so. The SDK's
own relay needs nothing: it is the app's own origin.

## Where it is written

On pages only, by the shell, which holds the nonce. A policy something upstream already wrote wins,
and the log warns once, since it refuses the shell's scripts unless it admits the nonce. A policy governs a document, so a script or a
JSON response would carry it for nothing. The two headers that come with it go on every response,
through a startup filter like `PoweredByHeader`, and neither overwrites a value something upstream
wrote.

## Not here

- `style-src`: #1268.
- A strict policy refusing the escape hatch's inline handlers at build time: #1269.
- The proof in a real browser: #1270.
