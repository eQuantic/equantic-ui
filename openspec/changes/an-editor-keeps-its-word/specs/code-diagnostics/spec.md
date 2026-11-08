## ADDED Requirements

### Requirement: A problem of no width stays before a line written below it

A problem of no width SHALL stay where it is when what is inserted there begins with a line break, and
SHALL go past any other text inserted there.

#### Scenario: A line moved past the document's end

- **WHEN** `alpha` and `beta` are two lines, a problem of no width stands at the end of `beta`, and ⌥↑ moves `beta` up
- **THEN** the problem stands at the end of `beta`, on the first line

#### Scenario: Enter at a missing semicolon

- **WHEN** Enter is pressed at a problem of no width at the end of a line
- **THEN** the problem stays at the end of that line
