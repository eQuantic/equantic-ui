## ADDED Requirements

### Requirement: A labelled group is named and then walked into on every target

A `Group` node SHALL be announced as a group named by its `Label`, after which what it holds SHALL be
read in order: on the web a `role="group"` element carrying `aria-label` (none when the label is empty),
on Photon a `SemanticRole.Group` node with that label, followed by the nodes of its child. It SHALL add
no geometry of its own: its host on the web takes the child's fill and cap, and Photon lays it out as the
web sizes that host. `SemanticsParityTests` SHALL find no difference between the two readings of a
grouped `Select`, opened, without a baseline entry.

#### Scenario: A grouped Select read on both targets

- **WHEN** `new Select(["Lisboa"], 0, groups: [new OptionGroup("A seguir", ["Braga"])])` is opened and
  read on the web through `ComponentTester` and on Photon through `PhotonHost.Semantics()`
- **THEN** both readings are, after the combobox, `Option "Lisboa"`, `Group "A seguir"`, `StaticText "A seguir"`
  and `Option "Braga"`, in that order, with `Selected` true on `Lisboa` alone

#### Scenario: A group outside a Select

- **WHEN** `Group(Column(children: [Text("Delivery"), Checkbox(…)]), label: "Shipping")` is laid out on
  Photon
- **THEN** its semantics hold a `SemanticRole.Group` node labelled `Shipping`, followed by the static text
  `Delivery` and the checkbox, and the group's bounds are the column's
