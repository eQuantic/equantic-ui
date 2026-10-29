## ADDED Requirements

### Requirement: An element counted from the end reads its receiver once

An element of an array, a list or a string counted from the end (`a[^n]`) SHALL be read and written with
its receiver evaluated once, then its offset, then the length, as C# evaluates them, whatever writes it
(an assignment, a compound assignment, an increment, `??=`, a deconstruction), and its offset SHALL be
one value whatever expression it is written as.

#### Scenario: A receiver made by a call

- **WHEN** `int[] Get() { calls++; return arr; }` and `Get()[^1] += 5` runs over `{ 1, 2, 3 }`
- **THEN** the array holds `1,2,8` and `Get` ran once, as in .NET; it ran three times

#### Scenario: An offset written as a sum

- **WHEN** `var a = new[] { 1, 2, 3, 4 }; int i = 1; return a[^(i + 1)];` runs
- **THEN** it answers 3, as .NET does; it read past the end, `a[a.length - i + 1]`

#### Scenario: An offset that changes the receiver's variable

- **WHEN** `a[^Swap()]` is read, where `Swap` assigns another array to `a` and answers 1
- **THEN** it reads the last element of the array `a` held before `Swap` ran, as in .NET
