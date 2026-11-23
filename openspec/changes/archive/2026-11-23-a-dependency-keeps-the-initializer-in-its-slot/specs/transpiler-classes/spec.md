## ADDED Requirements

### Requirement: A component's dependency leaves no slot before its initializer

A construction of a component whose constructor takes a dependency, built with an object initializer, SHALL pass
its other arguments in the parameters the twin's constructor keeps, fill the kept parameters no argument reached
with their own defaults, and pass the initializer in the config slot after them, so the component holds what .NET
builds whichever way its arguments are written.

#### Scenario: A named argument after the dependency

- **WHEN** a component is built with `new Quark(null, mood: "x") { Size = 3 }`, for
  `Quark(IClock? clock, string mood = "calm")` with `int Size { get; init; }`
- **THEN** it reads `Mood` `x` and `Size` `3`, as in .NET

#### Scenario: No argument after the dependency

- **WHEN** it is built with `new Quark(null) { Size = 5 }`
- **THEN** it reads `Mood` `calm` and `Size` `5`, as in .NET

#### Scenario: The dependency in the middle

- **WHEN** a component is built with `new Middle("a", null) { Tone = "warm" }`, for
  `Middle(string before, IClock? clock, int after = 3)`
- **THEN** it reads `Before` `a`, `After` `3` and `Tone` `warm`, as in .NET
