## MODIFIED Requirements

### Requirement: A problem is drawn under its range in its severity

The editor SHALL draw an error, a warning and an information as a wavy line under their range, in the
destructive, the warning and the info colour, and a hint as three dots under its first characters in
the muted ink. A problem whose range is empty SHALL be drawn one cell wide where it stands: under the
character there, or the cell after the line's end, an empty line's included. A problem whose range
takes no width to draw, on a server that measures no column or over characters that take no cell,
SHALL draw no wave, and the code around it SHALL still render. The gutter SHALL mark each line with a problem as an error or
a warning, the worst problem of the line deciding, behind a marker the app put on that line itself.

#### Scenario: An error and a warning on two lines

- **WHEN** an editor holds an error on line 1 and a warning on line 2
- **THEN** a wave in the destructive colour is drawn under the error's range, a wave in the warning
  colour under the warning's, and the gutter marks line 1 as an error and line 2 as a warning

#### Scenario: A problem the server cannot measure

- **WHEN** the server renders a code block holding an error, with no font to measure its columns
- **THEN** the block renders with its code and no wave, marked for the browser, which draws it
  again with the wave
