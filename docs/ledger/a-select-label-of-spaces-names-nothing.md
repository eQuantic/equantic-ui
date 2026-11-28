- **2026-11-28 · A select's label of nothing but spaces names nothing, and the factory hides a label**: after #1838 a
  `Select` labelled with spaces drew an empty line above its field and named its combobox with spaces, and `LabelHidden`
  could be set only with `new`, while every tree is written with factories. A blank label is now no label on both
  targets, `UI.Select` takes `labelHidden` as `UI.DatePicker` does, and the test of a hidden label checks that nothing is
  drawn above the field, where it could not fail before ([#1988](https://github.com/eQuantic/equantic-ui/issues/1988)).
  Proposed and archived through OpenSpec (`openspec/specs/screen-reader-parity`).
