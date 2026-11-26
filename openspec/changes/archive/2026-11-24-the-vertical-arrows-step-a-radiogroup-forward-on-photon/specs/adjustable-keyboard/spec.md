## ADDED Requirements

### Requirement: An adjustable's arrows step by its role, the same on every target

The arrow keys SHALL step an `Adjustable` under focus by its role, from one table both the web and Photon read
(`AdjustableKeys`): ArrowRight forward and ArrowLeft back for every role; for a slider and a separator ArrowUp SHALL
raise the value and ArrowDown lower it; for a tab list and a radio group ArrowDown SHALL choose the next item and
ArrowUp the previous one. Any other key SHALL reach the page untouched.

#### Scenario: A radio group and a tab strip

- **WHEN** ArrowDown, ArrowUp, ArrowRight or ArrowLeft is pressed on a radio group or a tab strip whose second item
  is chosen, on the web and on Photon
- **THEN** ArrowDown and ArrowRight choose the third item, and ArrowUp and ArrowLeft the first, on both targets

#### Scenario: A slider

- **WHEN** ArrowUp is pressed on a slider at half, on the web and on Photon
- **THEN** its value rises on both targets
