- **2026-10-31 · A field a list hangs from is a combobox on Photon, and its option is heard**: a Bug ([#1088](https://github.com/eQuantic/equantic-ui/issues/1088)).
  The code editor's go-to field is a `TextEntry` anchoring a listbox, which the web makes the combobox, with
  `aria-activedescendant` naming the option the arrows reach. On Photon it read as a plain text field, and nothing
  read `Anchored.ActiveIndex`, so moving through the list said nothing. `SemanticRole` gains `EditableComboBox`,
  which AppKit hears as `AXComboBox` and Android as `EditText`, and the row an open menu's or listbox's keys are
  on is a polite live region while it is the one, announced as it changes, in a `Select`, a `TimePicker` and a
  `Menu` as well. Proposed and archived through OpenSpec (`native-accessibility`).
