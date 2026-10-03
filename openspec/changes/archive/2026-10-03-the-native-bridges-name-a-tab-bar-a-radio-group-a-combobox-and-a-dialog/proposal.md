# Proposal

Closes #500 and #501, two Bugs under #207 (Handoff fidelity: verify and fix the visible deviations).

## Why

On Photon a `Tabs` and a `RadioGroup` reached VoiceOver and TalkBack as one unnamed slider: the
semantics walk announced every `Adjustable` as `SemanticRole.Slider`, whatever its role, and the
slider consumed the tabs and radios inside it, so neither their names nor which one is picked was
ever read (#500). A `Select`'s field reached the bridges as a button that expands, and an open modal
layer as a plain group with a name (#501). The web says `tablist`, `radiogroup`, `combobox` and
`dialog` for the same components, and the handoff's `native-roles` request still owed the combobox
and the dialog.

## What Changes

- **`SemanticRole` gains `TabBar`, `RadioGroup`, `ComboBox`, `Dialog` and `AlertDialog`**, appended
  at 17 to 21, so every value a released app compiled in keeps its meaning. `TabBar`, `RadioGroup`,
  `ComboBox`, `Dialog` and `AlertDialog` are Flutter's own `SemanticsRole` words.
- **A tab strip and a radio group are containers a reader walks into.** The walk maps every
  `AdjustableRole` with no default arm: a slider is still one stop with its value; a tab list is a
  `TabBar` and a radio group a `RadioGroup`, each carrying the author's label and then walked into,
  so every tab and radio is a stop of its own with #338's role and its state. The keyboard does not
  move: the bar is still the one Tab stop and its arrows still step the pick.
- **The pressable a listbox panel hangs from is the `ComboBox`**, the web's rule (`LowerAnchored`
  puts `role="combobox"` on the anchor's root), reached through component seams, with `Expanded`
  saying whether the list is open. A menu's trigger stays a button, as on the web.
- **An open modal layer is a `Dialog`**, or an `AlertDialog` when it interrupts (the destructive
  confirm), named by its label, as the web's `role="dialog"` and `role="alertdialog"` are.
- **The layer Photon opens for an anchored panel is a dialog only when the panel is one**, the date
  picker's calendar. The web has no layer there, and the native one was modal by default, so it read
  as an unnamed group in front of every open menu, select and tooltip.
- **`NativeRole` speaks each in the platform's own words.** From the W3C Core-AAM: `AXTabGroup` and
  `android.widget.TabWidget`, `AXRadioGroup` and `android.widget.RadioGroup`, `AXGroup` with the
  `AXApplicationDialog` or `AXApplicationAlertDialog` subrole and `android.app.Dialog` or
  `android.app.AlertDialog`. The combobox departs from Core-AAM's editable pair (`AXComboBox`,
  `EditText`) for each platform's select-only drop-down: `AXPopUpButton`, NSPopUpButton's role, and
  `android.widget.Spinner`, the class Chrome gives the same web component. UIKit hears the
  containers as a group and the combobox as a button whose expanded status the bridge already sets.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `native-accessibility`: a tab strip and a radio group become containers with their own roles, a
  combobox's field and an open modal layer gain their roles, and each platform's words for the five
  are added beside the pressable roles'.

## Impact

- **Developer surface:** none. An app writes the same `Tabs`, `RadioGroup`, `SegmentedControl`,
  `Select`, `TimePicker`, `DatePicker` and `Dialog`, and on macOS, iOS and Android a screen reader
  now reads their tabs, radios and choices, and says what the field and the dialog are.
- **Public surface:** five `SemanticRole` members, additions only. No break, so no migration line.
- **Parts reached:** Primitives (the enum), Native.Components (the walk, the table, and the layer the
  realizer opens for an anchored panel). The three bridges read the table and do not change. The
  runtime's generated `SemanticRoleValue` union gains the five names. The web realizer does not
  change: it already said all of this.
- **The handoff:** `docs/design/status.json` marks `native-roles` shipped, and the Foundations §10
  table says what the native tree carries.
