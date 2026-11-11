# Proposal

Closes #1241 (E6a), the first slice of #938, per-component theme slots, and #936, the code palette
that never reached the browser. The design is `docs/ELEMENT-TREE-PLAN.md` section 3.10. `Theme(theme,
child)` applied to a subtree waits for E5's positional theme and is #1387.

## Why

What makes a control look like itself was written in the component or in `Sizing`, a static ladder
no theme reached: `MaterialTheme` repainted every colour and every radius and could not change how
tall a control is. And the browser builds its theme from the JSON the server writes, which carried
no code palette, so a theme that overrides `Code` coloured a code block one way on the server and
another once the browser rendered it again.

## What Changes

- `IAppTheme` gains two slots, each a default member that answers null, so every theme an app wrote
  keeps compiling: `Controls` (`ControlsTheme`), the ladder `Sizing` holds (height, horizontal
  padding and label size per size and density, gap, icon and radius per size), and `Button`
  (`ButtonTheme`), its own ladder and what it wrote itself (minimum width, label weight and
  tracking, outline width and colour, a link's padding, and the fill its quiet variants take). Every member is nullable data, and a ladder rung left
  null is the next step's.
- A sized control resolves each value in the plan's order: its family's slot, the theme's `Controls`,
  then `Sizing`, reading `context.Density` at every step. The Button, and the Buttons a Dialog, a
  Banner, a Pagination and a form build, read their slot; every control that read the ladder reads it
  through the theme.
- `theme.With(controls:, button:)` replaces a slot and forwards every other member, the defaults
  included, for a theme an app builds from another.
- Every member of `IAppTheme` crosses to the browser in the theme's JSON: the two slots, member by
  member, when the theme answers one, and the code palette, every kind's colour.
- `PhotonTheme` fills both slots with today's constants. `MaterialTheme` fills the Button with
  Material 3's common button and leaves the shared ladder its own, since Material 3 states sizes per
  component.

The public surface grows (the two slots, their records, `SizeLadder`, `DensityLadder`,
`AppThemeExtensions.With`) and nothing goes. The developer surface does not move.

## Capabilities

### New Capabilities

- `theme-slots`: a component's look is the theme's to say, through slots resolved in one order and
  carried to the browser.

### Modified Capabilities

(none)

## Impact

Primitives (the slots and their records, `AppThemeExtensions`), the components that read the ladder,
`MaterialTheme`, the web's `ThemeBridge`, the runtime's theme bridge, slot mirrors and interface
defaults, the generated Photon theme, and the wiki's DesignSystem page in five languages.
