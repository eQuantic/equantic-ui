- **2026-11-09 · A Server Action is authorized by the rule a page is**: Bugs from the security review
  (#1265), [#1285](https://github.com/eQuantic/equantic-ui/issues/1285),
  [#1290](https://github.com/eQuantic/equantic-ui/issues/1290) and
  [#1291](https://github.com/eQuantic/equantic-ui/issues/1291). An action was authorized by a rule of its
  own, which read only the SDK's `[Authorize]`, ignored the schemes and the fallback policy, and ran an
  ASP.NET Core-protected page's actions for an anonymous POST; a page and an action are decided by one rule
  now (`RequestAuthorization`). An action's id is its component's CLR full name on both sides, and two
  methods under one id stop the app at start. The endpoint takes JSON only, caps every body
  (`EQuantic:ServerActions:MaxRequestBodySize`), keeps its reasons in the log, answers malformed arguments
  with 400, refuses an `UploadedFile` inside a value, and writes the declared result. Measured on main, 16
  of the 20 new tests fail there. Proposed and archived through OpenSpec (`server-actions`).
