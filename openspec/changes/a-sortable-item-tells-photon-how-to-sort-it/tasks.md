## 1. The vocabulary and the walk

- [ ] 1.1 `SemanticNode.Hint`, appended, with its documentation.
- [ ] 1.2 `SemanticsVisitor`: `Visit(Sortable)` carries the list's words to its children, `Visit(SortableItem)` opens
  the hint for an enabled item, and the pressable, link and adjustable stops take it.
- [ ] 1.3 Check: `SortableHostTests` read the hint off the semantics tree for an item's stop, in the SDK's words and in
  an app's own, and none outside an item, on a field, for a disabled item or for a list with no words. Each fails
  without its rule.

## 2. The shells

- [ ] 2.1 macOS sets `accessibilityHelp`, iOS `AccessibilityHint`, Android `TooltipText` from API 28.
- [ ] 2.2 Check: `AppKitAccessibilityTests` reads the help back through the real dispatch, and takes it away when the
  node no longer has one. The iOS and Android shells build.

## 3. Records

- [ ] 3.1 Public API in the branch's files; the ledger entry; `docs/FLUTTER-PARITY.md`'s sortable row loses #1596.
- [ ] 3.2 The wiki's Sortable section loses the #1596 scope note, in five languages.
- [ ] 3.3 Check: the affected suites, `check-openspec.sh`.
