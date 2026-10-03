# Tasks

## 1. The tree

- [x] 1.1 Append `TabBar`, `RadioGroup`, `ComboBox`, `Dialog` and `AlertDialog` to `SemanticRole`, 17 to 21, and refresh the enum pin and the runtime's generated union
- [x] 1.2 Map every `AdjustableRole` in the walk with no default arm: a slider stays one stop with its value, a tab list and a radio group announce their container and keep walking
- [x] 1.3 Announce the pressable a listbox panel hangs from as the combobox, through component seams, and an open modal layer as a dialog or an alert dialog
- [x] 1.4 Mark the layer the realizer opens for an anchored panel modal only when its panel is a dialog
- [x] 1.5 Check: `GroupRoleSemanticsTests` and `ComboBoxAndDialogSemanticsTests` read the tree a real `Tabs`, `RadioGroup`, `SegmentedControl`, `Select`, `TimePicker`, `Menu`, `DatePicker` and `Dialog` produce, roles, names, states and order, and fail against the old walk; the tests that pinned a slider and a group now pin the new roles

## 2. The bridges

- [x] 2.1 Give `NativeRole` a row per new role, AppKit's and Android's words from Core-AAM, the combobox in each platform's select-only words, the reason in each row
- [x] 2.2 Check: `AppKitAccessibilityTests` builds the macOS elements from real components through the real walk and reads each role, subrole, description and state back from AppKit; `NativeRoleTests` pins the rows and holds every control the frame makes reachable, a container by what it holds, to what it advertises
- [x] 2.3 Check: `AnnouncementParityTests` lowers each component to the DOM and lays it out on Photon, and the two announcements are one list

## 3. The handoff

- [x] 3.1 Mark `native-roles` shipped in `docs/design/status.json`, say what the native tree carries in the Foundations §10 table, and follow the code with the audit's B13 row and citations
- [x] 3.2 Check: `HandoffStatusTests` passes with the entry shipped and failed while it said partial; the docs citation guards pass

## 4. Documentation

- [x] 4.1 The wiki's Photon page, English and Portuguese
- [x] 4.2 One `docs/LEDGER.md` line citing the issues
