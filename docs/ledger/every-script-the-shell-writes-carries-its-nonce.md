- **2026-11-09 · Every script and style the shell writes carries the request's nonce**: a strict
  Content-Security-Policy admits an inline element only by a nonce or a hash, and the shell gave its
  own neither: the template's seven elements, the app's CSS, GTM's four scripts, a chart library's
  CDN script and initializer, and every page's assets, the atomic sheet among them. Each request now
  has one nonce of 128 random bits, stamped on every script and style the shell writes, chosen over
  hashes by measure: a hash cannot admit a CDN script the SDK does not serve, and five of the
  elements change with every response anyway. `HtmlShellOptions.HeadTags` holds `HtmlTag`s rendered
  per request, and `AddHeadTag(string)` is gone, since text the shell did not build is text it
  cannot stamp. Where an app wrote `AddHeadTag("<meta name=\"theme-color\" content=\"#3b82f6\">")` it
  writes `AddHeadTag(HtmlTag.Meta("theme-color", "#3b82f6"))`, and a favicon needs no head tag at
  all: the icon declared once as an `IAppIcon` writes every favicon link (#1240)
  ([#1266](https://github.com/eQuantic/equantic-ui/issues/1266)). Proposed and archived through
  OpenSpec, as the first requirement of `content-security`.
