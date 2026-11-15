## ADDED Requirements

### Requirement: A labelled group is named and then walked into on every target

A `Group` node SHALL be announced as a group named by its `Label`, after which what it holds SHALL be
read in order: on the web a `role="group"` element carrying `aria-label`, on Photon a
`SemanticRole.Group` node with that label, followed by the nodes of its child. A `Group` whose label is
empty SHALL still be a group, with no name, on both targets. It SHALL add no geometry of
its own: its host on the web takes the child's fill and cap, and Photon lays it out as the web sizes that
host. A `Text` marked `ExcludeFromSemantics` SHALL be drawn and never read on both targets, and no part
of a control's derived name, unless a run of it links somewhere, in which case it SHALL be read.

#### Scenario: A grouped Select read on both targets

- **WHEN** `new Select(["Lisboa"], 0, groups: [new OptionGroup("A seguir", ["Braga"])])` is opened and
  read on the web through `ComponentTester` and on Photon through `PhotonHost.Semantics()`
- **THEN** both readings are, after the combobox, `Option "Lisboa"`, `Group "A seguir"` and
  `Option "Braga"`, in that order, the heading read once as the group's name, with `Selected` true on
  `Lisboa` alone

#### Scenario: A group outside a Select

- **WHEN** `Group(Column(children: [Text("Delivery"), Checkbox(…)]), label: "Shipping")` is laid out on
  Photon
- **THEN** its semantics hold a `SemanticRole.Group` node labelled `Shipping`, followed by the static text
  `Delivery` and the checkbox, and the group's bounds are the column's

#### Scenario: A group with no name

- **WHEN** `Group(Column(children: [Text("Delivery")]))` is lowered on the web and laid out on Photon
- **THEN** the web writes a `role="group"` host with no `aria-label`, and Photon announces a
  `SemanticRole.Group` node with no label, followed by the static text `Delivery`

#### Scenario: A control's name leaves out the text a reader passes over

- **WHEN** a `Pressable` holding `Text("Shipping") { ExcludeFromSemantics = true }` and `Text("Free")` is
  read on the web through `ComponentTester` and on Photon through `PhotonHost.Semantics()`
- **THEN** both name the button `Free`
