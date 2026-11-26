## ADDED Requirements

### Requirement: An int and a long wrap in the default context as .NET wraps them

This requirement covers an `int`, `long`, `uint` and `ulong` result of `+`, `-`, `*` or `<<`, of a
negation, increment or decrement, and of a compound assignment.
- In C#'s default context such a result SHALL wrap to its type's width, as it does in `unchecked` and
  as .NET wraps it.
- In a `checked` context it SHALL throw `OverflowException`.
- A constant C#'s compiler folded outside `unchecked` SHALL be written as the operation, without a
  wrap, since it is in range.

#### Scenario: An int past its maximum

- **WHEN** `int m = int.MaxValue; return m + 1;` runs
- **THEN** it answers -2147483648, as .NET does; it answered 2147483648

#### Scenario: A hash written without unchecked

- **WHEN** `int h = 17; foreach (var c in "eQuantic") h = h * 31 + c; return h;` runs
- **THEN** it answers what .NET answers, the product's low bits included

#### Scenario: A long past its maximum

- **WHEN** `long l = long.MaxValue; return (l + 1).ToString();` runs
- **THEN** it answers "-9223372036854775808", as .NET does

#### Scenario: A step a void lambda holds

- **WHEN** `int c = int.MaxValue; Action step = () => c++; step(); return c;` runs
- **THEN** it answers -2147483648, and the module writes the step as an assignment

#### Scenario: A folded constant

- **WHEN** `var r = -5;` is compiled
- **THEN** the module writes `let r = -5;`
