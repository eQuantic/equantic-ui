# Spec Delta

## ADDED Requirements

### Requirement: Every member of a type holds a key of its own on its twin

eqc SHALL refuse with EQ1007, naming both members, a second member of one type that lands on a key of
its twin another member already holds: the same lowered name and the same static-ness, whatever kind
each member is, a field, a constant, a property, an event, a method, an operator, a conversion, an
extension block's property, indexer or method, and a primary constructor's parameter the twin holds. A
member's key SHALL be the name the emitter writes it under: a field's slot, a hiding method's own name,
an operator's and a conversion's static. What the twin holds as one member SHALL NOT be refused: the two
accessors of a property, a plain class's field that moved to a slot of its own, a positional parameter
its record redeclares, and a static beside an instance member of its name.

#### Scenario: A static field beside a static method of its name

- **WHEN** an app declares `static class Seq { static int next; public static int Next() => ++next; }`
- **THEN** the build fails with EQ1007 at `Next`, naming `Seq.Next()` and `Seq.next`

#### Scenario: A component's field beside its property

- **WHEN** a stateless component declares `readonly string label = "x";` and `public string Label => label.ToUpper();`
- **THEN** the build fails with EQ1007 at `Label`, naming `Page.Label` and `Page.label`

#### Scenario: Two operators of one symbol

- **WHEN** a record declares `operator +(Sum, Sum)` and `operator +(Sum, int)`
- **THEN** the build fails with EQ1007, naming both operators and asking to keep one

#### Scenario: A plain class's field beside its property

- **WHEN** a class declares `private int level;` and `public int Level { get => level; set => level = value * 2; }`
- **THEN** the build succeeds, the field written in a slot of its own

### Requirement: A member never takes a name its twin holds itself

eqc SHALL refuse with EQ1007 a member that lands on a name the twin holds itself: an instance
`constructor` and a static `prototype` on any class, `with` on a record or a struct, and `equals`,
`getHashCode`, `toString` and `toJSON` on a class, a record or a struct. An override of `Equals(object)`,
`GetHashCode()` or `ToString()` SHALL stand, and the twin SHALL write it in place of its own member of
that name and write nothing else there.

#### Scenario: A record's With method

- **WHEN** a record declares `public Style With(int size) => this with { Size = size };`
- **THEN** the build fails with EQ1007, naming `Style.With(int)` and `with`

#### Scenario: An overload of ToString

- **WHEN** a class declares `public string ToString(string unit)`
- **THEN** the build fails with EQ1007, naming `Temp.ToString(string)` and `toString`

#### Scenario: A struct's Equals override

- **WHEN** a struct declares `public override bool Equals(object? o)`
- **THEN** the build succeeds, and the struct's twin holds one `equals`, the override's

#### Scenario: A record's static ToString

- **WHEN** a record declares `public static string ToString(int v)`
- **THEN** the build succeeds, and the record's text is still the one C# synthesizes
