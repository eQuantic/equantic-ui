# Tasks

## 1. The tree

- [x] 1.1 Append `Radio`, `Tab`, `MenuItem`, `Option` and `Destination` to `SemanticRole`, 12 to 16
- [x] 1.2 Map every `PressableRole` in the walk with no default arm: a radio's check, a tab's and an option's selection, a destination's current-ness
- [x] 1.3 Check: `PressableRoleSemanticsTests` enumerates every pressable role and its state, each new arm proved both ways; the enum pin records the appended values

## 2. The bridges

- [x] 2.1 Give `NativeRole` an `AppKitSubrole` column and a row per new role, AppKit's and Android's words from Core-AAM
- [x] 2.2 Set the subrole in the macOS bridge, and reach its classes through `AppKit.Class`, once a build
- [x] 2.3 Give `NativeRole` a `UIKitCheckAsSelected` column, true for a radio, which the iOS bridge reads for the trait and the value (found in review)
- [x] 2.4 Check: `AppKitAccessibilityTests` builds the elements through the real dispatch on a Mac and reads each role and subrole back from AppKit, failing without the subrole; `NativeRoleTests` pins the rows and proves every control the frame makes reachable affords what it advertises

## 3. The handoff

- [x] 3.1 Mark `native-roles` partial in `docs/design/status.json`, and say what the native tree carries in the Foundations §10 table
- [x] 3.2 Check: `HandoffStatusTests` passes with the entry partial and failed while it said request

## 4. Documentation

- [x] 4.1 The wiki's Photon page, English and Portuguese
- [x] 4.2 One `docs/LEDGER.md` line citing the issue
