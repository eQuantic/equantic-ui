## ADDED Requirements

### Requirement: A test reads the stylesheet a frame is written against

`ComponentTester.Css` SHALL be the atomic stylesheet of the current frame: the rule of every atomic
class that `Html` names, as the server writes it beside the page. It SHALL throw when no frame has been
drawn, as `Html` does.

#### Scenario: A button's rules are beside its markup

- **WHEN** a test pumps `Button("Pay")`
- **THEN** every atomic class the markup names has its rule in `Css`, and `Css` holds `min-width:64px`

### Requirement: Every shipped control is pinned at every variant, size and density it accepts

The SDK's suites SHALL draw every public component of its assemblies, derived from the build output
and never listed, at every `Variant` and `SizeVariant` it accepts and at both densities. A component
accepts an axis when its widest constructor takes one of that type, or when it has one public property
of that type with a public setter. Each cell SHALL be pinned three ways: the markup and the atomic
rules the web lowers, what its semantics announce, and every box Photon lays out. The pins SHALL be
rewritten only behind `EQ_UPDATE_VARIANT_MATRIX=1`. A cell that cannot exist SHALL be a line of a
baseline with its reason, and the baseline SHALL only shrink: an entry is written by hand, and the
update drops the entries that no longer occur.

#### Scenario: A look changes

- **WHEN** a badge's horizontal padding changes from 4 to 8
- **THEN** the matrix fails, naming the badge's 20 cells whose pin differs

#### Scenario: A control joins the SDK

- **WHEN** a component with no variant and no size is added, with no pin and no line in the baseline
- **THEN** the matrix fails, naming its missing pin and the two axes it lacks
