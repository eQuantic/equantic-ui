# Proposal

Closes #1319, a security fix tracked in a private advisory, and #1288, a Bug under #1174 (a sub-issue of
#157), both found by the security review of #1265.

## Why

The document the server sends for a page is written from one template, `app-shell.html`, and two
things about it let one visitor's data reach where it must not.

- **#1319.** `HtmlTemplateEngine.Render` filled each `{{Variable}}` over the WHOLE document, one
  variable after another, so a placeholder inside a value already written was filled again by a later
  variable. Text the document carries from elsewhere, the server-rendered markup among it, could be read
  as template.
- **#1288, medium.** The document went with no `Cache-Control`, while it carries what the visitor's
  cookies chose (theme, density, culture) and the page's own `window.__INITIAL_STATE__`. The same state
  answered to a client navigation was already `no-store`. A CDN or proxy rule that caches HTML would
  serve one visitor's prefetched data to the next.

## What Changes

- The template engine fills every placeholder of the TEMPLATE in one pass, a single regular expression
  replacement with a lookup, so text it wrote is never read again. A placeholder with no variable stays
  as it is, as before.
- A page's route answers `Cache-Control: no-store`, said once at the top of `ServeAppShell` for both of
  its answers: the document, whether the page was rendered on the server or not, the not-found and the
  error documents, and the state a navigation asks for. `no-store` rather than `private, no-cache`:
  moving between the app's pages is the client router's, not the browser's back/forward cache, and a
  document that carries a visitor's state is not kept anywhere.
- Reaches the Server alone (`HtmlTemplateEngine`, `UIExtensions`). Neither the public surface nor the
  developer surface moves, and an app writes nothing.

## Capabilities

### New Capabilities

- `served-document`: what the server writes in the document of a page, and how it is sent.

### Modified Capabilities

(none)
