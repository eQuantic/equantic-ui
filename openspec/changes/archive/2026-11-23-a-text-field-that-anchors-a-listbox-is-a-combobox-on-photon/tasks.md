# Tasks

## 1. The role

- [x] 1.1 Append `SemanticRole.EditableComboBox` to the enum, with its line in the per-change API file and in the enum ABI baseline
- [x] 1.2 The semantics walk: a `TextEntry` a listbox hangs from is the editable combobox, named, valued and expanded like the web's
- [x] 1.3 The native role table: `AXComboBox`, `android.widget.EditText`, no UIKit trait, activatable
- [x] 1.4 The testing reader: an input or a textarea with `role="combobox"` is the editable combobox, valued by its text, and `EnterText` types into it
- [x] 1.5 A merge takes the editable combobox as its control, as the web's merged reading takes a `combobox`
- [x] 1.6 Check: a label merged with the field reads alike on both targets, and fails without the merge's role; `EnterText` reaches the field's callback, and fails without its role; the semantics parity fixture compares the field open and closed on both targets, and fails without either half; AppKit reads back `AXComboBox`, "combo box", the field's text and its expanded state

## 2. The active option

- [x] 2.1 The anchored node keeps what its list last said (the field's position keeps its text), and the mark is said from the field's position
- [x] 2.2 The option is the row `ActiveIndex` counts to over the item rows in tree order, read from the layer's layout; a closed list or no active row says nothing
- [x] 2.3 Check: the announcement comes from the field as the keyboard moves, again when the list opens or the keyboard comes back, never from the option marked selected; the tests fail without the change and with the selected option in place of the active row

## 3. Where a person listens to it

- [x] 3.1 The desktop gallery's editor lists what its snippet names (`SpecimenSymbols`, the sample's own C#), so ⇧⌘O shows the field and its list on Photon; the README's gallery pictures retaken, unchanged
- [x] 3.2 Check: the code editor's own go-to on a Photon host, driven by ⇧⌘O and the arrows, is the editable combobox and announces each row from the field

## 4. Documentation

- [x] 4.1 One entry under `docs/ledger/`
