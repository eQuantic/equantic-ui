## ADDED Requirements

### Requirement: A value no enum member names crosses as the browser holds it

A value of an enum that is not a flags one SHALL cross from the server to the browser as the browser holds it: a member's name as that name, and a value no member names as the number, or the BigInt for an enum over a long, as a field, as a collection's element and as a dictionary's key.

#### Scenario: An unnamed value in a field

- **WHEN** a page's prefetch sets a field to `(CrossingRank)7` and the browser compares it with `(CrossingRank)7`
- **THEN** the comparison answers true, as on the server; the browser could not draw the page

#### Scenario: An unnamed value over a long

- **WHEN** the field holds `(CrossingTier)4611686018427387905` of an enum over a long
- **THEN** the browser holds the BigInt with every digit
