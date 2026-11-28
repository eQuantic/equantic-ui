## ADDED Requirements

### Requirement: A corner's gradient line is CSS's on both targets

A box's linear gradient toward a corner SHALL run along the line CSS places for that corner keyword, on Photon as on the
web: through the box's centre, perpendicular to the diagonal through the other two corners, from where the line through
the opposite corner meets it to where the line through the named corner does. The gradient's middle SHALL therefore pass
through the other two corners. A text's gradient SHALL take the same line across the text's box.

#### Scenario: A wide box toward the bottom right

- **WHEN** a 400 by 240 box's gradient runs from black to white toward the bottom right
- **THEN** Photon reads 127 at the top-right corner, 128 at the bottom-left one and 96 at (300, 0), within 2, as
  Chromium 156 reads them, on the Reference backend, Metal and Vulkan

#### Scenario: A tall box toward the bottom right

- **WHEN** a 240 by 400 box's gradient runs from black to white toward the bottom right
- **THEN** Photon reads 127 at the top-right corner, 128 at the bottom-left one and 96 at (0, 300), within 2

#### Scenario: Toward the bottom left

- **WHEN** a 400 by 240 box's gradient runs from black to white toward the bottom left
- **THEN** Photon reads 127 at the top-left corner, 128 at the bottom-right one and 95 at (100, 0), within 2

### Requirement: A gradient's stops mix as the web draws them

A box's gradient, its glow and a text's gradient SHALL mix their stops premultiplied in sRGB on Photon, as a CSS
gradient does: each colour weighed by its alpha, and the mix divided by the mixed alpha. An artwork's gradient run SHALL
mix its stops straight, as an SVG paint server does. The CPU reference and the shader SHALL mix alike.

#### Scenario: A colour into transparent

- **WHEN** a box's gradient runs from red to transparent, drawn over nothing
- **THEN** Photon reads red at three quarters, half and a quarter of its alpha along it, (255, 0, 0, 191),
  (255, 0, 0, 127) and (255, 0, 0, 64) within 2, as Chromium 156 reads `linear-gradient(to right, red, transparent)`

#### Scenario: A half-transparent stop beside an opaque one

- **WHEN** a box's gradient runs from blue at half alpha to red, drawn over nothing
- **THEN** Photon reads (170, 0, 85, 192) at its middle, within 2

#### Scenario: An artwork's run

- **WHEN** an artwork's run goes from red to transparent black
- **THEN** its paint mixes straight and reads (129, 0, 0, 127) at its middle, within 2, as Chromium 156 reads the SVG
  `linearGradient`
