# Proposal

Closes #1266, a Task under #306 (the Content-Security-Policy the SDK writes), slice 2 of 6.

## Why

A strict Content-Security-Policy admits an inline script or style only by a nonce or a hash, and
the shell gave it neither. On `main` the shell wrote seven inline elements of its own in
`Templates/app-shell.html` (the framework's and the theme's styles, the import map, the slot script
with the configuration, theme, culture and state, the boot module, and two scripts in Development),
the app's CSS from `AddStyles`, the head tags of its integrations (four inline scripts from `UseGtm`,
a CDN script and an initializer from each chart library) and each page's assets, the atomic sheet
among them. An app that wanted a policy had one choice, `'unsafe-inline'`, which admits an injected
script as readily as the SDK's own.

`HtmlShellOptions.AddHeadTag(string)` took markup as text, and `HeadTags` was a `List<string>`: a
head element the shell did not build was text the shell could not stamp.

## What Changes

- Every request has one nonce, 128 random bits, made the first time the request asks for it
  (`ShellNonce`).
- Every `<script>` and `<style>` the shell writes carries it: the template's seven elements, the
  app's CSS, every head tag and every page asset. `HtmlTag.Render(nonce)` and
  `IAsset.Render(nonce)` stamp it on a script or a style and on nothing else.
- **BREAKING** `HtmlShellOptions.HeadTags` is an `IReadOnlyList<HtmlTag>`, rendered per request.
  `AddHeadTag(string rawHtml)` is removed: a head element is an `HtmlTag`, which can express any
  element. `UseGtm` and both chart libraries hand the shell typed tags.
- **BREAKING** `IAsset.Render()` and `AssetCollection.RenderTags()` take the nonce.
- No header is written yet: that is #1267. The scripts a component writes inside the page through
  the escape hatch are not the shell's, and #1269 decides them.

## Capabilities

### New Capabilities

- `content-security`: what the SDK writes so that a Content-Security-Policy can admit its own
  scripts and styles and refuse everything else. This change adds its first requirement, and
  #1267 to #1270 add the rest.

### Modified Capabilities

(none)

## Impact

`src/eQuantic.UI.Server` (`ShellNonce`, `HtmlTag`, `HtmlShellOptions`, `Assets/`, `UIExtensions`,
`Templates/app-shell.html`), `src/eQuantic.UI.Gtm`, `src/eQuantic.UI.Charts.ChartJs`,
`src/eQuantic.UI.Charts.ApexCharts`, their public API files, and the wiki's ServerIntegration page,
whose example used the raw overload.
