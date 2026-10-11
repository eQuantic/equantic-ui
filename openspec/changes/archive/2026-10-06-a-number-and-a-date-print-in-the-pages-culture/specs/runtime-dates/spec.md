## ADDED Requirements

### Requirement: A DateOnly, a TimeOnly and a DateTimeOffset print as .NET prints them

A `DateOnly`, a `TimeOnly` and a `DateTimeOffset` SHALL print through the formatter a `DateTime` prints
through, each with what its type takes: a `DateOnly` SHALL be its culture's `d` with no format and take
the date specifiers, a `TimeOnly` its culture's `t` and the time specifiers, each refusing a specifier
its type does not take with .NET's FormatException, and a `DateTimeOffset` its culture's `G` with its
offset, its `o` and `K` writing the offset and its `u` and `R` converting by it. A provider SHALL be read
as a `DateTime`'s is, and a null value SHALL write nothing.

#### Scenario: A DateOnly's long date

- **WHEN** `new DateOnly(2026, 9, 24).ToString("D")` runs with no culture in force
- **THEN** it prints `Thursday, 24 September 2026`, as .NET does

#### Scenario: A DateTimeOffset's round-trip form

- **WHEN** `new DateTimeOffset(new DateTime(2026, 9, 24, 10, 30, 15, 250), TimeSpan.FromHours(1)).ToString("o")` runs
- **THEN** it prints `2026-09-24T10:30:15.2500000+01:00`, as .NET does

#### Scenario: The invariant provider

- **WHEN** `new DateOnly(2026, 9, 24).ToString("d", CultureInfo.InvariantCulture)` runs with pt-BR in force
- **THEN** it prints `09/24/2026`, and the browser is handed no name it lacks

### Requirement: A custom picture writes the culture's separators, the offset and the era

A custom date picture SHALL write the culture's date separator for `/` and its time separator for
`:`, the offset for `z`, `zz` and `zzz` (the host's for a `DateTime` of no kind, as .NET reads it), and
the era for `g` and `gg`, and SHALL read a run of one letter as one token whose length decides what it
writes, as .NET's `FormatCustomized` does.

#### Scenario: A culture's date separator

- **WHEN** `new DateTime(2026, 9, 24).ToString("dd/MM/yyyy")` runs with de-DE in force
- **THEN** it prints `24.09.2026`, as .NET does, where it printed `24/09/2026`

#### Scenario: The offset and the era

- **WHEN** `new DateTimeOffset(2026, 9, 24, 10, 30, 0, TimeSpan.FromHours(1)).ToString("yyyy g zzz")` runs
- **THEN** it prints `2026 A.D. +01:00`, as .NET does, where it wrote the letters
