## MODIFIED Requirements

### Requirement: A params parameter is the array it is

A `params` parameter of a method, a local function, a delegate or a constructor on a twin the build writes SHALL be one array: a call that lists its elements packs them, none being an empty array, and an array handed whole SHALL be passed as it is, the callee holding the caller's array, with no limit on its length. A constructor's overloads SHALL count it as one argument, always passed.

#### Scenario: A large array

- **WHEN** a program calls `Count(items)` over `int Count(params int[] xs)` with an array of a million elements
- **THEN** the call answers 1000000, as in .NET

#### Scenario: The caller's array

- **WHEN** a callee writes `xs[0] = 0` over an array handed whole
- **THEN** the caller's array holds the 0, as in .NET

#### Scenario: A constructor handed a large array

- **WHEN** a program builds `new Pack(items)` over `Pack(params int[] items)` with an array of a million elements
- **THEN** the constructor holds the caller's array, of a million elements, as in .NET; it threw RangeError

#### Scenario: A constructor's overloads

- **WHEN** a class declares `Chained(params int[] xs)` and `Chained(string s, int n) : this(1, 2, n)`
- **THEN** the build completes, `new Chained("s", 7)` holds three elements and `new Chained(5, 6)` two, as in .NET; the build refused it with EQ1009
