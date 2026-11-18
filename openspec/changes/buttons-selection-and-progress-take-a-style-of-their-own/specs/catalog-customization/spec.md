## ADDED Requirements

### Requirement: Buttons, selection controls and progress take their family's style

`Button`, `FormSubmit`, `IconButton`, `Chip`, `Checkbox`, `RadioGroup`, `Switch`, `Slider`, `ProgressBar`,
`PullToRefresh` and `Skeleton` SHALL each take a constructor parameter `style` and a property `Style` of their
family's slot record (`ButtonTheme` for the first two, `IconButtonTheme`, `ChipTheme`, `SelectionControlsTheme`
for the four selection controls, `ProgressTheme` for the last three), and their factories SHALL mirror it. Each
SHALL resolve its slot as the record's `Merge` of the theme's slot under its own `Style`, on the web, in the
browser's twin and on Photon alike: a member the instance names wins, and a member it leaves null is the
slot's. A `ProgressBar` retained across its parent's builds SHALL adopt the style its parent hands it.

#### Scenario: The instance's member wins over the slot's

- **WHEN** a theme's `SelectionControls` slot names a checkbox box of 29 and a `Checkbox` is built with
  `style: new SelectionControlsTheme { Box = new DensityPair { Comfortable = 33 } }`
- **THEN** the checkbox's box is 33 wide on the web and on Photon, and no box of it is 29 wide

#### Scenario: A member the instance leaves null is the slot's

- **WHEN** a theme's slot names a radio ring of 29 and a `RadioGroup` is built with a style that names only its
  dot, 13
- **THEN** the group draws a ring of 29 and a dot of 13

#### Scenario: The submit button hands its style on

- **WHEN** a `FormSubmit` is built with `style: new ButtonTheme { MinWidth = 191 }` under a theme whose `Button`
  slot names 173
- **THEN** the button it builds is at least 191 wide, and nothing is 173

#### Scenario: A retained bar adopts a new style

- **WHEN** a `ProgressBar` built with a bar height of 7 adopts a configuration whose style names 11
- **THEN** its next build draws a bar 11 tall

### Requirement: A radio group's options take a builder

`RadioGroup` SHALL take `optionBuilder`, a `Func<int, VisualNode>` over an option's index, which draws what the
option shows in place of its name. The group SHALL keep its chrome: the ring, the row a press lands on, the one
Tab stop, and each option's name in `options` as that option's accessible name. A content that names no colour
SHALL take the option's colour, the primary text, or the muted text while the group is disabled. With no
builder, the group SHALL draw each option's name, as before.

#### Scenario: The builder draws, the name stays the radio's name

- **WHEN** a `RadioGroup(["Standard", "Express"], …, optionBuilder: i => new Text(shown[i]))` is drawn
- **THEN** the drawn texts are the builder's and not the names, and the radios a reader meets are named
  "Standard" and "Express", on the web and on Photon

#### Scenario: The content takes the option's colour

- **WHEN** the same group is disabled
- **THEN** the builder's text, which names no colour, is drawn in the theme's muted text
