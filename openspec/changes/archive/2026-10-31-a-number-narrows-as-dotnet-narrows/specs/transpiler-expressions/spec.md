## ADDED Requirements

### Requirement: An explicit cast of a double or a float saturates as .NET does

An unchecked explicit conversion of a `double` or a `float` to an integer SHALL answer what .NET 9 and later
answer on every platform: an `int`, a `uint`, a `long` and a `ulong` take the whole part, the type's nearest
end past its range, and 0 for a NaN, and a `byte`, an `sbyte`, a `short`, a `ushort` and a `char` take the low
bits of that `int`. A checked conversion SHALL keep throwing, and a constant the compiler folds SHALL be the
compiler's value, which is no run-time conversion's.

#### Scenario: Past an int

- **WHEN** `double d = 1e12; return (int)d;` runs
- **THEN** it answers 2147483647, as .NET does; it answered -727379968

#### Scenario: A NaN

- **WHEN** `double d = double.NaN; return (ulong)d;` runs
- **THEN** it answers 0, as .NET does

#### Scenario: A width under an int

- **WHEN** `(byte)d` runs for a `d` of 300, of -1 and of 1e12
- **THEN** it answers 44, 255 and 255, as .NET does

#### Scenario: Below an unsigned width

- **WHEN** `double d = -1; return (ulong)d;` runs
- **THEN** it answers 0, as .NET does; it answered 2^64-1

#### Scenario: A constant the compiler folds

- **WHEN** `return unchecked((int)1e12);` runs
- **THEN** it answers 0, as .NET does, whose compiler folds the cast, where the same cast of a variable answers
  2147483647
