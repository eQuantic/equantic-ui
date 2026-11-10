## ADDED Requirements

### Requirement: A ref or out argument's element is taken once

An element of an array passed as a `ref` or `out` argument SHALL have its receiver and its position evaluated once, before the call, in the order C# evaluates the arguments, and the call SHALL read it and the write-back write it through that one receiver and position, whether the element is counted from the end or, where its receiver or position runs code, from the start. An argument taken before the call SHALL NOT count as code run ahead of the arguments after it.

#### Scenario: A ref element counted from the end

- **WHEN** `Twice(ref Get()[^1])` runs over `{ 1, 2 }`, `Twice` doubling its parameter
- **THEN** the array holds `1,4` and `Get` ran once, as in .NET; it ran twice

#### Scenario: A ref element whose position steps

- **WHEN** `Twice(ref a[i++])` runs over `{ 1, 2, 3 }` with `i` at 0
- **THEN** the array holds `2,2,3` and `i` is 1, as in .NET

#### Scenario: Two ref elements of calls

- **WHEN** `Swap(ref Get()[^1], ref Get()[0])` runs over `{ 1, 2, 3 }`
- **THEN** the array holds `3,2,1` and `Get` ran twice, in order, as in .NET
