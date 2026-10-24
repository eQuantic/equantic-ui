# Tasks

## 1. One type per file, before anything moves

- [ ] 1.1 `IAppTheme.cs`, `UIExtensions.cs` and `TokenCss.cs` split into one file per type, with no other change, and their entries leave the one-type-per-file baseline
- [ ] 1.2 `PhotonCssGenerator` becomes `ThemeStylesheet`, and the citations of the moved lines in the documentation follow them
- [ ] 1.3 Proof: the solution builds, and the Web, Server and Native suites pass unchanged

## 2. The theme names the document

- [ ] 2.1 `IAppTheme` gains `TextFamily`, `Selection`, `OnSelection`, `Placeholder`, `ScrollbarThumb` and `ScrollbarTrack`, with the defaults the SDK draws with today
- [ ] 2.2 The runtime's theme carries them: the bridge, the `AppTheme` twin, the interface defaults and Photon's generated theme
- [ ] 2.3 `EffectiveTypeStyle.WithThemeFace` fills a proportional style with `TextFamily` and a monospaced one with `MonoFamily`
- [ ] 2.4 Proof: the interface-defaults guard and the theme bridge's round trip, both failing before the slots exist, and the face tests on both targets

## 3. The web writes the document from the theme

- [ ] 3.1 The theme's stylesheet writes the body, the selection, every field's placeholder and the page's scrollbars from the theme, and `--eq-font-family` from `TextFamily`
- [ ] 3.2 The head holds the invariants, the theme's stylesheet, the app's own styles and the app's head tags, in that order, and a page with no chosen theme carries Photon's stylesheet
- [ ] 3.3 `SetBaseStyles` and `BaseStyles` go, and `AddStyles` appends the app's own CSS
- [ ] 3.4 Proof: the shell's head read back from a served page, in order, with and without a theme, and with an app's `AddStyles`

## 4. Every target draws the slots

- [ ] 4.1 A field's placeholder, a table's scrollbars (Photon, and the web's lowering on both sides of the parity line) and the code editor's selection read the slots
- [ ] 4.2 Photon sets text with no face of its own in `TextFamily`
- [ ] 4.3 Proof: the goldens unchanged under every theme that sets no slot, a theme that sets them drawn from them on both targets, and the hydration contract held for the table's scrollbars

## 5. The sample and the real thing

- [ ] 5.1 The dashboard drops its `SetBaseStyles`, and looks as it did: checked in a browser, then its README pictures taken again, looked at, and recorded
- [ ] 5.2 A page with a brand theme checked in a browser: its body, selection, placeholder and scrollbars read from the theme, and its own `AddStyles` rule winning

## 6. Documentation

- [ ] 6.1 The wiki's DesignSystem and ServerIntegration pages, in every language they are written in, on the wiki branch named like the pull request's
- [ ] 6.2 One ledger entry citing #1038, as a file of its own under `docs/ledger/`
- [ ] 6.3 Archive this change in the same pull request
