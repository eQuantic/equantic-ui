- **2026-11-27 · A select is named by its label, on the web and on Photon**: a `Select` had no way to be named, and a
  combobox is never named from its content, so every select was announced with no name on every target (VoiceOver
  said "pop up button, Lisbon"), which fails WCAG 4.1.2. `Select` takes a `label`, as `TextInput` does: drawn above the
  field and left out of the reading, and handed to the trigger, so the field is a combobox named by it with the choice
  as its value on both targets. `LabelHidden` keeps the name for a form that draws its own label. The dashboard
  sample's select carries one ([#1838](https://github.com/eQuantic/equantic-ui/issues/1838)). Proposed and archived
  through OpenSpec (`openspec/specs/screen-reader-parity`).
