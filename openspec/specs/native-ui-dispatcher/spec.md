# native-ui-dispatcher Specification

## Purpose
TBD - created by archiving change posted-work-marks-no-hosts-tree. Update Purpose after archive.

## Requirements

### Requirement: Work posted to the thread that draws is the process's, not a host's

Work posted to Photon's UI dispatcher SHALL wake an idle window: every shell SHALL draw a frame while work is waiting, and that frame SHALL run the work at its top. A post SHALL NOT mark any host's tree as changed, and SHALL NOT change what a host has scheduled. A host's `NeedsRender` SHALL say only what its own tree asked for, and its `IsFrameDue` only what it scheduled. The frame the work's own `SetState` needs SHALL be asked for by the position the work marks, from that position's host.

#### Scenario: A worker posts to an idle window

- **WHEN** a window has settled and a worker thread posts work
- **THEN** the window wants a frame, its `NeedsRender` stays false, and after that frame it wants none

#### Scenario: Another thread posts while a live region owes an announcement

- **WHEN** a live region changed inside its quiet window, and another thread posts work before the window ends
- **THEN** the host's `NeedsRender` stays false, the announcement stays scheduled for the window's end, and it is said there once
