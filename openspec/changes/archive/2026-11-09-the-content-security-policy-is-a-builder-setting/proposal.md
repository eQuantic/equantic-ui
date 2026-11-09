# Proposal

Closes #1267, a Task under #306 (the Content-Security-Policy the SDK writes), slice 3 of 6. It builds
on #1266, which stamps the request's nonce on every script and style the shell writes.

## Why

There was no way to state a Content-Security-Policy in eQuantic.UI. An app that wrote the header by
hand had to know what the shell, its integrations and its relays need, which is a platform
incantation the product principle rules out, and the only way it could admit the shell's scripts
was `'unsafe-inline'`, which admits an injected script as readily.

## What Changes

- `UIOptions.UseContentSecurityPolicy(csp => …)` turns the policy on. It is the strict one:
  `script-src 'nonce-…' 'strict-dynamic'`, `object-src 'none'`, `base-uri 'none'`,
  `frame-ancestors 'self'` and `connect-src 'self'`, written on every page with the nonce its scripts
  carry.
- The app states only what its own code needs, as C#: `AllowConnectionsTo(...)`,
  `AllowFramingBy(...)`, `ReportOnly()`, `ReportViolationsTo(uri)`. Nothing takes a policy string.
- What varies per environment is read from `EQuantic:ContentSecurityPolicy` through the standard
  binder: `ReportOnly`, `ReportUri`, `ConnectSources` and `FrameAncestors`. A source the policy could
  not write stops the app at start.
- The SDK adds what its own features need. `UseGtm` declares the hosts Google's guidance names for
  GTM and GA4, and its snippet hands the container the page's nonce. Server events, Server Actions,
  uploads and the hot-reload relay are the app's own origin. Under `dotnet watch`, whose injected
  script no nonce admits, the policy is written report-only. The first page logs the policy it
  writes.
- `X-Content-Type-Options: nosniff` and `Referrer-Policy: strict-origin-when-cross-origin` come with
  the policy, on every response.
- `style-src` is not written yet: the runtime's own style elements and the realizer's style
  attributes come inside a policy with #1268, which adds the directive.

## Capabilities

### New Capabilities

(none)

### Modified Capabilities

- `content-security`: the policy itself, stated in C# and completed by the SDK.

## Impact

`src/eQuantic.UI.Server` (`UIOptions`, `ContentSecurityPolicyBuilder`, `ContentSecurityPolicyOptions`,
its validator, `ContentSecurityPolicy`, `SecurityHeaders`, the shell), `src/eQuantic.UI.Gtm`, the
public API file, the developer surface baseline (`config EQuantic:ContentSecurityPolicy`), and the
wiki's ServerIntegration page.
