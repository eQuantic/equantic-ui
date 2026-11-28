## MODIFIED Requirements

### Requirement: An adjustable's arrows step by its role, the same on every target

The arrow keys SHALL step an `Adjustable` under focus by its role and the direction it reads in, from one table both
the web and Photon read (`AdjustableKeys`): in a left-to-right reading ArrowRight SHALL step forward and ArrowLeft
back for every role, and in a right-to-left reading ArrowLeft SHALL step forward and ArrowRight back; for a slider and
a separator ArrowUp SHALL raise the value and ArrowDown lower it; for a tab list and a radio group ArrowDown SHALL
choose the next item and ArrowUp the previous one, whatever the direction. The web SHALL read the direction where the
adjustable sits; Photon SHALL read left to right until it mirrors its layout. Any other key SHALL reach the page
untouched.

#### Scenario: A radio group and a tab strip

- **WHEN** ArrowDown, ArrowUp, ArrowRight or ArrowLeft is pressed on a radio group or a tab strip whose second item
  is chosen, on the web and on Photon, in a left-to-right reading
- **THEN** ArrowDown and ArrowRight choose the third item, and ArrowUp and ArrowLeft the first, on both targets

#### Scenario: A slider

- **WHEN** ArrowUp is pressed on a slider at half, on the web and on Photon
- **THEN** its value rises on both targets

#### Scenario: A tab strip in a right-to-left reading on the web

- **WHEN** ArrowLeft is pressed on a tab strip whose second tab is chosen, inside a `Directionality(RightToLeft)` or
  a page in Arabic, on the web
- **THEN** the third tab is chosen, and ArrowRight from the second chooses the first, while ArrowDown still chooses the
  third
