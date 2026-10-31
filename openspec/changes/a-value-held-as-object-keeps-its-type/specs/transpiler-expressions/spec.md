## ADDED Requirements

### Requirement: A value held as object keeps the type its answers depend on

An enum member and a float boxed into `object` or `ValueType` SHALL keep their .NET type in the browser: their
`Equals`, `GetHashCode` and `ToString`, a type pattern, a constant pattern, a `switch` over `object`, an unboxing
cast, `Convert` and the `Enum` statics that take an `object` SHALL answer as .NET's do, and JSON SHALL write the
value as it was written before it was boxed. A `List<T>` and a `T[]` held as `object` SHALL compare and hash by
reference, and a value tuple by its elements. An anonymous object SHALL never equal a value of another type that
has the same members.

#### Scenario: A boxed member is not a string of its name

- **WHEN** browser-side code runs `Enum.IsDefined(typeof(Status), (object)"pending")` and `(Status)(object)"pending"`
- **THEN** the first answers false and the second throws an InvalidCastException, as in .NET; they answered true and the member

#### Scenario: A boxed float and a boxed member print as .NET prints them

- **WHEN** browser-side code writes `object o = 0.1f;` and `object e = Tone.Warm;` into text
- **THEN** they print `0.1` and `Warm`, as in .NET; they printed `0.10000000149011612` and `warm`

#### Scenario: Two lists held as object

- **WHEN** browser-side code compares `object a = new List<int> { 1 }, b = new List<int> { 1 };` with `a.Equals(b)`
- **THEN** it answers false, as in .NET, and `new HashSet<object> { a, b }` holds two; it answered true and one

#### Scenario: A value held as data and an anonymous object

- **WHEN** browser-side code runs `Color.White.Equals((object)new { R = (byte)255, G = (byte)255, B = (byte)255, A = (byte)255 })`
- **THEN** it answers false, as in .NET; it answered true
