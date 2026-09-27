# Spec Delta

## ADDED Requirements

### Requirement: A date prints through its specifier as .NET prints it

A `DateTime`'s `ToString`, with a standard specifier, a custom picture or neither, SHALL print the
text .NET prints for it: a standard specifier from the culture's patterns (the invariant ones with
no culture in force), `o`, `s`, `u` and `R` from the value's own parts, `U` from the value read as
local time and moved to UTC, a time a transition skips or repeats read as standard time, and a custom
picture token by token, its fraction of a second exact.
A provider SHALL be read as .NET reads it: the invariant culture writes the invariant patterns,
no specifier, or a null or empty one, is `G` in the current culture, the current culture or a null is the call with none, and any other culture is refused at the build
(EQ2108). A null `DateTime?` SHALL write nothing, and no time zone SHALL move a value's parts but `U`'s.

#### Scenario: A standard specifier

- **WHEN** `new DateTime(2026, 9, 24, 10, 30, 15).ToString("D")` runs with no culture in force
- **THEN** it prints `Thursday, 24 September 2026`, as in .NET

#### Scenario: The invariant provider

- **WHEN** `d.ToString("G", CultureInfo.InvariantCulture)` runs
- **THEN** it prints `09/24/2026 10:30:15`, and the browser is handed no name it lacks

#### Scenario: The round-trip form off UTC

- **WHEN** `new DateTime(2026, 9, 24, 10, 30, 15, 250).ToString("o")` runs on a machine at UTC+1
- **THEN** it prints `2026-09-24T10:30:15.2500000`, the value's own hour and seven fractional digits

#### Scenario: A time a spring-forward gap skips

- **WHEN** `new DateTime(2026, 3, 8, 2, 30, 0).ToString("HH:mm")` runs on a machine in America/New_York
- **THEN** it prints `02:30`, where a local Date moved it to `03:30`, and `U` prints `07:30`, as .NET does

#### Scenario: A time a fall-back repeats

- **WHEN** `new DateTime(2026, 11, 1, 1, 30, 0).ToString("U")` runs on a machine in America/New_York
- **THEN** it prints `Sunday, 01 November 2026 06:30:00`, the time read as standard time as .NET reads
  it, where the Date constructor took the daylight instant and printed `05:30:00`

#### Scenario: A null or an empty format

- **WHEN** `d.ToString((string)null)` or `d.ToString("")` runs with pt-BR in force
- **THEN** it prints `24/09/2026 10:30:15`, the culture's `G`, as .NET does

#### Scenario: A custom picture

- **WHEN** `d.ToString("dd MMM yyyy")` and `d.ToString("HH:mm:ss.fff")` run
- **THEN** they print `24 Sep 2026` and `10:30:15.250`
