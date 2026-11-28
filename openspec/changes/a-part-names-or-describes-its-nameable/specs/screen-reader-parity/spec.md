## ADDED Requirements

### Requirement: A nameable is named and described by the parts that reach it on every target

A `LabelPart` SHALL name, and a `DescriptionPart` SHALL describe, the nearest nameable it reaches, by what its subtree
says, on every target. The nameables SHALL be a modal, open `Overlay`, an anchored panel whose `PanelRole` is
`AnchorPanelRole.Dialog`, a `Group`, a `LiveRegion`, a `TextEntry`, and a `Pressable` whose `Role` is a checkbox, a
radio or a switch.

A part SHALL reach the first ancestor that is a nameable container, or that holds exactly one nameable control outside
the part that no nearer part reached and that carries no label of its own. Every `Overlay` and every anchored panel
SHALL be a scope, so a part inside a layer that is not a dialog, or outside every nameable, SHALL name and describe
nothing. An ancestor holding two such controls SHALL be refused, on every target, with the same message. Several parts
SHALL name or describe one nameable together, in reading order. A nameable with a label part SHALL be named by the part
over its own label, and by its label when its parts say nothing. A description part SHALL be read after the nameable's
own description.

On the web a part SHALL lower to a host with no role, which takes an id scoped by its path when it reaches a nameable,
and the nameable SHALL list the ids in `aria-labelledby` or `aria-describedby`. On Photon the nameable's stop SHALL be
named by the text under its label parts, and SHALL carry the text under its description parts as its `Description`. A
part SHALL be no stop of its own: what it holds SHALL be read where it stands. It SHALL add no geometry of its own. The
email realizer SHALL draw its child as it stands.

#### Scenario: A layer titled by a component, read on both targets

- **WHEN** a modal `Overlay` holding `Column(children: [LabelPart(Row(children: [Text("Rename"), Text("file")])),
  Text("The name is shared.")])` is read on the web through `ComponentTester` and on Photon through
  `PhotonHost.Semantics()`
- **THEN** both readings start with `Dialog "Rename file"`, followed by the static texts the part and the message hold

#### Scenario: A part beside a Label

- **WHEN** a modal `Overlay` whose `Label` is `Settings` holds `LabelPart(Text("Rename file"))`, and another holds
  `LabelPart(Text("Rename file") { ExcludeFromSemantics = true })`, each read on both targets
- **THEN** the first dialog is named `Rename file` on both, and the second `Settings` on both

#### Scenario: Two parts name one dialog

- **WHEN** a modal `Overlay` holds `LabelPart(Text("Step 2"))` and, later in reading order, `LabelPart(Text("Payment"))`
- **THEN** its dialog is named `Step 2 Payment` on both targets, and the web's dialog lists both parts' ids in
  `aria-labelledby`, in that order

#### Scenario: A part names only the nearest dialog

- **WHEN** a non-modal `Overlay` holding `LabelPart(Text("Saved"))` stands inside a modal `Overlay` labelled `Editor`
- **THEN** the modal layer is named `Editor` on both targets, and the web writes no `aria-labelledby` on it

#### Scenario: A field named and described by the parts beside it

- **WHEN** a `Column` holds `LabelPart(Text("Email"))`, a `TextEntry` whose `Description` is `Required`, and
  `DescriptionPart(Text("We never share it."))`
- **THEN** the field is announced on both targets as a text field named `Email` and described by
  `Required We never share it.`

#### Scenario: A dialog's title beside a field that names itself

- **WHEN** a modal `Overlay` holds `LabelPart(Text("Rename file"))` and a `TextEntry` whose `Label` is `Name`
- **THEN** the dialog is named `Rename file` and the field `Name`, on both targets

#### Scenario: A group and a checkbox named by their parts

- **WHEN** a `Group` holds `LabelPart(Text("Shipping"))`, and a `Row` holds a checkbox `Pressable` with no label beside
  `LabelPart(Text("I accept the terms"))`
- **THEN** the group is named `Shipping` and the checkbox `I accept the terms`, on both targets

#### Scenario: A part beside two fields

- **WHEN** a `Column` holds `LabelPart(Text("Email"))` and two `TextEntry` nodes with no label
- **THEN** the web's lowering, the browser's and Photon's semantics each fail with the message that names the
  `LabelPart`, the column, the two controls, and tells the developer to give each field its own `Column` or `Group`

#### Scenario: A part outside every nameable

- **WHEN** `LabelPart(Text("Orders"))` is lowered on a page with no nameable
- **THEN** the web writes the host with no id, Photon announces only the static text `Orders`, and neither names
  anything by it
