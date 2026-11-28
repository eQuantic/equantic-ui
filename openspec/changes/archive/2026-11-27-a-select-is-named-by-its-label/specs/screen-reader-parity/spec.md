## ADDED Requirements

### Requirement: A select is named by its label on every target

A `Select` given a label SHALL be announced as a combobox named by it, with the choice it shows as its value, on the web and on Photon alike. Its label SHALL be drawn above the field and left out of the reading, so the words are heard once. With the label left out of the drawing for a form that draws its own, the field SHALL still be named by it. A select with no label SHALL have no name, never the choice it shows.

#### Scenario: A labelled select

- **WHEN** a `Select` labelled "City" showing "Lisbon" is read on the web and on Photon
- **THEN** both read one combobox named "City" whose value is "Lisbon"

#### Scenario: The label left to the form

- **WHEN** the same select sets `LabelHidden`
- **THEN** nothing is drawn above the field, and both targets still read a combobox named "City" whose value is "Lisbon"

#### Scenario: Open

- **WHEN** the labelled select's list is open
- **THEN** both targets read the combobox named "City", expanded, then its options, alike
