## ADDED Requirements

### Requirement: An argument guard throws as .NET throws it

`ArgumentNullException.ThrowIfNull`, `ArgumentException.ThrowIfNullOrEmpty` and `ThrowIfNullOrWhiteSpace`,
and `ArgumentOutOfRangeException.ThrowIfZero`, `ThrowIfNegative`, `ThrowIfNegativeOrZero`, `ThrowIfEqual`,
`ThrowIfNotEqual`, `ThrowIfGreaterThan`, `ThrowIfGreaterThanOrEqual`, `ThrowIfLessThan` and
`ThrowIfLessThanOrEqual` SHALL throw where .NET throws and pass where .NET passes, with the exception type
.NET throws, its `ParamName`, its `ActualValue` and its message, the values in it written in the current
culture. A parameter's name the call leaves out SHALL be the text of the argument it checks, as
`CallerArgumentExpression` gives it. The arguments SHALL be evaluated once, in the order they are written.
A guard over a type the browser does not hold faithfully, or whose order it cannot reproduce, SHALL be
refused with EQ2004.

#### Scenario: A negative integer

- **WHEN** `int part = -1; ArgumentOutOfRangeException.ThrowIfNegative(part);` runs
- **THEN** it throws an ArgumentOutOfRangeException whose `ParamName` is "part", whose `ActualValue` is -1
  and whose message is "part ('-1') must be a non-negative value. (Parameter 'part')\nActual value was -1.",
  as in .NET, where the build failed with EQ2004

#### Scenario: A real in the current culture

- **WHEN** `ArgumentOutOfRangeException.ThrowIfNegative(-1.5, "v")` runs in pt-BR
- **THEN** its message is "v ('-1,5') must be a non-negative value. (Parameter 'v')\nActual value was -1,5.",
  as in .NET

#### Scenario: A NaN and a negative zero

- **WHEN** `ThrowIfNegative(double.NaN)`, `ThrowIfNegative(-0.0)` and `ThrowIfZero(-0.0)` run
- **THEN** each throws, as in .NET, and `ThrowIfLessThan(-0.0, 0.0)` passes

#### Scenario: Two values compared by the type's own comparer

- **WHEN** `ThrowIfEqual(double.NaN, double.NaN)` and `ThrowIfGreaterThan("b", "a")` run
- **THEN** both throw, as in .NET, and `ThrowIfGreaterThan(double.NaN, double.NaN)` passes

#### Scenario: A null in an equality guard and in an ordering guard

- **WHEN** `string s = null; ArgumentOutOfRangeException.ThrowIfNotEqual(s, "a", "v");` runs
- **THEN** it throws with the message "v ('null') must be equal to 'a'. (Parameter 'v')" and no actual
  value, as in .NET, and `ThrowIfLessThan(s, "a", "v")` throws a NullReferenceException, as .NET's call of
  `CompareTo` through null does

#### Scenario: A string that is empty or white

- **WHEN** `ArgumentException.ThrowIfNullOrWhiteSpace("\u0085", "v")` runs
- **THEN** it throws an ArgumentException with the message "The value cannot be an empty string or
  composed entirely of whitespace. (Parameter 'v')", as in .NET, and `"​"`, which .NET does not take
  for white space, passes

#### Scenario: A null with no parameter's name

- **WHEN** `ArgumentNullException.ThrowIfNull(null, null)` runs
- **THEN** it throws an ArgumentNullException with the message "Value cannot be null." and no `ParamName`,
  as in .NET

#### Scenario: Arguments named out of order

- **WHEN** `ArgumentOutOfRangeException.ThrowIfGreaterThan(other: Log(1), value: Log(2))` runs
- **THEN** `Log(1)` runs before `Log(2)`, as in .NET

#### Scenario: A type the browser cannot hold

- **WHEN** `ArgumentOutOfRangeException.ThrowIfNegative((Int128)x)` is built
- **THEN** the build fails with EQ2004, naming the type
