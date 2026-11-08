## ADDED Requirements

### Requirement: A scroller fixed on a line's axis is not squeezed

A `ScrollView` with a fixed size on a flex line's axis SHALL keep it on the web and on Photon, and its
width SHALL be a ceiling at the line's width wherever it stands in the line, as the web's
`max-width: 100%` makes it. A scroller that hugs that axis SHALL still give way.

#### Scenario: A fixed width wider than the row

- **WHEN** a row 400 wide holds a box fixed at 100 and a horizontal `ScrollView` 540 wide
- **THEN** they are 100 and 400 wide, on Photon and in Chrome

#### Scenario: A fixed width that fits

- **WHEN** the scroller is 200 wide
- **THEN** it keeps 200

#### Scenario: A hugging scroller

- **WHEN** the scroller declares no width
- **THEN** it is 300 wide

#### Scenario: A fixed height in a column

- **WHEN** a column 400 tall holds a box fixed at 100 and a `ScrollView` 540 tall
- **THEN** the scroller keeps 540, on Photon and in Chrome

#### Scenario: A fixed width in a column

- **WHEN** a column 200 wide holds a horizontal `ScrollView` 540 wide
- **THEN** it is 200 wide
