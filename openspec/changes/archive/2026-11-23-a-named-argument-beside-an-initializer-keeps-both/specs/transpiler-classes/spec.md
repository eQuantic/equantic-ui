## ADDED Requirements

### Requirement: A named argument and an initializer both reach the twin

A construction that hands its object initializer to the twin as the constructor's trailing config SHALL
place each argument in the parameter the bound tree binds it to, fill the parameters no argument reached
with their defaults counted from the arguments emitted, and pass the initializer in the config slot, so a
named argument beside an initializer keeps both.

#### Scenario: A vocabulary record with a named argument and an initializer

- **WHEN** a component builds `new TextRun(code, Mono: true) { StyleOverride = new TypeStyle(13, …) }`
- **THEN** the twin receives the content, no color and `mono` in its positional parameters and the
  initializer in its config, and the run's `Mono` and its `StyleOverride` are what .NET reads

#### Scenario: Every argument named, out of order

- **WHEN** `new TextRun(Mono: true, Content: "x") { Italic = true }` is built
- **THEN** the twin's content is `"x"`, its `mono` is true and its `italic` is true, as in .NET
