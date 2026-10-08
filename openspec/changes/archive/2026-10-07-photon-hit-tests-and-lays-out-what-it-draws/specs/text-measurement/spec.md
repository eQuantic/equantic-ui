## ADDED Requirements

### Requirement: The stand-in measurer charges every space

The measurer Photon lays text out with when a shell registers none SHALL charge a space its advance:
between two words, at a paragraph's ends, and in a run that is only a space. A break SHALL drop the
spaces it falls on. A rich paragraph SHALL therefore measure as the same sentence without emphasis, on one line or broken
over several, whichever run its spaces belong to.

#### Scenario: A rich paragraph and its plain twin

- **WHEN** "alpha", "alpha beta", "alpha beta gamma delta", " alpha" and "  alpha beta" are measured as
  one run of a rich paragraph and as plain text
- **THEN** each pair measures the same width

#### Scenario: A rich paragraph that breaks

- **WHEN** "alpha beta", "alpha  beta gamma" and "  alpha beta", and "alpha beta" as the runs "alpha "
  and "beta", are measured in a box that holds their first line and one space more
- **THEN** each breaks after its first line and measures as its plain twin, without the spaces the
  break falls on

#### Scenario: The space a Mac draws

- **WHEN** the gap between "a" and "b" is measured by CoreText and by the stand-in, in the body face
- **THEN** the two are within a tenth of the font's size
