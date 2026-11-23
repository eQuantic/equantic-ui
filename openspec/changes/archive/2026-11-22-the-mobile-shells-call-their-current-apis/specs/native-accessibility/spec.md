## ADDED Requirements

### Requirement: A mixed check on Android is announced without a state

The Android shell SHALL make a checked or unchecked check checkable, with its state, and SHALL leave a mixed
check uncheckable, so TalkBack announces it with no state rather than as off. From Android 16 the node SHALL
carry its state as `CheckedState`, a mixed check as partial.

#### Scenario: TalkBack on Android 16

- **WHEN** TalkBack focuses three checks, on, off and mixed, on the Android 16 emulator
- **THEN** it reads "checked", "not checked", and the mixed one with its name and role and no state

### Requirement: Android's nodes come from the constructors current from API 30

The Android shell SHALL create its accessibility nodes, range information and events with the constructors
Android added in API 30, and with the `Obtain` factories only below API 30.

#### Scenario: TalkBack walks the screen

- **WHEN** TalkBack's next-item key walks a screen from its first node on the Android 16 emulator
- **THEN** it reads every node in tree order with the same words as before this change
