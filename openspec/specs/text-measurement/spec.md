# text-measurement Specification

## Purpose
What Photon charges for the text it lays out when a shell registers no measurer of its own: the
stand-in every golden and every layout test measures with.

## Requirements

### Requirement: The stand-in measurer charges every space

The measurer Photon lays text out with when a shell registers none SHALL charge a space its advance:
between two words, at a paragraph's ends, and in a run that is only a space. A break SHALL drop the
spaces it falls on. A rich paragraph SHALL therefore measure as the same sentence without emphasis.

#### Scenario: A rich paragraph and its plain twin

- **WHEN** "alpha", "alpha beta" and "alpha beta gamma delta" are measured as one run of a rich
  paragraph and as plain text
- **THEN** each pair measures the same width

#### Scenario: The space a Mac draws

- **WHEN** the gap between "a" and "b" is measured by CoreText and by the stand-in, in the body face
- **THEN** the two are within a tenth of the font's size
