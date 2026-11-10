# Tasks

## 1. Measured

- [x] 1.1 List every script and style the shell writes and what each varies by. Verify: the table in the design, read from `app-shell.html`, `UIExtensions.ServeAppShell`, `UseGtm`, both chart libraries and `AssetCollection`
- [x] 1.2 Read whether anything in the SDK caches the shell. Verify: no `Cache-Control` on the shell, and the state it carries is per visitor (#1288)

## 2. The nonce

- [x] 2.1 `ShellNonce`, one per request. Verify: two requests carry two nonces of 16 bytes
- [x] 2.2 `HtmlTag.Render(nonce)` and `IAsset.Render(nonce)` stamp a script or a style and nothing else. Verify: a link renders without it
- [x] 2.3 `HeadTags` holds `HtmlTag`s, the raw overload of `AddHeadTag` goes, and `UseGtm` and both chart libraries hand typed tags. Verify: the public API files declare the change, the removal among it
- [x] 2.4 The template's seven elements, the app's CSS and the page's assets carry it. Verify: `ShellNonceTests` reads every script and style start tag of a served page, in Production and Development, with SSR and without, and finds one nonce on all of them, and fails on `main`

## 3. Documentation

- [x] 3.1 The wiki's ServerIntegration page, in five languages, writes the theme-color meta as an `HtmlTag`
- [x] 3.2 One entry for the ledger citing #1266, as a file of its own under `docs/ledger/`
