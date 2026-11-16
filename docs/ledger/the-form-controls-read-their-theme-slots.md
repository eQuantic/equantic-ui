- **2026-11-11 · The form controls read their theme slots**: five slots join the control ladder and
  the Button on `IAppTheme`: `IconButton`, `Input` (TextInput, SearchField, Select and TimePicker),
  `SelectionControls` (Checkbox, RadioGroup, Switch and Slider), `Segmented` (SegmentedControl and
  Stepper) and `Chip`, with `DensityPair` for the sizes a selection control varies by density alone.
  What each control wrote that is look moved into its slot: sizes, paddings, gaps, border widths,
  radii, elevations, the label's type and the colours it chose itself. A colour straight from
  `Colors(variant)` stays there, and behaviour stays in the component. Each control reads its
  family's slot, then the theme's `Controls`, then its own default, at `context.Density`.
  `PhotonTheme` fills the slots with what the controls drew, so the pinned outputs under it are byte
  for byte. `MaterialTheme` fills them from Material 3's specifications, so a page under it now draws
  a 40dp icon button, a 56dp text field whose 1dp outline grows to 2dp, an 18dp checkbox, a 20dp
  radio, a 52 by 32 switch, a 40dp segmented button and a 32dp chip. The plan's `Selection` slot is
  `SelectionControls`, since `IAppTheme.Selection` is the colour of selected text, and the chip's
  label is four plain members rather than a `TypeStyle`, since the configuration binder walks the
  theme (#1398) ([#1242](https://github.com/eQuantic/equantic-ui/issues/1242)). Proposed and
  archived through OpenSpec, in `theme-slots`.
