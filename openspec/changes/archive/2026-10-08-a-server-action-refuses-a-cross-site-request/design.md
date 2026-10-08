# Design

## How others answer it

Flutter has no server and no endpoint of its own, so `docs/FLUTTER-PARITY.md` has no row for this.
The frameworks that do have server actions answer it the same way: Next.js compares a Server
Action's `Origin` with its `Host` and allows `serverActions.allowedOrigins`,
and SvelteKit checks the origin of a form action (`csrf.checkOrigin`). ASP.NET Core's own answer for
forms is an antiforgery token, which needs a token in every page and every request. The SDK writes
both ends of the call, but a token would have to be minted per session, rendered into the page and
carried by the runtime, for a protection the browser already gives in two headers.

## The rule

1. **With an `Origin`**, the request runs when the origin's host and port are the app's own host, or
   the origin is one the app allows. Any other origin is refused, the opaque `null` included (a
   sandboxed frame, a `file:` page), which names no site an app could allow.
2. **Without an `Origin`**, `Sec-Fetch-Site` decides: `cross-site` and `same-site` are refused, and
   `same-origin` and `none` run.
3. **With neither header**, the request runs. Every browser the SDK supports sends `Origin` on a
   `POST`, so such a request is a server or a tool calling the endpoint, which cross-site forgery is
   not about.

The scheme is not compared, only the host and the port, with a scheme's default port left out on both
sides: a proxy that ends TLS hands the app an `http` request for an `https` page, and comparing the
scheme would refuse every action behind it.

## The app's own host

It is the request's `Host`. Behind a proxy that rewrites it, ASP.NET Core's `UseForwardedHeaders`
restores it from the proxies the app trusts, which is .NET's own answer and the only safe one: the raw
`X-Forwarded-Host` is never read, since a site the app's CORS policy lets through a preflight could
write its own host into it (found by the self-review). An app that cannot use the forwarded headers
lists its public origin instead.

## Configuration

`ServerActionsOptions` is bound from `EQuantic:ServerActions`, like `ServerEventsOptions` from
`EQuantic:ServerEvents`, so the origins that vary per environment live in `appsettings.json`, and
`UIOptions.AllowServerActionOrigins` adds to them from `Program.cs`. Each origin is validated at
start: a value that is not an absolute `http` or `https` URI with a host and nothing else (no path,
no query) stops the app, naming the value, since a list that silently matched nothing would refuse
the very page it was written for.

## The answer

A refused request gets 403 and the endpoint's usual error body, before the body is read, the action
resolved or its authorization asked. The log says which origin or which `Sec-Fetch-Site` was refused,
at warning level, so an app that forgot an origin finds it in one line.

## The proof

- Server tests drive the middleware with each case of the rule: the same host, another site, `null`,
  a default port written or left out, a forwarded host, an allowed origin, `Sec-Fetch-Site` alone, and
  neither header, and assert that a refused action never ran.
- A start-up test shows that a malformed allowed origin stops the app.
- The dashboard sample's own actions still run in a browser, which sends `Origin` on each of them.
