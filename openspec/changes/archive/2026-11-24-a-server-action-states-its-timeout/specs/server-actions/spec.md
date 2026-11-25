## ADDED Requirements

### Requirement: A Server Action states how long the browser waits for it

An action marked `[ServerAction(TimeoutSeconds = n)]` SHALL be invoked by its stub with `timeoutMs` set to
`n` seconds, and the browser SHALL abandon the call, throwing, only once that time has passed. An action that
states no timeout, or 0, SHALL keep the runtime's 30 seconds and its stub SHALL be written as before. The value
SHALL be read as a constant, and a value below 0 or above 600, or one the build cannot read as a constant,
SHALL be refused at build time with EQ2017, at the argument's line.

#### Scenario: An action that states two minutes

- **WHEN** a page declares `[ServerAction(TimeoutSeconds = 120)] Task<string> Run()`
- **THEN** its stub calls `invoke('<id>', [], { timeoutMs: 120000 })`, and a call still unanswered at 30 seconds goes on waiting until 120

#### Scenario: An action that states none

- **WHEN** a page declares `[ServerAction] Task<string> Load()`
- **THEN** its stub calls `invoke('<id>', [])`, and the call is abandoned at 30 seconds

#### Scenario: A constant expression

- **WHEN** an action states `TimeoutSeconds = TwoMinutes`, where `const int TwoMinutes = 2 * 60`
- **THEN** its stub hands `{ timeoutMs: 120000 }`

#### Scenario: A value the stub could not honour

- **WHEN** an action states `TimeoutSeconds = -5`, `601` or `60 * 60`
- **THEN** the build fails with EQ2017 at the line of that argument, naming the component, the action and the value
