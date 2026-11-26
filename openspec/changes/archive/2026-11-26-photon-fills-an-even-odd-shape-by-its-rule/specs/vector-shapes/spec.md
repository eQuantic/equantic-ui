## ADDED Requirements

### Requirement: A shape fills by its own rule on every target

A vector shape or an icon glyph that asks for the even-odd rule SHALL paint only the points inside an odd number of its outlines, on every target. The web SHALL write `fill-rule="evenodd"` on its path, and every Photon rasterizer SHALL fill it by the same rule. A shape that asks for none SHALL fill by the nonzero rule. A stroke SHALL be the union of its outline whatever the rule.

#### Scenario: A ring of two circles turned the same way, at even-odd

- **WHEN** a ring whose two circles are turned the same way fills by the even-odd rule
- **THEN** its centre is left empty and its rim is painted, on the web and on Photon's rasterizer

#### Scenario: The same ring at nonzero

- **WHEN** the same ring fills by the nonzero rule
- **THEN** its centre is painted

#### Scenario: A drawing's shape

- **WHEN** a drawing's shape asks for the even-odd rule
- **THEN** the glyph Photon rasterizes it from carries the rule

#### Scenario: An icon on the web

- **WHEN** an icon whose glyph asks for the even-odd rule is lowered on the web, by the C# realizer or by the runtime
- **THEN** its path carries `fill-rule="evenodd"`, the same on both
