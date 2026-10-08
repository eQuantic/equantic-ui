## ADDED Requirements

### Requirement: A type reached through its namespace is imported by what it binds

A type the C# reaches through its namespace, part of it, the whole of it or `global::`, or through a
using alias, SHALL be written by its own name and imported by the symbol the model binds, as a type
reached through a using is, whatever member the expression reads from it. An enum or an interface
reached that way SHALL import no module.

#### Scenario: A type reached through part of its namespace

- **WHEN** inside `namespace App.Chat`, `Portal.Fold.Text(2)`, `Portal.Fold.Max`, `new Portal.Tally().N` and `Portal.Tally.Zero` name the types of `App.Portal`
- **THEN** the module imports `Fold` and `Tally`, and each answers what .NET answers

#### Scenario: A type reached through its whole namespace, and through global::

- **WHEN** `App.Portal.Fold.Text(1)` and `global::App.Portal.Tally.Zero`
- **THEN** the module imports `Fold` and `Tally`, and each answers what .NET answers

#### Scenario: A type reached through a using alias

- **WHEN** `using F = App.Portal.Fold;` and `F.Text(4)`, `F.Max`
- **THEN** the module writes and imports `Fold`, and each answers what .NET answers

#### Scenario: An enum's member reached through its namespace

- **WHEN** `Portal.Mood.Loud` and `App.Portal.Mood.Calm`
- **THEN** each is its member's value, and the module imports no module of the enum's name
