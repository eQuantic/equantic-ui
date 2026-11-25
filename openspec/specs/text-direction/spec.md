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
page's direction, and the code editor, the code block, the diagram and the spreadsheet's entry lines SHALL build
left to right.

#### Scenario: A table in an Arabic page

- **WHEN** a `TableView` is lowered in an `ar` page
- **THEN** its viewport's host carries `dir="ltr"`
