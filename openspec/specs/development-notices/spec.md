# development-notices Specification

## Purpose
What a component tells the developer while the app runs in development: a mistake that draws and
runs, and shows only as a page that behaves wrong, said where the developer looks and nowhere else.

## Requirements

### Requirement: A picker drawn again without the value it reported says so

A `DatePicker`, a `TimePicker` or a `DateTimePicker` that reported a value through `onChanged` and is
drawn again by its parent with no value SHALL say, through `DevelopmentNotices`, that its field emptied
and that a picker shows the value it is handed, naming the value it reported. It SHALL say nothing when
it reported nothing since it was last drawn, or when it is handed a value. A `DateTimePicker` SHALL say
it once, for the moment, and its halves SHALL say nothing of their own.

#### Scenario: An order form that keeps the day and hands back none

- **WHEN** a page draws `DatePicker(null, day => SetState(() => _day = day), label: "Day")`, the tester
  types 7/17/2026 into it, and the page draws again
- **THEN** one notice is heard, starting "A DatePicker reported 7/17/2026 through onChanged and was drawn
  again with no date"

#### Scenario: A picker handed back its value

- **WHEN** the page hands the picker the day it keeps, and the tester types a day
- **THEN** no notice is heard

#### Scenario: A picker nobody chose from

- **WHEN** a page draws an empty picker and draws again because another control changed its state
- **THEN** no notice is heard

#### Scenario: A date and time picker that loses its moment

- **WHEN** a page keeps the moment a `DateTimePicker` reported, hands back none, and the tester chooses
  a day and a time
- **THEN** one notice is heard, for the `DateTimePicker`'s moment, and none for its `TimePicker`

### Requirement: A notice reaches the developer in development only

`DevelopmentNotices.Say` SHALL hand a notice to the sink when one is armed and drop it otherwise. The
browser's development runtime SHALL arm the sink with the console, as a warning, and the production
runtime SHALL arm none. A Photon app SHALL arm it with its logger, as a warning, when its environment is
Development, and keep a sink the app set itself.

#### Scenario: A Photon app in Development

- **WHEN** a Photon app built with `--environment Development` runs and a notice is said
- **THEN** the app's logger receives it as a warning

#### Scenario: A Photon app in Production

- **WHEN** the same app is built with `--environment Production`
- **THEN** the logger receives nothing

#### Scenario: The dashboard in a browser, in Development

- **WHEN** a page served in Development draws `DatePicker(null, …)` and a person types a day into it
- **THEN** the console shows the notice, prefixed `[eQuantic]`
