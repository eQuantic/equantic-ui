# text-direction Specification

## Purpose
A page reads the way its language does: right to left for Arabic, Hebrew, Persian or Urdu, left to right for
the rest, with .NET's culture as the one answer. A layout written in start and end mirrors with it, a subtree can
read the other way, and what places its content by a physical position reads left to right in every language.

## Requirements

### Requirement: A page and a subtree say which way they read

The vocabulary SHALL have a `TextDirection` (`LeftToRight`, `RightToLeft`) and a `Directionality` that sets it for
its subtree. A component's `TextDirection` SHALL be the nearest `Directionality` above it, or the UI culture's
direction when there is none, on every target. A component that reads it SHALL be built again when it changes.

#### Scenario: The culture's direction

- **WHEN** a component reads its `TextDirection` with the UI culture `ar` and no `Directionality` above it
- **THEN** it reads `RightToLeft`, and with `en` it reads `LeftToRight`

#### Scenario: A subtree overrides it

- **WHEN** a component sits under `Directionality(TextDirection.LeftToRight, …)` in an `ar` page
- **THEN** it reads `LeftToRight`

### Requirement: The web lays out start and end as the reader's start and end

The served page SHALL carry `dir` beside `lang`, from the UI culture, and a culture switch in the browser SHALL set
it from the culture's data. A `Directionality` SHALL write `dir` on its child's own element and add no element of its own, the nearest one winning. On the web, a
start and an end SHALL be written as logical CSS, so they mirror under `rtl`: an asymmetric padding or border width,
a `Positioned` start or end, and a `Positioned` shift, which moves toward the end. The server and the browser SHALL
write the same markup for both directions.

#### Scenario: An Arabic page

- **WHEN** the dashboard sample is served with the UI culture `ar`
- **THEN** its `<html>` carries `dir="rtl"`, and in a browser a row whose padding is larger at its start sits with
  that padding on its right, and a child positioned at its start is measured from the right edge

#### Scenario: A switch in the browser

- **WHEN** a page in `en` switches its culture to `ar` without a reload
- **THEN** the document's `dir` is `rtl`

### Requirement: What places content by physical position reads left to right

A table viewport, a spreadsheet surface and a code surface SHALL lay out left to right on the web whatever the
page's direction, and the code editor, the code block, the code diff, the diagram and the spreadsheet's entry lines
SHALL build left to right.

#### Scenario: A table in an Arabic page

- **WHEN** a `TableView` is lowered in an `ar` page
- **THEN** its viewport's host carries `dir="ltr"`

#### Scenario: A code diff in an Arabic page

- **WHEN** a `CodeDiff` is lowered in an `ar` page
- **THEN** its gutter is on the left and its horizontal scroll opens to the right

### Requirement: A glyph that matches the direction turns where it reads right to left

An `IconGlyph` SHALL say whether it matches the text direction (`MatchTextDirection`, false by default), and the
curated `ChevronLeft` and `ChevronRight` SHALL match it. On the web, both producers SHALL draw a matching glyph
mirrored horizontally wherever its element reads right to left and unmirrored wherever it reads left to right, a
`dir="ltr"` surface inside an `rtl` page included, and SHALL follow a change of direction without lowering again.
A glyph that does not match SHALL be drawn as before. Photon SHALL draw every glyph unmirrored while it lays out
left to right.

#### Scenario: A back chevron in an Arabic page

- **WHEN** `Icon(Icons.ChevronLeft)` is lowered under `Directionality(TextDirection.RightToLeft, …)` on the web
- **THEN** its `<svg>` carries `scale: var(--eq-inline, 1) 1`, the server's markup and the browser's are equal, and
  in a browser the drawn chevron points right

#### Scenario: Inside a left-to-right surface

- **WHEN** a code block's fold chevron (`Icons.ChevronRight`) is drawn in an `ar` page
- **THEN** it points right, because the code block reads left to right

#### Scenario: A glyph that does not match

- **WHEN** `Icon(Icons.Play)` is lowered under `Directionality(TextDirection.RightToLeft, …)`
- **THEN** its `<svg>` carries no `scale`

#### Scenario: Photon

- **WHEN** `Icon(Icons.ChevronLeft)` is drawn on Photon with the UI culture `ar`
- **THEN** it is drawn exactly as with the UI culture `en`

### Requirement: A horizontal drag is measured toward the end

A horizontal `Draggable`'s offset, its `Min`, `Max` and `RestOffset` SHALL be measured toward the end of the
direction where its surface reads: on the web, a finger moving toward the reading end SHALL increase the offset
in both directions, the surface SHALL be painted at its offset toward the end, and its rest SHALL be written so it
mirrors under `rtl`. A vertical drag SHALL be unchanged. Photon SHALL measure toward the right while it lays out
left to right.

#### Scenario: A switch dragged on

- **WHEN** a `Switch` that is off is dragged leftwards past half its travel under `Directionality(RightToLeft, …)`
  on the web
- **THEN** it turns on, and its thumb never leaves the track while the finger moves

#### Scenario: A row swiped open

- **WHEN** a `SwipeableRow` is swiped rightwards past half its action's width in an `ar` page
- **THEN** it reports open, and rests with its action uncovered on its left

#### Scenario: A slider dragged up

- **WHEN** a `Slider` at 0.5 is dragged leftwards by a quarter of its width under `Directionality(RightToLeft, …)`
- **THEN** it reports 0.75

#### Scenario: A drag inside a left-to-right surface

- **WHEN** a `Switch` under `Directionality(LeftToRight, …)` in an `ar` page is dragged rightwards
- **THEN** it turns on, as in an English page

### Requirement: The horizontal arrows step in the reading direction

On the web, ArrowRight SHALL mean the next item and ArrowLeft the previous one where the element reads left to
right, and the reverse where it reads right to left, for a `Navigable` and an `Adjustable` alike; ArrowUp,
ArrowDown and every other key SHALL keep their meaning. `NavigableKeys.Move` SHALL take the direction. Photon
SHALL keep the left-to-right meaning while it lays out left to right. The code editor's, the spreadsheet's and the
table viewport's own keymaps SHALL be unchanged.

#### Scenario: A tab list in Arabic

- **WHEN** ArrowLeft is pressed on the selected first tab of `Tabs` in an `ar` page
- **THEN** the second tab is selected; with `en`, ArrowRight selects it

#### Scenario: A radio group, a segmented control and a slider

- **WHEN** ArrowLeft is pressed on a `RadioGroup`, a `SegmentedControl` and a `Slider` under
  `Directionality(RightToLeft, …)`
- **THEN** each one adjusts by +1: the next option, the next segment, the value one step up

#### Scenario: A calendar and a tree

- **WHEN** ArrowLeft is pressed in a `Calendar` and on a closed node of a `TreeView` in an `ar` page
- **THEN** the calendar moves to the next day and the node opens, and ArrowRight moves to the previous day and
  closes the node

#### Scenario: The key table

- **WHEN** `NavigableKeys.Move("ArrowLeft", false, TextDirection.RightToLeft)` is asked
- **THEN** it answers `NavigableMove.NextItem`, and with `TextDirection.LeftToRight` it answers
  `NavigableMove.PreviousItem`

#### Scenario: Photon

- **WHEN** ArrowRight is pressed on a focused `Adjustable` on Photon with the UI culture `ar`
- **THEN** it adjusts by +1, as with `en`

### Requirement: A horizontal sortable list reads in the reading order

On the web, a horizontal `Sortable` SHALL place a carried item among the others by their order from the reading
start, a vertical one SHALL read travel toward the reading end as deeper, and the keyboard carry's ArrowLeft and
ArrowRight SHALL swap where the list reads right to left.

#### Scenario: Dropping between two chips

- **WHEN** an item of a horizontal `Sortable` in an `ar` page is carried to just right of the first item's middle
- **THEN** it lands at index 0, before the first item in reading order

#### Scenario: Carrying deeper in a tree

- **WHEN** an item of a vertical `Sortable` with an `Indent` is carried leftwards by one indent in an `ar` page
- **THEN** it asks for a depth one deeper than its own

### Requirement: A horizontal scroll offset counts from the start

On the web, a horizontal `ScrollView`'s `Offset`, its `ScrollRequest` and what `OnScrolled` reports SHALL be the
distance from the reading start, in both directions.

#### Scenario: Scrolled to the end in Arabic

- **WHEN** a horizontal `ScrollView` under `Directionality(RightToLeft, …)` is scrolled 100 pixels toward its end
- **THEN** `OnScrolled` reports 100, and an `Offset` of 100 scrolls it to the same place
