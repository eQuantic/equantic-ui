# Tasks

## 1. The vocabulary

- [ ] 1.1 `DensityValue`, and the five records `IconButtonTheme`, `InputTheme`, `SelectionControlsTheme`, `SegmentedTheme` and `ChipTheme`, one per file, every member nullable data. Verify: the records compile into `PublicAPI.Unshipped.txt`, and `ThemeSlotsTests` reads every property of each
- [ ] 1.2 The five default members of `IAppTheme`, forwarded by `OverriddenTheme` and taken by `With`, after E6a's. Verify: `OverriddenThemeTests` finds no member left to the interface

## 2. The components read their slots

- [ ] 2.1 IconButton, TextInput, SearchField, Select, TimePicker, Checkbox, RadioGroup, Switch, Slider, SegmentedControl, Stepper and Chip each read their family's slot in the slot order, `ControlMetrics` for a ladder number with the family's `Controls`, and `AtDensity` for a value by density. Verify: one test per family puts a slot value on a theme and finds it in the web's lowering and in Photon's layout
- [ ] 2.2 `PhotonTheme` fills the five slots with today's constants. Verify: the transpiled pins, the parity fixture and the goldens regenerate to no diff

## 3. Material's specifications

- [ ] 3.1 `MaterialTheme` fills the five slots from Material 3's component specifications. Verify: a test lowers each family under `MaterialTheme` and reads the specification's numbers

## 4. The crossing

- [ ] 4.1 Each slot has a TypeScript mirror and builder in `theme-slots.ts`, a default in `interface-defaults.ts` and an export, and crosses through the generic slot writer. Verify: `ThemeSlotsTests` and `theme-bridge.spec.ts` hold every member on the wire, and `dotnet build src/eQuantic.UI.Runtime -t:TestRuntime` passes

## 5. The documentation

- [ ] 5.1 The wiki's Theming page in its five languages, and one ledger entry citing #1242. Verify: the docs guards pass against the wiki branch
