## ADDED Requirements

### Requirement: A merged subtree is heard as one stop on every target

A `MergeSemantics` node SHALL be heard as ONE stop. When a control is among the stops its subtree would make,
the stop SHALL be the first control, with that control's role, value and states, and activating it SHALL press
that control on every target; a later control SHALL stay a stop of its own, after the merged one. With no
control the stop SHALL be a group, and nothing inside it SHALL be a stop of its own. It SHALL add no geometry of
its own.

#### Scenario: A switch beside its label

- **WHEN** `MergeSemantics(Row[Text("Wi-Fi"), Switch(true, onChanged)])` is read on Photon through
  `PhotonHost.Semantics()` and on the web through `ComponentTester`
- **THEN** both readings are one stop, `Switch "Wi-Fi"`, checked, and activating it on Photon or tapping it on
  the web calls `onChanged`

#### Scenario: Read content with no control

- **WHEN** `MergeSemantics(Row[Icon(Check, label: "Paid"), Text("Total"), Text("42 EUR")])` is read on both
  targets
- **THEN** both readings are one stop, `Group "Paid Total 42 EUR"`

#### Scenario: Two controls

- **WHEN** `MergeSemantics(Row[Text("Volume"), Slider(0.5), Button("Mute")])` is read on both targets
- **THEN** both readings are `Slider "Volume"` and then `Button "Mute"`

### Requirement: A merged stop is named by what its subtree says

A merged stop's name SHALL join, in reading order, the names of the stops its subtree would make, the merged
control's own in its place and a later control's left out. What a reader passes over, a `Text` marked
`ExcludeFromSemantics` or an `ExcludeSemantics` subtree, SHALL be no part of it. On the web the name SHALL be
computed by the browser from the parts the merge hides, so it follows their content.

#### Scenario: The merged name leaves out what a reader passes over

- **WHEN** `MergeSemantics(Row[ExcludeSemantics(Text("JD")), Text("Initials") { ExcludeFromSemantics = true },
  Text("Jane Doe")])` is read on both targets
- **THEN** both readings are one stop, `Group "Jane Doe"`

### Requirement: An excluded subtree is heard by no one on every target

An `ExcludeSemantics` node whose `Excluding` is true SHALL leave its whole subtree out of what a reader hears on
every target, controls included, and SHALL change nothing a person does with it: a control inside SHALL still
take a press and a Tab stop. With `Excluding` false its subtree SHALL be read as if the node were not there, and
the node SHALL stay in the tree. It SHALL add no geometry of its own.

#### Scenario: Decoration beside what is read

- **WHEN** `Row[ExcludeSemantics(Row[Icon(label: "Done"), Text("Decoration")]), Text("Shown")]` is read on
  both targets
- **THEN** both readings are `StaticText "Shown"` alone, and with `Excluding = false` they are `Image "Done"`,
  `StaticText "Decoration"` and `StaticText "Shown"`

#### Scenario: A control inside an exclusion

- **WHEN** `ExcludeSemantics(Pressable(Text("Hidden"), onPressed))` is laid out on Photon
- **THEN** its semantics are empty, the pressable is still a focus stop, and activating it calls `onPressed`
