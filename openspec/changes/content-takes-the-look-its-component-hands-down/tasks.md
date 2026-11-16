## 1. The vocabulary

- [ ] 1.1 `ContentStyle` with `Merge`, `BoxStyle.Content`; `Text.Role`, `MaxLines`, `Align` and `Icon.Size` optional, with `Text.Under` and `Icon.SizeUnder`; `EffectiveTypeStyle.Resolve` takes the look in effect
- [ ] 1.2 `VisualNode`'s conversion from `string`, and the runtime twin's `VisualNode.fromString`

## 2. The producers

- [ ] 2.1 Photon: measure (and intrinsics) and paint carry the look down their walks
- [ ] 2.2 The web: the server's lowering and the browser's, the browser's relowered records included
- [ ] 2.3 Email

## 3. Proof, docs

- [ ] 3.1 Tests on every producer: a box's role, lines, alignment and icon size taken; a text's own role kept whole; nested merge; nothing stated draws as before; a string as a node; a component lowered again alone. The conformance suite executed on both sides; the transpiled pins and the served runtime's budget recorded again
- [ ] 3.2 The wiki's page for the vocabulary in every language, a ledger entry; validate and archive
