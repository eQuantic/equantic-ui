# Proposal

Closes #338, a Bug under #207 (Handoff fidelity: verify and fix the visible deviations).

## Why

The vocabulary names nine pressable roles and the native semantics walk knew three of them:
`Checkbox`, `Switch` and `GridCell` reached a `SemanticRole` of their own, and every other pressable
went to `_ => Button`. So VoiceOver on macOS and iOS and TalkBack on Android announced a radio, a
tab, a menu item, a list option and a navigation destination as a button, where the web said
`radio`, `tab`, `menuitem`, `option` and `aria-current` for the same components. The handoff's
Foundations §10 table records the five as "role lost", and its `native-roles` request asks for the
members.

## What Changes

- **`SemanticRole` gains `Radio`, `Tab`, `MenuItem`, `Option` and `Destination`**, appended at 12
  to 16, so every value a released app compiled in keeps its meaning.
- **The walk maps every pressable role, with no default arm.** A radio carries its check (On or
  Off, the web's `aria-checked`), a tab and an option their selection (`aria-selected`), a
  destination where the user is (`aria-current`), and a menu item nothing. A role appended to
  `PressableRole` fails the build by name until the walk says what the tree calls it.
- **`NativeRole` speaks each in the platform's own words.** AppKit's and Android's are the W3C
  Core-AAM's for the same ARIA role, which is what WebKit and Chrome expose for the web half of the
  component: a radio is `AXRadioButton` and `android.widget.RadioButton`, a tab `AXRadioButton`
  with the `AXTabButton` subrole and a plain `android.view.View`, a menu item `AXMenuItem` and
  `android.view.MenuItem`. An option is `AXMenuItem`, departing from Core-AAM's `AXStaticText`,
  which would read a control as a paragraph, and a destination keeps a button's words on all three.
  UIKit has a role for none of them and keeps the button trait, with the Selected trait for what
  is picked or current.
- **`NativeRole` gains an `AppKitSubrole` column**, which the macOS bridge sets.
- **The macOS bridge reaches its classes through `AppKit.Class`**, the shell's own door, which
  loads AppKit first. `objc_getClass` answered nil wherever no window had loaded it, and building
  these elements outside a running app hung on a root class that answered nothing.

## Impact

- **Developer surface:** none. An app writes the same `Pressable { Role = … }`, and its menus,
  selects, time pickers, navigation bars and rails are announced in each platform's words.
- **Public surface:** five `SemanticRole` members (additions), and `NativeRole`'s constructor and
  `Deconstruct` widened by `AppKitSubrole`, the old shapes retired as `*REMOVED*`. Nothing an app
  writes constructs a `NativeRole`, so no migration line is owed.
- **Parts reached:** Primitives (the enum), Native.Components (the walk and the table), the macOS
  shell (the subrole and the class lookups). The iOS and Android bridges read the table and do not
  change.
- **The handoff:** `docs/design/status.json` marks `native-roles` partial, five of its seven
  members shipped; a combobox and a dialog are still to come.
