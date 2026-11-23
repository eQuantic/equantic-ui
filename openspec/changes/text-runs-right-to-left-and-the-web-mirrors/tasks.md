## 1. The vocabulary

- [x] 1.1 `TextDirection` enum (`LeftToRight = 0`, `RightToLeft = 1`) in `eQuantic.UI.Primitives`, and the `EnumValueAbiTests` pin
- [x] 1.2 `Directionality : InheritedComponent` (`Direction`, `UpdateShouldNotify` on a change), its TS twin in `vocabulary.ts` registered as `Theme` is, and `UI.Directionality(direction, child)`
- [x] 1.3 `ComponentContext.TextDirection` in C# and `textDirection` in `photon-context.ts`: the nearest `Directionality`, else the UI culture's, read as a dependency
- [x] 1.4 `PublicAPI.Unshipped.<branch>.txt` through `./scripts/public-api.sh update`

## 2. The culture

- [x] 2.1 `app-shell.html` and `ServeAppShell`: `dir` beside `lang`, from `CurrentUICulture.TextInfo.IsRightToLeft`
- [x] 2.2 The culture JSON (`BuildCultureData` and `/_equantic/culture/{name}.json`) carries `rightToLeft`; `installCulture` and a switch set `document.documentElement.dir`

## 3. The web, both producers

- [x] 3.1 `Directionality` lowers to a `display: contents` host with `dir` (C# `Visit(InheritedComponent)`, TS `case 'inherited'`)
- [x] 3.2 Asymmetric padding and border widths as `*-inline` / `*-block`, symmetric ones unchanged (`TokenCss.Padding`, `SideWidths` and the TS twins); `HtmlStyle` gains the logical members it needs
- [x] 3.3 `Positioned.Start`/`End` as `inset-inline-start`/`-end`; `ShiftX` through `--eq-inline`, declared by the token sheet; the anchored centre placement physical
- [x] 3.4 `TableViewport`, `SheetSurface` and `CodeSurface` hosts carry `dir="ltr"`
- [x] 3.5 `component-parity` fixture regenerated (`EQ_UPDATE_PARITY_FIXTURE=1`), with new cases: a padded box and a positioned child under `Directionality(RightToLeft)`, and a shifted centre

## 4. Components and the sample

- [x] 4.1 `CodeEditor`, `CodeBlock`, `Mermaid` and the spreadsheet's entry lines build inside `Directionality(LeftToRight)`
- [x] 4.2 `samples/DefaultUIDashboard`: `ar` among the supported cultures and the alternates, `Strings.ar.resx`

## 5. Proof and docs

- [ ] 5.1 Tests: the shell's `dir` for `ar` and `en`; the context's direction from the culture and from a `Directionality` on both targets; the lowered properties under each direction; the browser's `dir` after a culture switch
- [ ] 5.2 The dashboard in Arabic, driven in a real browser: the page mirrored (a padded, start-aligned row and a start-positioned child measured from the right)
- [ ] 5.3 `docs/FLUTTER-PARITY.md` row SAME for the web, `FlutterParityPinTests` probe; `docs/I18N-PLAN.md` D9 says where RTL went; ledger entry; the wiki in five languages
