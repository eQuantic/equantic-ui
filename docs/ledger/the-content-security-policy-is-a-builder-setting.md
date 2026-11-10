- **2026-11-09 · The Content-Security-Policy is a builder setting**: `ui.UseContentSecurityPolicy()`
  writes the strict policy on every page, `script-src` by the request's nonce and `'strict-dynamic'`,
  `object-src` and `base-uri` refused, framing and connections to the app's own origin, with
  `X-Content-Type-Options: nosniff` and `Referrer-Policy` on every response. The app states only the
  origins its own code connects to or is framed by, in C# (`AllowConnectionsTo`, `AllowFramingBy`)
  and per environment in `EQuantic:ContentSecurityPolicy`, where report-only and the report URI live
  too, and a source that is not one stops the app at start. The SDK adds what its features need:
  `UseGtm` declares Google's hosts and hands its container the nonce, and under `dotnet watch`, whose
  own script no nonce admits, the policy reports and refuses nothing. `style-src` waits for #1268
  ([#1267](https://github.com/eQuantic/equantic-ui/issues/1267)). Proposed and archived through
  OpenSpec, into `content-security`.
