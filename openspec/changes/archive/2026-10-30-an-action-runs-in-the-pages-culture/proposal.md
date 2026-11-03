## Why

Closes #580, a bug under #206 (the fences of v1 to revisit), and #825, a bug under #565 (the transpiler's
fences hold on every path, continued).

A `POST /api/_equantic/actions` made from `/pt-BR/playground`, with `Accept-Language: en` and no culture
cookie, ran its handler in English: the endpoint has no culture segment, so the route that rendered the
page answers nothing there, and the cookie or the browser chooses. A Portuguese page got an English error
back, and a `pt-BR` browser on a bare URL got the opposite. Switching in the browser to an app's neutral
language asked for `/_equantic/strings/en.json`, a 404 with an error in the console, on the way to
`neutral.json`: the page does not know which catalogs the build wrote.

## What Changes

- Every Server Action call names the culture of its page, in the `culture` and `ui-culture` query keys.
  ASP.NET's own `QueryStringRequestCultureProvider` reads them ahead of the cookie and `Accept-Language`,
  so the app's `UseRequestLocalization` applies the page's culture, a culture the app does not support is
  refused there as any other, and a page with no culture installed keeps today's order.
- The shell's culture data names the catalogs the build wrote, and a switch walks exact culture, parents
  and `neutral` as before, asking only for those. A host with a catalog loader of its own answers for
  itself. An app with no catalog asks for nothing.
- For a developer using the SDK: nothing to write. A handler answers in the language of the page that
  called it, and its numbers and dates follow the page's format culture.

## Capabilities

### New Capabilities

(none)

### Modified Capabilities

- `server-actions`: gains the requirement "A Server Action runs in the culture of its page".
- `page-culture`: gains the requirement "A switch asks for no catalog the build did not write".

## Impact

- **The runtime**: `ServerActionsClient` and the culture store; `boot.ts` hands the list over.
- **The server**: `CultureBridge` writes `catalogs` into `window.__EQ_CULTURE__`.
- **eqc, the realizers, the Photon shells, the SDKs, the templates**: none.
- **Public surface**: none. The developer surface does not move.
