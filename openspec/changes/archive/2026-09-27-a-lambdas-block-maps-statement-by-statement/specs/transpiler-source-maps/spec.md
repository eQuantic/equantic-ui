# Spec Delta

## ADDED Requirements

### Requirement: A lambda's block maps statement by statement

Every statement of a lambda's or a `delegate`'s block that reaches the writer as IR SHALL map to its
own C# line, wherever the writer places the arrow: a call's argument, a LINQ operator's template, a
local's initializer, an arrow inside an arrow. The block the emitter adds to a concise body that
declares a variable SHALL map to that body. What follows the block on its closing line SHALL map to
the statement that holds the arrow. Writing the marks SHALL change nothing the writer writes.

#### Scenario: A block lambda passed to List.ForEach

- **WHEN** `values.ForEach(value => { var twice = Twice(value); total += twice; });` is compiled with
  each statement on its own line
- **THEN** the map leads `let twice = this.twice(value);` and `total += twice;` to their own C#
  lines, where neither line had a segment

#### Scenario: A frame thrown inside a lambda's block

- **WHEN** a method called from a statement inside a `List.ForEach` block throws, in the module a
  build bundles with the embedded Bun
- **THEN** the lambda's frame leads, through the composed map, to the statement that called, not to
  the line that holds the lambda

#### Scenario: A frame after a lambda's block

- **WHEN** `var kept = values.FindAll(value => { var doubled = value * 2; return doubled > 0; }).Count + Check(count);`
  runs, its block on lines of its own, and `Check` throws
- **THEN** the frame that called `Check` leads to the statement, not to `return doubled > 0;`, the
  block's last statement
