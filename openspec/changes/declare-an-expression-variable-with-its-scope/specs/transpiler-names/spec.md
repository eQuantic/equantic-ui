# Spec Delta

## ADDED Requirements

### Requirement: A local named after a global the emitted code reads is escaped

A local or a parameter named `undefined`, `NaN` or `Infinity`, which no JavaScript rule reserves but
which the emitted code compares against (a TryParse's `!== undefined`, `double.NaN`), SHALL be
escaped with a `$` as a reserved word is, so that the global keeps its meaning wherever the local is
in scope.

#### Scenario: A local named undefined beside a TryParse

- **WHEN** `var undefined = 5; int.TryParse("x", out var n); return n + undefined;` runs
- **THEN** it answers 5, as in .NET
