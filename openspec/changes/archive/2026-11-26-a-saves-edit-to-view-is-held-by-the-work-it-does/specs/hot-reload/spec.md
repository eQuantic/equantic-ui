## MODIFIED Requirements

### Requirement: The desktop sample reaches the screen in under a second

The time from a save to the frame that draws it, measured on the Photon desktop sample under `dotnet watch`, SHALL be held to a budget of a second on its FASTEST warm save, and the measurement SHALL fail when that save is over the budget, naming the part, the value and the median beside it. The fastest is held, not the median, because a loaded machine slows some saves and never makes one faster.

#### Scenario: Within the bar

- **WHEN** the measuring tool times six edits of the desktop sample with the repository's budget
- **THEN** the fastest warm save's total is under 1000ms and the run passes

#### Scenario: Over a budget

- **WHEN** the same edits are held to a budget below their fastest save
- **THEN** the run fails, saying the fastest warm save, its median and the budget it is over

### Requirement: The web sample is held to a ceiling that only comes down

The time from a save to the frame that draws it, measured on the dashboard sample under `dotnet watch`, SHALL be held to a ceiling on its FASTEST warm save, set from what CI measured with room for the runner's spread. The measurement SHALL fail over the ceiling. While the ceiling is more than one and a half bars, it SHALL say when the fastest save falls under half of it, so the change that made it faster lowers it in the same pull request. Within one and a half bars, where the ceiling holds the runner's spread and nothing more, it SHALL say whether the fastest save met the bar.

#### Scenario: Over the ceiling

- **WHEN** the measuring tool times four edits of the web sample with the repository's budget, and their fastest warm save is over the ceiling
- **THEN** the run fails, saying the fastest warm save, its median and the ceiling it is over

#### Scenario: A loaded runner

- **WHEN** the warm saves took 1898, 2488 and 1074ms, a median over the ceiling of 1.5s
- **THEN** the run passes on the fastest, 1074ms

#### Scenario: Far under the ceiling

- **WHEN** the fastest warm save is under half of a ceiling more than one and a half bars of a second
- **THEN** the run passes and says the ceiling can be lowered

#### Scenario: Within the runner's spread of the bar

- **WHEN** the ceiling is 1.5s and the fastest warm save is 361ms
- **THEN** the run passes and says the fastest save is under the bar of a second, inside its ceiling

## ADDED Requirements

### Requirement: A save is held to the work it does

eqc SHALL write, for each compile the measurement asks for, the work it did: whether it kept the compilation the compile before it loaded, the files it walked and compiled, the TypeScript modules it wrote, and the files and bytes bun wrote. The measurement SHALL hold the web sample's warm saves to the most of that work the budget names. It SHALL fail when any warm save did more, saying the count and the budget, and when a warm save loaded the project cold, whatever the time.

#### Scenario: A save of one screen

- **WHEN** a warm save changes one screen of the dashboard sample
- **THEN** eqc compiles one of its files, writes one module, bun writes that screen's files, and the run passes

#### Scenario: Every save recompiles the app

- **WHEN** a regression makes each warm save compile every file of the app
- **THEN** the run fails, saying a warm save compiled 159 files over the budget of 2, whatever the load

### Requirement: A dotnet watch that exits before the app answers is named

When `dotnet watch` exits before the sample answers, the measurement SHALL start it once more, saying what it said last, its exit code and the machine's load. A second exit SHALL fail the run with the same, so a start that cannot succeed is never taken for a slow edit.

#### Scenario: One exit at the start

- **WHEN** `dotnet watch` exits once before the app answers, and the second start answers
- **THEN** the run warns with the first exit's cause and goes on to time the edits
