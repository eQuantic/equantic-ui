## 1. The vocabulary

- [x] 1.1 `ContentStyle` with `Merge`, `BoxStyle.Content`; `Text.Role`, `MaxLines`, `Align` and `Icon.Size` optional, with `Text.Under` and `Icon.SizeUnder`; `EffectiveTypeStyle.Resolve` takes the look in effect
- [x] 1.2 `VisualNode`'s conversion from `string`, and the runtime twin's `VisualNode.fromString`

## 2. The producers

- [x] 2.1 Photon: measure (and intrinsics) and paint carry the look down their walks
- [x] 2.2 The web: the server's lowering and the browser's, the browser's relowered records included
- [x] 2.3 Email

## 3. Proof, docs

- [x] 3.1 Tests on every producer: a box's role, lines, alignment and icon size taken; a text's own role kept whole; nested merge; nothing stated draws as before; a string as a node; a component lowered again alone. The conformance suite executed on both sides; the transpiled pins recorded again, and the served runtime measured inside its budget (it grew less than the 1% a change may add, so it records no delta)
- [x] 3.2 The wiki's page for the vocabulary in every language, a ledger entry; validate and archive
