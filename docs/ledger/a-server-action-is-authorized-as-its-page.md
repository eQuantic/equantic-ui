- **2026-11-09 · A Server Action is authorized by its page's rules**: a security fix in Server Actions,
  tracked in a private advisory ([#1320](https://github.com/eQuantic/equantic-ui/issues/1320)), with
  [#1290](https://github.com/eQuantic/equantic-ui/issues/1290) and
  [#1291](https://github.com/eQuantic/equantic-ui/issues/1291) from the security review (#1265). A page and
  a Server Action are decided by one rule (`RequestAuthorization`): the SDK's and ASP.NET Core's attributes,
  their schemes, the app's fallback policy and the request as the resource. An action's id is its
  component's CLR full name on both sides, and two methods under one id stop the app at start. The endpoint
  takes JSON only, caps every body (`EQuantic:ServerActions:MaxRequestBodySize`), keeps its reasons in the
  log, answers malformed arguments with 400, refuses an `UploadedFile` inside a value, and writes the
  declared result. Proposed and archived through OpenSpec (`server-actions`).
