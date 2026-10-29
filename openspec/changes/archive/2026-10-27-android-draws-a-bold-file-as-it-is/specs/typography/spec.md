## MODIFIED Requirements

### Requirement: Text is set in each of the nine weights

`FontWeight` SHALL name the nine weights OpenType defines: `Thin` (100), `ExtraLight` (200), `Light` (300),
`Regular` (400), `Medium` (500), `SemiBold` (600), `Bold` (700), `ExtraBold` (800) and `Black` (900). Text set
in any of them SHALL be drawn at that weight: on the web, by the server and by the runtime alike, and on
Photon, by each shell's text engine. A family that ships files SHALL be drawn from the file that sets the
weight and the slant, or, when none does, from the file a browser would choose (CSS Fonts §5.2), on the web,
on Apple, on Android and on Windows, and on Apple a variable file SHALL be set at the weight on its `wght`
axis. A static face's weight SHALL be the one position its `STAT` table gives it on `wght` when it gives one,
and its weight class otherwise. On Windows and on Android from API 28, whose text engines are asked for a
weight over the file chosen, a static file asked for the weight it sets, or for a heavier one when the file
sets 600 or more, SHALL be drawn as the file draws, whatever weight the file declares itself. On Android below
API 28, which cannot be asked for a weight, the shell SHALL draw a weight below 600 as regular and the others
as bold.

#### Scenario: A light weight on the web

- **WHEN** a page sets text in `FontWeight.Light`
- **THEN** the markup the server renders and the node the runtime lowers both carry `font-weight: 300`

#### Scenario: A family that ships a file per weight, on Photon

- **WHEN** Photon on Apple or on Windows measures text set in `Light` in a family that ships files at 300,
  400 and 700
- **THEN** it measures as the 300 file does

#### Scenario: The system face, on Photon

- **WHEN** Photon on Apple draws the same text in the system face in each of the nine weights
- **THEN** each one draws differently from the eight others

#### Scenario: Two thin faces that share a weight class

- **WHEN** an app declares Inter's `Thin` and `ExtraLight` from Google Fonts, whose files both carry the
  weight class 250 and give 100 and 200 in their `STAT`
- **THEN** the catalog carries them at 100 and 200, and the web, Apple, Android and Windows draw each weight
  from its own file

#### Scenario: A file weighed heavier than it declares, on Windows

- **WHEN** a family's one file declares the weight class 400 and the family gives it 700, and Photon on
  Windows draws text in it in `Bold`
- **THEN** it draws as the same file does in `Regular` in a family that gives it 400, with no bold laid over
  it, and so it does in `Black`

#### Scenario: A file weighed heavier than it declares, on Android

- **WHEN** a family's one file declares the weight class 400 and the family gives it 700, and Photon on
  Android from API 28 draws text in it in `Bold`
- **THEN** it draws as the same file does in `Regular` in a family that gives it 400, with no bold laid over
  it, and so it does in `Black`

#### Scenario: A weight a lighter file lacks, on Android

- **WHEN** a family's one file declares the weight class 400 and the family gives it 400, and Photon on
  Android from API 28 draws text in it in `Bold`
- **THEN** it draws bolder than the same file does in `Regular`, as a browser synthesizes the bold a family
  lacks

#### Scenario: A variable file, on Apple

- **WHEN** Photon on Apple measures text set in `Bold` in a family that ships one variable file whose `wght`
  axis runs from 100 to 900
- **THEN** it measures as the file's instance at 700 does, not as its default instance

#### Scenario: A weight the family does not ship

- **WHEN** text is set in `SemiBold` in a family that ships files at 300, 400 and 700
- **THEN** the web, Apple, Android and Windows draw it from the 700 file, as a browser chooses

#### Scenario: A provider's weights

- **WHEN** an app declares `[assembly: GoogleFont("Inter", FontWeight.Light, FontWeight.Black)]`
- **THEN** the build fetches Inter's 300 and 900 files, and `Fonts.Inter` carries both
