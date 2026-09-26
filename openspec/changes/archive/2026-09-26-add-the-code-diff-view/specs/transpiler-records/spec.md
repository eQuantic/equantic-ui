# Spec Delta

## ADDED Requirements

### Requirement: A record's twin says what C# declares

A record's twin SHALL annotate a nullable delegate as a function that may be missing, and SHALL
import every runtime name its annotations use, `Decimal` included.

#### Scenario: A record with a fold label and an amount

- **WHEN** a record has a `decimal Amount`, a method returning `(decimal Net, decimal Tax)`, and a
  parameter `Func<int, string>? label`
- **THEN** its module imports `Decimal`, the method is annotated `[Decimal, Decimal]`, and the
  parameter `((value: number) => string) | null`
