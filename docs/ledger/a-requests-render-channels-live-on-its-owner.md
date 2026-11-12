- **2026-11-11 · A render's channels are handed to it**: the web's style, gradient and face sinks, the
  expansion scope, the reported density and the link policy were six `AsyncLocal`s the server armed
  around a render, which any code in the request's flow could read or overwrite. They are one
  `RenderChannels` now, made for each prefetch round and each discovery and handed to
  `MarkupHost.Lower`. The escape hatch's bridge, inside an `HtmlElement`'s `Render()`, which takes no
  parameter, reads them where the server arms them around such a page, the one web channel left in the
  flow. Every `AsyncLocal` the source keeps is named with its reason in a list that may only shrink, and
  two requests drawn at once each write their own page. The SSR fixtures and the hydration payload are
  unchanged ([#1404](https://github.com/eQuantic/equantic-ui/issues/1404), under
  [#1094](https://github.com/eQuantic/equantic-ui/issues/1094)). Proposed and archived through
  OpenSpec (`openspec/specs/element-tree`).
