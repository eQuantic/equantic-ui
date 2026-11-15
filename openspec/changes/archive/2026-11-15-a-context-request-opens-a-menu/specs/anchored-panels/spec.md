## ADDED Requirements

### Requirement: A panel may open at a point of its anchor

An `Anchored` with `At` set SHALL place its panel's top-left corner at that point of the anchor's box, plus its
`Gap`, instead of along one of the anchor's edges, on both targets; with `At` null it SHALL keep its
`Placement`.

#### Scenario: A menu where the pointer asked

- **WHEN** an open `Anchored` over a 200 by 40 row has `At` (120, 30)
- **THEN** its panel's top-left corner stands 120 px right of the row's left edge and 30 px below its top
