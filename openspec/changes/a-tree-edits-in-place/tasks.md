## 1. The blocks

- [ ] 1.1 `Navigable.FocusVersion` and `TextEntry.SelectOnFocus` in Primitives, with every registration a member has
- [ ] 1.2 The tree's measures in `ListTheme`, merged

## 2. The tree

- [ ] 2.1 `TreeRowState`, `RowBuilder` and `Style`; the row reads the merged slot
- [ ] 2.2 `TreeEditRequest`, F2, the field, `OnRenamed` and `OnCreated`, busy while the task runs, the parent read again
- [ ] 2.3 `TreeReloadRequest` and `BusyKeys`
- [ ] 2.4 `MenuBuilder` and `OnMenuSelect`: a target per row, one menu anchored to its row

## 3. Web

- [ ] 3.1 The keyboard's context request follows `aria-activedescendant`
- [ ] 3.2 The composite answers only its host's keys, and honours a focus version; the entry selects on focus

## 4. Photon

- [ ] 4.1 The keyboard's context request follows the focused composite's active cell
- [ ] 4.2 A focus version moves the host's focus; a field selects on focus; a field's keys never reach the composite

## 5. Proof, sample, docs

- [ ] 5.1 Tests on both targets by the keyboard: rename, create, cancel, busy, menu, reload, builder and style
- [ ] 5.2 The dashboard's `/files` tree renames, creates, deletes and opens its menu
- [ ] 5.3 The wiki's Components page in every language, a ledger entry; archive
