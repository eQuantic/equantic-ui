# Spec Delta

## ADDED Requirements

### Requirement: Every pressable role reaches the native tree as its own role

Every `PressableRole` SHALL reach the native semantics tree as a `SemanticRole` of its own, with its
state where the web puts it: a radio's check as `Checked` (the web's `aria-checked`), a tab's and an
option's selection as `Selected` (`aria-selected`), a destination's place as `Current`
(`aria-current`), and nothing for a menu item. A role appended to `PressableRole` SHALL fail the
build until the walk names it.

#### Scenario: A picked tab

- **WHEN** a `Pressable` with `Role = PressableRole.Tab` and `Selected = true` is laid out on Photon
- **THEN** its semantic node's role is `SemanticRole.Tab` and its `Selected` is true, where the
  node's role was `Button`

#### Scenario: A chosen radio

- **WHEN** a `Pressable` with `Role = PressableRole.Radio` and `Selected = true` is laid out
- **THEN** its role is `SemanticRole.Radio`, its `Checked` is `On`, and its `Selected` is null

#### Scenario: The current destination

- **WHEN** a `Pressable` with `Role = PressableRole.Destination` and `Selected = true` is laid out
- **THEN** its role is `SemanticRole.Destination` and its `Current` is true

### Requirement: Each platform hears a pressable role in its own words

The bridges SHALL announce each pressable role with the platform's own role, AppKit's and Android's
taken from the W3C Core-AAM for the same ARIA role unless the row says why not: a radio as
`AXRadioButton` and `android.widget.RadioButton`; a tab as `AXRadioButton` with the `AXTabButton`
subrole and `android.view.View`; a menu item as `AXMenuItem` and `android.view.MenuItem`; an option
as `AXMenuItem` and `android.view.View`; a destination as `AXButton` and `android.widget.Button`. On
UIKit each SHALL carry the button trait, with the Selected trait for what is picked or current. No
pressable role SHALL be announced as static text on any platform.

#### Scenario: A tab on macOS

- **WHEN** the macOS bridge builds the accessibility element for a `SemanticRole.Tab` node, outside a
  running app
- **THEN** AppKit reads `AXRadioButton` as the element's role and `AXTabButton` as its subrole

#### Scenario: A radio on Android

- **WHEN** the Android bridge describes a `SemanticRole.Radio` node
- **THEN** its class name is `android.widget.RadioButton`, which TalkBack names a radio button, and
  it is checkable and checked as the radio is
