## Why

Closes #1286 and #1295, two Bugs the security review (#1265) found in what the web writes without
escaping it.

- #1286 (medium): no URL scheme was checked anywhere. A `Link`'s or a `TextRun`'s destination was
  written to `href` as it came, and `MarkdownParser` keeps a link's target as written, so a reader's
  Markdown reached `href` with whatever scheme it carried, in SSR and on the client.
- #1295 (low): the server writes the atomic sheet as `<style id="eq-atomic">…</style>` with every
  `property:value` as it came, and a string of the `HtmlStyle` escape hatch reaches it, so a value could
  end the element it was written in. The theme's sheet, with the faces' rules, and an app's own styles in
  the shell were written the same way.

## What Changes

- The web realizer and its TypeScript twin write a link's destination only when it has no scheme (a
  path, relative or rooted, a query, a fragment, a protocol-relative address) or its scheme is `http`,
  `https`, `mailto` or `tel`, and write the anchor with no `href` for any other. The scheme is read as
  a browser's URL parser reads it, with its letter case and the characters that parser ignores folded
  away. One rule per target, at the one place each
  resolves a destination (`RenderContext.ResolveDestination`, which now answers `string?`, and
  `writtenDestination` in `utils/link-scheme.ts`), so a `Link`, a run and a Markdown link all go
  through it.
- Photon is unchanged: a pressed link hands its destination to the app's `PhotonHost.NavigationRequested`,
  and is inert while none is set; an `IUrlLauncher` the app opens it with honours its `OpenUrlPolicy`.
- The `HtmlElement` escape hatch is unchanged: its `Href` is the DOM's attribute, written as the app
  wrote it.
- Every `</` in the text of a style element the server writes is written `\3c /`, which CSS reads as
  the same two characters inside a string or a `url()` and which a selector or a media query never
  holds. `StyleElementText` is the one writer: the atomic sheet (`InlineStyleAsset`), the theme's sheet
  and an app's own styles go through it. An atomic class's hash is taken before, so the classes the
  server writes and the client computes stay the same.
- Breaking, for a contract assembly: `RenderContext.ResolveDestination(string)` returns `string?`. An
  app that called it reads null for a destination no href carries.

## Capabilities

### New Capabilities

(none)

### Modified Capabilities

- `links`: a link writes an href only for a scheme it may carry.
- `document-theme`: nothing a style element holds can end the element.
