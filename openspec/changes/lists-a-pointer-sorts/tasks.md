## 1. The vocabulary

- [ ] 1.1 `Sortable`, `SortableItem` and `SortDrop`, each in its own file; two visits on `IVisualNodeVisitor`; `ChildSlots`
- [ ] 1.2 `Sortable` and `SortableItem` factories in `UI.cs`; `./scripts/public-api.sh update`, additions only
- [ ] 1.3 Check: the solution builds, every visitor naming the two nodes

## 2. The web realizer

- [ ] 2.1 C# `LowerSortable` and `LowerSortableItem`: the marks on the child, the custom events declared
- [ ] 2.2 TS twin: `lowerSortable`, `lowerSortableItem`, `SortDrop` in `value-types.ts`, `nodes.ts`, `vocabulary.ts`, node kinds regenerated
- [ ] 2.3 `dom/sortable.ts`: pick-up thresholds, the copy that follows, placement by boxes, nesting, the guard, hover reports, auto-scroll, Escape, the swallowed click
- [ ] 2.4 Check: vitest drives a board through pointer events (in-list, across lists, refused, nested, Escape, press without travel), each failing without the behaviour it names; the parity fixture holds a sortable column

## 3. Photon and email

- [ ] 3.1 Measure, emit and semantics pass the child through; the email walks into it
- [ ] 3.2 Check: `LayoutTransparencyTests` and the Photon suite pass

## 4. Sample and docs

- [ ] 4.1 The dashboard's board: cards move inside and across columns, with an indicator where a card would land
- [ ] 4.2 Wiki Components page in every language; `docs/FLUTTER-PARITY.md` row; ledger entry
- [ ] 4.3 Check: the board in a browser, moved by a mouse

## 5. Archive

- [ ] 5.1 `./scripts/check-openspec.sh` passes, then `openspec archive lists-a-pointer-sorts --yes`
