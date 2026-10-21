# Spec Delta

## ADDED Requirements

### Requirement: A cell's address holds at Excel's extent

A cell SHALL be addressable at every row up to 1 048 576 and every column up to 16 384, and the number
the engine keys a cell by SHALL be the same in .NET and in the browser for every such address.

#### Scenario: The last cell of Excel's grid

- **WHEN** the cell at row 1 048 576 and column 16 384 is written `x` and read back, in .NET and in the
  browser
- **THEN** both read `x`, its address reads `XFD1048576`, and its key is 17 179 869 183 on both
