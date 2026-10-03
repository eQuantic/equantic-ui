## ADDED Requirements

### Requirement: An enum's text is .NET's

An enum's `ToString()`, its interpolation and its concatenation SHALL write what .NET writes: a
member's declared name, a `[Flags]` combination's set flags joined by `, ` in ascending order, a value
no member names as its digits, and nothing for a null nullable enum. A format SHALL write what .NET
writes for it: `D` the number, `X` its hex in the underlying type's width, `F` the set flags of any
enum, and `G` or none the text above, and an unknown format SHALL throw. An alignment SHALL pad that
text.

#### Scenario: A flags combination

- **WHEN** browser-side code prints `Perm.Read | Perm.Write` of `[Flags] enum Perm { None = 0, Read = 1, Write = 2, Exec = 4 }`
- **THEN** it writes `Read, Write`, as in .NET

#### Scenario: A format and an alignment

- **WHEN** browser-side code writes `$"{r:D}|{r,6}|{r,-6:X}|"` for `Rank.Mid` of `enum Rank { Zeta, Alpha, Mid = 5 }`
- **THEN** it writes `5|   Mid|00000005|`, as in .NET

#### Scenario: A nullable enum

- **WHEN** browser-side code writes `$"[{s,10}][{n,3}]"` for `Status? s = Status.Pending` and `Status? n = null`
- **THEN** it writes `[   Pending][   ]`, as in .NET

### Requirement: The statics of Enum read the enum's shape

`Enum.Parse`, `TryParse`, `GetName`, `GetNames`, `GetValues` and `IsDefined` SHALL answer in the
browser what they answer in .NET, for the enum their type argument or their `typeof` names: `Parse` reads a name, names
joined by commas and a number, keeps the case unless told not to, and throws where .NET throws;
`TryParse` leaves the enum's default on a failure, and the overload that takes a `Type` leaves null;
`GetName` answers the name of the member with the value, or null where none has it;
`GetNames` and `GetValues` follow the values; `IsDefined` reads its argument as the enum, a number, a
declared name, or an `object` holding any of them.

#### Scenario: Parse and TryParse

- **WHEN** browser-side code calls `Enum.TryParse<Status>("nope", out var s)` and `Enum.Parse<Status>("pending", true)`
- **THEN** the first answers false with `s` the first member, and the second answers `Pending`, as in .NET

#### Scenario: An object holding the enum

- **WHEN** browser-side code casts `Enum.Parse(typeof(Status), "Pending")` to `Status` and asks
  `Enum.IsDefined(typeof(Status), o)` for `object o = Status.Pending`
- **THEN** the cast is `Pending` and `IsDefined` answers true, as in .NET

### Requirement: An enum crosses as a dictionary key

A dictionary keyed by an enum SHALL cross between the server and the browser in both directions, its
keys as the browser holds them: a member's camelCase name, and a `[Flags]` enum's number. A name no
member has SHALL be refused, as a value and as a key.

#### Scenario: Keys both ways

- **WHEN** the server serializes a `Dictionary<Shelf, int>` and a `Dictionary<Channels, int>` keyed by a flags enum
- **THEN** it writes `{"dataAccess":1,"core":2}` and `{"1":1,"3":3}`, and reads both back to the same dictionaries

#### Scenario: A key no member has

- **WHEN** the server reads `{"gone":1,"old":2}` as a `Dictionary<Shelf, int>`
- **THEN** it throws a `JsonException` naming `'gone'`
