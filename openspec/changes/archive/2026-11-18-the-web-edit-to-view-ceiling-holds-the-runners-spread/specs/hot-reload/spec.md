## MODIFIED Requirements

### Requirement: The web sample is held to a ceiling that only comes down

The time from a save to the frame that draws it, measured on the dashboard sample under `dotnet
watch`, SHALL be held to a ceiling on its warm median, set from the first CI run that measured it with
room for the runner's spread. The measurement SHALL fail over the ceiling. While the ceiling is more
than one and a half bars, it SHALL say when the median falls under half of it, so the change that made
it faster lowers it in the same pull request. Within one and a half bars, where the ceiling holds the
runner's spread and nothing more, it SHALL say whether the median met the bar.

#### Scenario: Over the ceiling

- **WHEN** the measuring tool times four edits of the web sample with the repository's budget, and
  their warm median is over the ceiling
- **THEN** the run fails, saying the warm median and the ceiling it is over

#### Scenario: Far under the ceiling

- **WHEN** the warm median is under half of a ceiling more than one and a half bars of a second
- **THEN** the run passes and says the ceiling can be lowered

#### Scenario: Within the runner's spread of the bar

- **WHEN** the ceiling is 1.5s and the warm median is 722ms
- **THEN** the run passes and says the median is under the bar of a second, inside its ceiling
