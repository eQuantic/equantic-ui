## ADDED Requirements

### Requirement: The form controls read their family's slot

`IAppTheme` SHALL answer five slots that answer null by default:
- `IconButton`, which styles the IconButton;
- `Input`, which styles TextInput, SearchField, Select and TimePicker;
- `Selection`, which styles Checkbox, RadioGroup, Switch and Slider;
- `Segmented`, which styles SegmentedControl and Stepper;
- `Chip`, which styles the Chip.

Every member of each slot SHALL be nullable data. Each component SHALL resolve every value of its look in the order of the theme slots:
- its family's slot first;
- then, for a number of the control ladder, the theme's `Controls`;
- then the component's own default.

A value that varies by density SHALL be read at `context.Density` at every step.

#### Scenario: A slot reaches the web's lowering

- **WHEN** a page under `PhotonTheme.Instance.With(selection: new SelectionTheme { Box = new DensityValue { Comfortable = 30 } })` holds a `Checkbox`
- **THEN** the checkbox's box lowers 30 wide and 30 tall on the web

#### Scenario: A slot reaches Photon's layout

- **WHEN** the same page is laid out on Photon
- **THEN** the checkbox's box is laid out 30 by 30

#### Scenario: A member left null is the component's own

- **WHEN** a theme answers `Selection` with only `Box` named
- **THEN** the checkbox's border and corner are the ones it draws with no slot

#### Scenario: The density picks its side

- **WHEN** the slot's `Box` is `new DensityValue { Comfortable = 30, Compact = 24 }` and the page is compact
- **THEN** the checkbox's box is 24 by 24

### Requirement: The SDK's themes fill the form control slots

`PhotonTheme` SHALL answer the five form control slots with the values its components drew before the slots existed, so that every pinned output stays byte for byte. `MaterialTheme` SHALL answer them from Material 3's component specifications.

#### Scenario: Photon draws what it drew

- **WHEN** the transpiled pins, the parity fixture and the realizers' goldens are produced under `PhotonTheme`
- **THEN** they are byte for byte what they were before the slots

#### Scenario: Material draws its own text field

- **WHEN** a `TextInput` is lowered under `MaterialTheme`
- **THEN** its container is 56dp tall and its outline is 1dp, and 2dp while focused

### Requirement: The form control slots cross to the browser

Every member a form control slot names SHALL cross to the browser in the theme the server writes into the shell, under its camel-cased name. The browser SHALL read the slot in the same order as the server.

#### Scenario: A slot member crosses

- **WHEN** the server's theme answers `Chip` with `PaddingX = 20`
- **THEN** the theme the browser builds answers `chip.paddingX` as 20, and a chip the browser renders again is padded by 20
