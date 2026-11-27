# Proposal

Closes #1838, a Bug under #1587.

## Why

A `Select` with no label around it was announced with no name on every target. The web read it as a combobox whose value is the choice it shows, and since #1328 Photon reads it the same way: VoiceOver said "pop up button, Lisbon", and TalkBack a spinner with no description. A combobox with no accessible name fails WCAG 4.1.2 (name, role, value). The component offered no way to name it: its trigger is a `Pressable` with no `Label`, and a combobox is never named from its content. An app that drew a label beside it had no way to tie the two together.

## What Changes

- **`Select` takes a `label`**, as `TextInput` does. It is drawn above the field, in the input slot's label role and gap, and it reaches the trigger's `Pressable.Label`. That makes it `aria-label` on the web and the combobox's name on Photon, with the choice the field shows as its value. The drawn label is left out of the reading, so the words are heard once.
- **`LabelHidden`** leaves the label out of the drawing for a form that lays its labels out itself, as `TextInput.LabelHidden` does, and the field still names itself by the same words.
- The `UI.Select` factory takes `label` too, and the runtime's twin is transpiled again.
- The dashboard sample's select carries a label ("Payout account").

The surface changes in one place: `Select`'s constructor and `UI.Select` gain a last parameter, `label`, and the old signatures go. `Label` and `LabelHidden` are added.

## Impact

- `eQuantic.UI.Components`: `Select` and `UI`, and the runtime's transpiled twins.
- `samples/DefaultUIDashboard`'s payments page, and its README picture.
- `openspec/specs/screen-reader-parity`.
