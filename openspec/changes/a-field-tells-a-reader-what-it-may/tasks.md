# Tasks

## 1. The password field (#817)

- [x] 1.1 Write the dots into the node and add `Obscured`, in `SemanticNode` and `SemanticsVisitor`
- [x] 1.2 Map it on Android (`Password`) and macOS (`AXSecureTextField`); iOS reads the dots as the value
- [x] 1.3 Prove it both ways: `AnObscuredField_HoldsDotsInTheTree_AndSaysSo` fails with the visitor as it stood and passes
- [ ] 1.4 Check it on a real platform: `uiautomator dump` on the Android emulator shows `password="true"`, or leave it to the owner with the reason

## 2. The field a listbox hangs from (#1088)

- [x] 2.1 Append `SemanticRole.EditableComboBox` and its `NativeRole` row, `AXComboBox` and `android.widget.EditText`
- [x] 2.2 Make the first text field of a listbox's anchor one, with `Expanded`, in `SemanticsVisitor`
- [x] 2.3 Mark the item row an open menu's or listbox's keys are on as a polite live region, and announce it as it changes
- [x] 2.4 Prove it both ways: the two `ComboBoxAndDialogSemanticsTests` cases and the parity case fail with the tree as it stood; the come-back and the live-node assertions each fail without their own line
- [ ] 2.5 Check it with VoiceOver on macOS in the dashboard's code editor, or leave it to the owner with the reason

## 3. Documentation

- [x] 3.1 One ledger entry under `docs/ledger/` per Bug, citing #817 and #1088; the wiki states no gap here, so no page changes
