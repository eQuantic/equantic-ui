## 1. The vocabulary

- [ ] 1.1 `TitlePart` in `eQuantic.UI.Primitives`: a `SingleChildNode`, node kind `titlePart`, its visitor method, its
  child slot, and the API declared in this change's file
- [ ] 1.2 The `TitlePart` factory in `UI`, and the runtime's twin (`vocabulary.ts`, `nodes.ts`, the node kinds, the
  exports)
- [ ] 1.3 Check: the coverage suites that enumerate the vocabulary by reflection (closed hierarchy, layout
  transparency, the Flutter parity probe, the email refusal parity) take the new node, each with the answer it gives

## 2. The web

- [ ] 2.1 `WebLoweringVisitor`: every `Overlay` and every anchored panel starts a scope, a part lowers to a semantics
  host with an id scoped by its path when a scope is open, and a modal open layer, or a dialog panel, lists the ids in
  `aria-labelledby`
- [ ] 2.2 The browser's twin in `lowering.ts`, with the scope on `LoweringContext`, so both producers write the same
  markup
- [ ] 2.3 The harness (`AnnouncedTree`) takes an `aria-labelledby` only when the text it gathers is not empty, as the
  accessible-name rule does
- [ ] 2.4 Check: the lowering fixture holds the two producers to the same markup, and a runtime spec reads the name in
  the browser's lowering

## 3. Photon and email

- [ ] 3.1 Measure and emit pass through the part; the semantics walk wraps it
- [ ] 3.2 `AnnounceOverlay` names the dialog's stop by the text under its parts, over `Label`, and by `Label` when the
  parts say nothing
- [ ] 3.3 The email walk draws the part's child
- [ ] 3.4 Check: `SemanticsParityTests` gains the cases of the spec, read on both targets, and each fails without the
  change

## 4. Documentation

- [ ] 4.1 The Flutter parity row for `namesRoute`
- [ ] 4.2 The wiki's accessibility page in every language it is written in, with a snippet `WikiClaimsCompile` compiles
- [ ] 4.3 One ledger entry citing #1916, as a file of its own under `docs/ledger/`
- [ ] 4.4 Archive the change into `screen-reader-parity` before the merge
