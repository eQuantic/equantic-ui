# Design

## The question: a nonce per request, or hashes for the shell's fixed scripts

A policy admits an inline element by a nonce the response carries or by the hash of its content.
A nonce is made per response. A hash can be computed ahead for a script that never changes, and a
policy of hashes alone does not change between responses, so it survives a cache that stores a
page. #306's review asked which one the shell takes, decided by measure.

## Measured on `main` (2026-11-09)

What each element the shell writes varies by:

| Element | Varies by | Hashable ahead |
|---|---|---|
| The framework's invariant `<style>` | nothing | yes |
| The theme's `<style>` | the theme, the mode cookie, the served fonts | no: per request |
| The import map | the build id | per build |
| The configuration and state `<script>` | the page, the density and mode cookies, the culture, the page's state | no: per request |
| The boot module | nothing | yes |
| The two Development scripts | the build id, SSR, Server Actions | per build |
| The app's CSS (`AddStyles`) | the app's configuration | per deployment |
| GTM's four scripts | the app's configuration | per deployment |
| A chart library's CDN `<script src>` | a file the SDK does not serve | no |
| An app's `AddScript(uri)` | a file the SDK does not serve | no |
| A page's assets, the atomic sheet among them | the page and what it drew | no: per response |

Who caches the shell: nothing in the SDK does. The shell is written with no `Cache-Control`, and it
carries the visitor's own state, so a shared cache must never hold it, with or without a policy
(#1288, which sends it `no-store`). An app that puts a cache in front anyway stores the header with
the body (ASP.NET Core's output cache and CDNs do), so the nonce in the header still matches the
body. It is reused, which the CSP specification advises against, and that reuse is the same
exposure caching the visitor's state already is.

## Decision: a nonce per request, on every script and style

- Hashes cannot admit every element. A hash admits an external script only through that script's
  `integrity` attribute, and the SDK cannot know the hash of a file it does not serve: a chart
  library's CDN script, or an app's `AddScript` URL. A nonce admits them as it admits the rest.
- Hashes would not survive a cache either. Three of the elements change per request or per response,
  so their hashes would be computed per response, as a nonce is, and the policy would change with
  every page anyway.
- One mechanism, not two. A policy of a nonce for some elements and hashes for others is two things
  to keep true, and the elements move between the columns as the shell changes.
- The cost is 16 random bytes and their base64 per request, on a path that already builds the page.

## How

- `ShellNonce.Of(HttpContext)` makes the nonce on first use and keeps it in the request's items.
  It is internal: the header (#1267) reads it from the same place, and nothing outside the SDK
  needs to.
- `HtmlTag.Render(nonce)` and `IAsset.Render(nonce)` stamp it on a `script` or a `style` and on
  nothing else, through one writer of the attribute (`ShellNonce.Attribute`).
- `HtmlShellOptions.HeadTags` holds `HtmlTag`s and the shell renders them per request. The raw
  overload of `AddHeadTag` goes: text the shell did not build is text it cannot stamp, and
  `HtmlTag` can express any element.
- The template's own elements carry `nonce="{{Nonce}}"`, and the nonce is the first variable the
  shell sets.

## Not here

- The header itself: #1267.
- The styles the runtime creates in the browser, and the `style` attributes the realizer writes:
  #1268.
- A `<script>` a component writes inside the page through the escape hatch (`ChartJsScripts`, the
  chart markers, `DynamicElement`) is not the shell's: #1269 decides what a strict policy does with
  them, and #1270 proves the result in a browser.
