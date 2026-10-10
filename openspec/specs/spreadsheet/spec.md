# spreadsheet Specification

## Purpose
How the shared spreadsheet lays out its grid, its headers and its marks, the same on the web and on
Photon.

## Requirements

### Requirement: A cell sits under its column's header on every target

The spreadsheet's cells SHALL start after the row number strip, under the headers of their columns,
on every target and whatever the width of the grid. The strip SHALL keep the header's width beside a
grid wider than its window.

#### Scenario: A grid wider than its window

- **WHEN** a spreadsheet of 26 columns is shown in a window narrower than its grid
- **THEN** the row number strip is 44 wide and A1 starts at the left edge of the "A" header, on the web
  as on Photon
