- **2026-11-23 · A text field a listbox hangs from is an editable combobox on Photon, and the option the
  keyboard is on is announced from it**: the code editor's go-to field read as a plain text field on Photon,
  where the web's text control is the combobox with the list's active option on it (#1083). It reaches the
  tree as the new `SemanticRole.EditableComboBox`, `AXComboBox` and `android.widget.EditText` (Core-AAM's
  pair), named by its label, valued by its text, saying whether its list is open. The row `ActiveIndex`
  counts to is announced politely from the field as it changes, and again when the list opens or the
  keyboard comes back to it; the anchored node keeps what it said, because the field's position keeps its
  text. The testing reader hears the web's input as the same role, so the semantics parity fixture compares
  the field open and closed. AppKit reads back "combo box". On Android the option is not heard yet: TalkBack
  reads a change only from a live region's node
  ([#1088](https://github.com/eQuantic/equantic-ui/issues/1088)). Proposed and archived through OpenSpec
  (`openspec/specs/native-accessibility`).
