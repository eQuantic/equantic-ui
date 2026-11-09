# Tasks

## 1. Links

- [x] 1.1 `LinkScheme` and its twin `allowsScheme` read a scheme as a browser does, and `RenderContext.ResolveDestination` and `writtenDestination` answer null for a scheme an href never carries. Verify: `LinkSchemeTests` and `link-scheme.spec.ts`, the same table on both sides
- [x] 1.2 A `Link` and a run write no `href` for it. Verify: the parity case `links-with-schemes`, a Link, a run and a Markdown link, the same on both producers
- [x] 1.3 `MarkdownParser`'s documentation says where a link's target is checked

## 2. Style elements

- [x] 2.1 `StyleElementText` writes every `</` as `\3c /`, and the atomic sheet, the theme's sheet and the app's styles go through it. Verify: `StyleElementTextTests`, the atomic value, a case-folded end tag and an app's own styles

## 3. Proof and documentation

- [ ] 3.1 Each new case fails on the code as it was
- [ ] 3.2 The Web, Server, Compiler, Conformance and runtime suites pass whole
- [x] 3.3 The wiki's Components, Markdown and Styling pages in all five languages, on the wiki branch named like this one
- [x] 3.4 One ledger entry for each issue, as files of their own under `docs/ledger/`
