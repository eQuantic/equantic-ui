## ADDED Requirements

### Requirement: A dialog is named by the part that titles it on every target

A `TitlePart` node SHALL name the nearest dialog around it by what its subtree says, on every target. A dialog is a
modal, open `Overlay`, or an anchored panel whose `PanelRole` is `AnchorPanelRole.Dialog`. Every `Overlay` and every
anchored panel SHALL start a scope of its own, so a part names only the nearest one, and a part inside a layer that is
not a dialog, or outside every layer, SHALL name nothing. Several parts in one dialog SHALL name it together, in reading
order. A dialog with a part SHALL be named by the part over its `Label`, and by its `Label` when its parts say nothing.

On the web the part SHALL lower to a host with an id scoped by its path and no role, and the dialog's element SHALL list
those ids in `aria-labelledby`. On Photon the dialog's stop SHALL be named by the text under its parts, joined. The part
SHALL be no stop of its own: what it holds SHALL be read where it stands. It SHALL add no geometry of its own: its host
on the web takes the child's fill and cap, and Photon lays it out as it lays out its child. The email realizer SHALL
draw its child as it stands.

#### Scenario: A layer titled by a component, read on both targets

- **WHEN** a modal `Overlay` holding `Column(children: [TitlePart(Row(children: [Icon(Icons.Warning, label: null),
  Text("Rename file")])), Text("The name is shared.")])` is read on the web through `ComponentTester` and on Photon
  through `PhotonHost.Semantics()`
- **THEN** both readings start with `Dialog "Rename file"`, followed by the static texts `Rename file` and
  `The name is shared.`

#### Scenario: A part beside a Label

- **WHEN** a modal `Overlay` whose `Label` is `Settings` holds `TitlePart(Text("Rename file"))`, and another holds
  `TitlePart(Text("Rename file") { ExcludeFromSemantics = true })`, each read on both targets
- **THEN** the first dialog is named `Rename file` on both, and the second `Settings` on both

#### Scenario: Two parts name one dialog

- **WHEN** a modal `Overlay` holds `TitlePart(Text("Step 2"))` and, later in reading order, `TitlePart(Text("Payment"))`
- **THEN** its dialog is named `Step 2 Payment` on both targets, and the web's dialog lists both parts' ids in
  `aria-labelledby`, in that order

#### Scenario: A part names only the nearest dialog

- **WHEN** a non-modal `Overlay` holding `TitlePart(Text("Saved"))` stands inside a modal `Overlay` labelled `Editor`
- **THEN** the modal layer is named `Editor` on both targets, and the web writes no `aria-labelledby` on it

#### Scenario: A part outside every layer

- **WHEN** `TitlePart(Text("Orders"))` is lowered on a page with no layer
- **THEN** the web writes the host with no id, Photon announces only the static text `Orders`, and neither names
  anything by it
