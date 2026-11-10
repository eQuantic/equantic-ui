# Tasks

## 1. Measured

- [x] 1.1 Read where the SDK's features connect. Verify: Server Actions, server events, uploads and the hot-reload relay (`/_equantic/hmr`) are the app's own origin, and GTM is the one integration that connects elsewhere
- [x] 1.2 Read what dotnet watch does to a page. Verify: it injects a script the SDK did not write, which no nonce admits

## 2. The policy

- [x] 2.1 `ContentSecurityPolicyBuilder`, `ContentSecurityPolicyOptions` bound from `EQuantic:ContentSecurityPolicy`, and the validator. Verify: a malformed source, a source carrying a directive, and a quoted keyword each stop the app
- [x] 2.2 `ContentSecurityPolicy` writes the header on pages with the request's nonce, report-only when asked or under dotnet watch. Verify: the strict policy's exact text, with the document's nonce
- [x] 2.3 `UseGtm` declares its hosts and hands its container the nonce. Verify: the hosts are in `connect-src` whether GTM is installed before or after the policy
- [x] 2.4 `SecurityHeaders` on every response. Verify: a script response carries `nosniff` and no policy

## 3. Documentation

- [x] 3.1 The developer surface baseline names `config EQuantic:ContentSecurityPolicy`
- [x] 3.2 The wiki's ServerIntegration page, in five languages
- [x] 3.3 One entry for the ledger citing #1267, as a file of its own under `docs/ledger/`
